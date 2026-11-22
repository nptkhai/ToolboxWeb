using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using ToolboxWeb.Web.Data;
using ToolboxWeb.Web.Domain;
using ToolboxWeb.Web.Enums;
using ToolboxWeb.Web.Infrastructure.Files;
using ToolboxWeb.Web.Infrastructure.Hosting;
using ToolboxWeb.Web.ViewModels.Files;

namespace ToolboxWeb.Web.Services.Files;

/// <summary>A file operation the user can fix (wrong name, missing file, no access…); the message is shown as is.</summary>
public sealed class FileOperationException : Exception
{
    public FileOperationException(string code, string message) : base(message)
    {
        Code = code;
    }

    /// <summary>notFound, invalid, denied, unavailable, exists, conflict, unsupported.</summary>
    public string Code { get; }
}

/// <summary>
/// "Files &amp; folders" (/files.html): the user's roots and everything read inside them. Every
/// path goes through <see cref="FilePathGuard"/>, every root is the current user's, and on the
/// IIS site only folders under <c>Files:AllowedFolders</c> are reachable (<see cref="FilesAccess"/>).
/// </summary>
public interface IFileWorkspaceService
{
    Task<IReadOnlyList<FileRootView>> GetRootsAsync(CancellationToken cancellationToken = default);

    Task<FileRootView> AddRootAsync(string? path, string? name, CancellationToken cancellationToken = default);

    Task<FileRootView> RenameRootAsync(int id, string? name, CancellationToken cancellationToken = default);

    Task<FileRootView> RelinkRootAsync(int id, string? path, CancellationToken cancellationToken = default);

    Task MoveRootAsync(int id, int offset, CancellationToken cancellationToken = default);

    Task RemoveRootAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>The Windows folder dialog (desktop on Windows); null when cancelled. Throws "unsupported" elsewhere.</summary>
    Task<string?> PickFolderAsync(string? startFolder);

    /// <summary>The folder picker drawn in the page: drives (desktop) or the allowed folders (IIS), then their subfolders.</summary>
    FolderPickView BrowseFolders(string? path);

    Task<FileListView> ListAsync(int rootId, string? path, bool showHidden, CancellationToken cancellationToken = default);

    Task<FileStatsView> GetStatsAsync(int rootId, bool showHidden, CancellationToken cancellationToken = default);

    Task<FileReadView> ReadAsync(int rootId, string? path, CancellationToken cancellationToken = default);

    /// <summary>Full path and content type for showing (images, PDF) or downloading a file.</summary>
    Task<(string FullPath, string ContentType, string Name)> GetRawAsync(int rootId, string? path, CancellationToken cancellationToken = default);

    /// <summary>Size and time of a file, to notice a change made outside Toolkit; null when it no longer exists.</summary>
    Task<FileInfoView?> GetInfoAsync(int rootId, string? path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes the text back in the file's own encoding and line ending. Refuses with "conflict" when the
    /// file changed since <see cref="FileSaveRequest.ExpectedHash"/> (unless Force), and with "encoding" when
    /// the text cannot be stored in the file's code page.
    /// </summary>
    Task<FileSavedView> SaveAsync(FileSaveRequest request, CancellationToken cancellationToken = default);

    /// <summary>Files whose name contains <paramref name="query"/> in every available root (quick open, Ctrl+P).</summary>
    Task<IReadOnlyList<FileFindItem>> FindByNameAsync(string? query, bool showHidden, CancellationToken cancellationToken = default);

    /// <summary>Desktop: "Save a copy" through the Windows "Save as" dialog; returns the new path, or null when cancelled.</summary>
    Task<string?> SaveCopyAsAsync(int rootId, string? path, CancellationToken cancellationToken = default);

    /// <summary>Desktop: opens the file with its Windows program, or shows it selected in Explorer.</summary>
    Task OpenOutsideAsync(int rootId, string? path, bool reveal, CancellationToken cancellationToken = default);
}

public sealed class FileWorkspaceService : IFileWorkspaceService
{
    /// <summary>Folders hidden unless "Show hidden files" is on: tool output nobody reads by hand.</summary>
    public static readonly IReadOnlySet<string> NoiseFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", "bin", "obj", "__pycache__"
    };

    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly FilesAccess _access;
    private readonly AppModeInfo _mode;
    private readonly IOptionsMonitor<FilesOptions> _options;
    private readonly IActivityLogService _activityLog;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public FileWorkspaceService(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        FilesAccess access,
        AppModeInfo mode,
        IOptionsMonitor<FilesOptions> options,
        IActivityLogService activityLog,
        IStringLocalizer<SharedResource> localizer)
    {
        _db = db;
        _currentUser = currentUser;
        _access = access;
        _mode = mode;
        _options = options;
        _activityLog = activityLog;
        _localizer = localizer;
    }

    private FilesOptions Options => _options.CurrentValue;

    // ------------------------------------------------------------------ roots

    public async Task<IReadOnlyList<FileRootView>> GetRootsAsync(CancellationToken cancellationToken = default)
    {
        var roots = await UserRoots().OrderBy(x => x.SortOrder).ThenBy(x => x.Id).AsNoTracking().ToListAsync(cancellationToken);
        return roots.Select(ToView).ToList();
    }

    public async Task<FileRootView> AddRootAsync(string? path, string? name, CancellationToken cancellationToken = default)
    {
        var full = CheckFolder(path);
        var existing = await UserRoots().AsNoTracking().ToListAsync(cancellationToken);
        if (existing.Any(x => FilePathGuard.PathComparer.Equals(FilePathGuard.NormalizeRoot(x.Path), full)))
        {
            throw Error("exists", "Files.Error.RootExists");
        }

        var root = new FileRoot
        {
            UserId = _currentUser.UserId,
            Path = full,
            Name = CleanRootName(name) ?? DefaultName(full),
            SortOrder = existing.Count == 0 ? 0 : existing.Max(x => x.SortOrder) + 1
        };
        _db.FileRoots.Add(root);
        await _db.SaveChangesAsync(cancellationToken);
        await LogAsync(ActivityActionType.Create, ActivityEntityType.FileRoot, root.Id, _localizer["ActivityLog.FileRootAdded", root.Name, root.Path].Value);
        return ToView(root);
    }

    public async Task<FileRootView> RenameRootAsync(int id, string? name, CancellationToken cancellationToken = default)
    {
        var root = await FindRootAsync(id, cancellationToken);
        var clean = CleanRootName(name) ?? throw Error("invalid", "Files.Error.RootNameRequired");
        var old = root.Name;
        root.Name = clean;
        await _db.SaveChangesAsync(cancellationToken);
        await LogAsync(ActivityActionType.Update, ActivityEntityType.FileRoot, root.Id, _localizer["ActivityLog.FileRootRenamed", old, clean].Value);
        return ToView(root);
    }

    public async Task<FileRootView> RelinkRootAsync(int id, string? path, CancellationToken cancellationToken = default)
    {
        var root = await FindRootAsync(id, cancellationToken);
        var full = CheckFolder(path);
        var others = await UserRoots().Where(x => x.Id != id).AsNoTracking().ToListAsync(cancellationToken);
        if (others.Any(x => FilePathGuard.PathComparer.Equals(FilePathGuard.NormalizeRoot(x.Path), full)))
        {
            throw Error("exists", "Files.Error.RootExists");
        }

        root.Path = full;
        await _db.SaveChangesAsync(cancellationToken);
        await LogAsync(ActivityActionType.Update, ActivityEntityType.FileRoot, root.Id, _localizer["ActivityLog.FileRootRelinked", root.Name, full].Value);
        return ToView(root);
    }

    public async Task MoveRootAsync(int id, int offset, CancellationToken cancellationToken = default)
    {
        var roots = await UserRoots().OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToListAsync(cancellationToken);
        var index = roots.FindIndex(x => x.Id == id);
        if (index < 0)
        {
            throw Error("notFound", "Files.Error.RootNotFound");
        }

        var target = Math.Clamp(index + Math.Sign(offset), 0, roots.Count - 1);
        var moving = roots[index];
        roots.RemoveAt(index);
        roots.Insert(target, moving);
        for (var i = 0; i < roots.Count; i++)
        {
            roots[i].SortOrder = i;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveRootAsync(int id, CancellationToken cancellationToken = default)
    {
        var root = await FindRootAsync(id, cancellationToken);
        _db.FileRoots.Remove(root);
        await _db.SaveChangesAsync(cancellationToken);
        await LogAsync(ActivityActionType.Delete, ActivityEntityType.FileRoot, root.Id, _localizer["ActivityLog.FileRootRemoved", root.Name].Value);
    }

    public async Task<string?> PickFolderAsync(string? startFolder)
    {
        if (!_mode.IsDesktop)
        {
            throw Error("unsupported", "Files.Error.NoWindowsDialog");
        }

        if (!OperatingSystem.IsWindows())
        {
            throw Error("unsupported", "Files.Error.NoWindowsDialog");
        }

        return await WindowsShell.PickFolderAsync(_localizer["Files.Root.PickTitle"].Value, startFolder);
    }

    public FolderPickView BrowseFolders(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return new FolderPickView(null, null, StartingFolders());
        }

        string full;
        try
        {
            full = FilePathGuard.NormalizeRoot(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw Error("invalid", "Files.Error.FolderInvalid");
        }

        if (!_access.IsFolderAllowed(full))
        {
            throw Error("denied", "Files.Error.FolderNotAllowed");
        }

        if (!Directory.Exists(full))
        {
            throw Error("notFound", "Files.Error.FolderMissing");
        }

        var parent = Path.GetDirectoryName(full);
        if (parent is not null && !_access.IsFolderAllowed(parent))
        {
            parent = null;
        }

        var folders = new List<FolderPickItem>();
        try
        {
            foreach (var dir in new DirectoryInfo(full).EnumerateDirectories("*", new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = FileAttributes.System }))
            {
                if (IsHidden(dir) || !_access.IsFolderAllowed(dir.FullName))
                {
                    continue;
                }

                folders.Add(new FolderPickItem(dir.Name, dir.FullName));
                if (folders.Count >= 1000)
                {
                    break;
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            throw Error("denied", "Files.Error.NoAccess");
        }

        folders.Sort((a, b) => NaturalComparer.Instance.Compare(a.Name, b.Name));
        return new FolderPickView(full, parent, folders);
    }

    // ------------------------------------------------------------------ reading

    public async Task<FileListView> ListAsync(int rootId, string? path, bool showHidden, CancellationToken cancellationToken = default)
    {
        var (root, full) = await ResolveAsync(rootId, path, cancellationToken);
        if (!Directory.Exists(full))
        {
            throw Error("notFound", "Files.Error.FolderMissing");
        }

        var entries = new List<FileEntryView>();
        var total = 0;
        try
        {
            foreach (var info in new DirectoryInfo(full).EnumerateFileSystemInfos("*", new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = 0 }))
            {
                var hidden = IsHidden(info);
                if ((hidden && !showHidden) || LeavesRoot(root.Path, info))
                {
                    continue;
                }

                total++;
                if (entries.Count < Options.MaxListEntries)
                {
                    entries.Add(ToEntry(root.Path, info, hidden));
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            throw Error("denied", "Files.Error.NoAccess");
        }

        entries.Sort(CompareEntries);
        return new FileListView(root.Id, FilePathGuard.Relative(root.Path, full), entries, total, total > entries.Count);
    }

    public async Task<FileStatsView> GetStatsAsync(int rootId, bool showHidden, CancellationToken cancellationToken = default)
    {
        var (_, full) = await ResolveAsync(rootId, null, cancellationToken);
        int files = 0, folders = 0;
        long bytes = 0;
        var partial = false;
        var stopAt = DateTime.UtcNow.AddSeconds(3);
        var pending = new Stack<DirectoryInfo>();
        pending.Push(new DirectoryInfo(full));
        var options = new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint };
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (files + folders > 200_000 || DateTime.UtcNow > stopAt)
            {
                partial = true;
                break;
            }

            var dir = pending.Pop();
            try
            {
                foreach (var info in dir.EnumerateFileSystemInfos("*", options))
                {
                    if (!showHidden && IsHidden(info))
                    {
                        continue;
                    }

                    if (info is DirectoryInfo sub)
                    {
                        folders++;
                        pending.Push(sub);
                    }
                    else if (info is FileInfo file)
                    {
                        files++;
                        bytes += file.Length;
                    }
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                // A folder that cannot be read is skipped, like Explorer does.
            }
        }

        return new FileStatsView(files, folders, bytes, partial);
    }

    public async Task<FileInfoView?> GetInfoAsync(int rootId, string? path, CancellationToken cancellationToken = default)
    {
        var (_, full) = await ResolveAsync(rootId, path, cancellationToken);
        var info = new FileInfo(full);
        return info.Exists ? new FileInfoView(info.Length, info.LastWriteTimeUtc, info.IsReadOnly) : null;
    }

    public async Task<FileSavedView> SaveAsync(FileSaveRequest request, CancellationToken cancellationToken = default)
    {
        var (root, full) = await ResolveAsync(request.Root, request.Path, cancellationToken);
        var text = request.Text ?? string.Empty;
        var info = new FileInfo(full);
        if (info.Exists && info.IsReadOnly)
        {
            throw Error("denied", "Files.ReadOnly.Attribute");
        }

        if (Directory.Exists(full) || (info.Exists && KindOf(info.Name) is "image" or "pdf" or "office" or "binary"))
        {
            throw Error("invalid", "Files.ReadOnly.NotText");
        }

        var format = new TextFormat(
            AllowedEncoding(request.Encoding),
            request.Bom,
            request.LineEnding is "CRLF" or "LF" or "CR" ? request.LineEnding : TextFileCodec.DetectLineEnding(text));
        if (!TextFileCodec.CanEncode(text, format))
        {
            throw new FileOperationException("encoding", _localizer["Files.Save.EncodingLoss", format.Encoding].Value);
        }

        var bytes = TextFileCodec.Encode(text, format);
        if (bytes.LongLength > Options.MaxEditBytes)
        {
            throw Error("invalid", "Files.ReadOnly.TooLarge");
        }

        if (info.Exists && !request.Force)
        {
            byte[] current;
            try
            {
                current = await File.ReadAllBytesAsync(full, cancellationToken);
            }
            catch (IOException)
            {
                throw Error("unavailable", "Files.Error.FileBusy");
            }

            if (!string.Equals(TextFileCodec.Hash(current), request.ExpectedHash, StringComparison.Ordinal))
            {
                throw Error("conflict", "Files.Save.Conflict");
            }
        }
        else if (!info.Exists && !request.Force)
        {
            throw Error("deleted", "Files.Save.Deleted");
        }

        await WriteAtomicallyAsync(full, bytes, cancellationToken);
        var saved = new FileInfo(full);
        return new FileSavedView(root.Id, FilePathGuard.Relative(root.Path, full), TextFileCodec.Hash(bytes), saved.Length, saved.LastWriteTimeUtc, format.Encoding, format.Bom, format.LineEnding);
    }

    /// <summary>
    /// Writes next to the file first, then swaps it in, so a crash or a full disk never leaves half a
    /// file. On Windows, File.Replace keeps the original's attributes and permissions.
    /// </summary>
    private static async Task WriteAtomicallyAsync(string full, byte[] bytes, CancellationToken cancellationToken)
    {
        var folder = Path.GetDirectoryName(full)!;
        var temp = Path.Combine(folder, $".~{Path.GetFileName(full)}.{Guid.NewGuid():N}.toolkit-tmp");
        try
        {
            await File.WriteAllBytesAsync(temp, bytes, cancellationToken);
            if (File.Exists(full) && OperatingSystem.IsWindows())
            {
                File.Replace(temp, full, destinationBackupFileName: null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temp, full, overwrite: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A folder that refuses new files (or a volume without replace): write in place instead.
            TryDelete(temp);
            await File.WriteAllBytesAsync(full, bytes, cancellationToken);
        }
        finally
        {
            TryDelete(temp);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Left behind; harmless and hidden (starts with ".~").
        }
    }

    private static string AllowedEncoding(string? name) => name switch
    {
        "utf-8" or "utf-16" or "utf-16BE" => name,
        not null when name.StartsWith("windows-", StringComparison.OrdinalIgnoreCase) || name.StartsWith("iso-8859-", StringComparison.OrdinalIgnoreCase) => name,
        _ => "utf-8"
    };

    public async Task<FileReadView> ReadAsync(int rootId, string? path, CancellationToken cancellationToken = default)
    {
        var (root, full) = await ResolveAsync(rootId, path, cancellationToken);
        var info = new FileInfo(full);
        if (!info.Exists)
        {
            throw Error("notFound", "Files.Error.FileMissing");
        }

        var kind = KindOf(info.Name);
        var relative = FilePathGuard.Relative(root.Path, full);
        FileReadView Details(string actualKind, bool tooLarge = false) => new()
        {
            RootId = root.Id,
            Path = relative,
            Name = info.Name,
            Size = info.Length,
            Modified = info.LastWriteTimeUtc,
            Kind = actualKind,
            TooLarge = tooLarge,
            ReadOnlyReason = _localizer["Files.ReadOnly.NotText"].Value
        };

        if (kind is "image" or "pdf" or "office")
        {
            return Details(kind);
        }

        if (info.Length > Options.MaxViewBytes)
        {
            return Details(kind, tooLarge: true);
        }

        byte[] bytes;
        try
        {
            await using var stream = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 81920, useAsync: true);
            bytes = new byte[stream.Length];
            await stream.ReadExactlyAsync(bytes, cancellationToken);
        }
        catch (UnauthorizedAccessException)
        {
            throw Error("denied", "Files.Error.NoAccess");
        }
        catch (IOException)
        {
            throw Error("unavailable", "Files.Error.FileBusy");
        }

        if (TextFileCodec.LooksBinary(bytes))
        {
            return Details("binary");
        }

        var (text, format) = TextFileCodec.Decode(bytes);
        string? readOnly = null;
        if (info.IsReadOnly)
        {
            readOnly = _localizer["Files.ReadOnly.Attribute"].Value;
        }
        else if (info.Length > Options.MaxEditBytes)
        {
            readOnly = _localizer["Files.ReadOnly.TooLarge"].Value;
        }

        return new FileReadView
        {
            RootId = root.Id,
            Path = relative,
            Name = info.Name,
            Size = info.Length,
            Modified = info.LastWriteTimeUtc,
            Kind = kind,
            Text = text,
            Encoding = format.Encoding,
            Bom = format.Bom,
            LineEnding = format.LineEnding,
            Hash = TextFileCodec.Hash(bytes),
            Editable = readOnly is null,
            ReadOnlyReason = readOnly,
            Plain = info.Length > Options.MaxEditBytes
        };
    }

    public async Task<(string FullPath, string ContentType, string Name)> GetRawAsync(int rootId, string? path, CancellationToken cancellationToken = default)
    {
        var (_, full) = await ResolveAsync(rootId, path, cancellationToken);
        if (!File.Exists(full))
        {
            throw Error("notFound", "Files.Error.FileMissing");
        }

        var name = Path.GetFileName(full);
        return (full, ContentTypes.TryGetContentType(name, out var type) ? type : "application/octet-stream", name);
    }

    public async Task<IReadOnlyList<FileFindItem>> FindByNameAsync(string? query, bool showHidden, CancellationToken cancellationToken = default)
    {
        var text = (query ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return [];
        }

        var roots = await UserRoots().OrderBy(x => x.SortOrder).ThenBy(x => x.Id).AsNoTracking().ToListAsync(cancellationToken);
        var found = new List<(FileFindItem Item, int Rank)>();
        var stopAt = DateTime.UtcNow.AddSeconds(2);
        var options = new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint };
        foreach (var root in roots.Where(r => RootProblem(r.Path) is null))
        {
            var rootPath = FilePathGuard.NormalizeRoot(root.Path);
            var pending = new Stack<DirectoryInfo>();
            pending.Push(new DirectoryInfo(rootPath));
            var seen = 0;
            while (pending.Count > 0 && seen < 100_000 && DateTime.UtcNow < stopAt)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var dir = pending.Pop();
                try
                {
                    foreach (var info in dir.EnumerateFileSystemInfos("*", options))
                    {
                        seen++;
                        if (!showHidden && IsHidden(info))
                        {
                            continue;
                        }

                        if (info is DirectoryInfo sub)
                        {
                            pending.Push(sub);
                            continue;
                        }

                        var rank = Rank(info.Name, text);
                        if (rank >= 0)
                        {
                            found.Add((new FileFindItem(root.Id, root.Name, FilePathGuard.Relative(rootPath, info.FullName), info.Name, KindOf(info.Name)), rank));
                        }
                    }
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                {
                    // Skipped, like Explorer.
                }
            }
        }

        return found
            .OrderBy(x => x.Rank)
            .ThenBy(x => x.Item.Path.Length)
            .ThenBy(x => x.Item.Name, NaturalComparer.Instance)
            .Take(50)
            .Select(x => x.Item)
            .ToList();
    }

    public async Task<string?> SaveCopyAsAsync(int rootId, string? path, CancellationToken cancellationToken = default)
    {
        var (_, full) = await ResolveAsync(rootId, path, cancellationToken);
        if (!File.Exists(full))
        {
            throw Error("notFound", "Files.Error.FileMissing");
        }

        if (!_mode.IsDesktop)
        {
            throw Error("unsupported", "Files.Error.NoWindowsDialog");
        }

        if (!OperatingSystem.IsWindows())
        {
            throw Error("unsupported", "Files.Error.NoWindowsDialog");
        }

        var target = await WindowsShell.PickSaveFileAsync(_localizer["Files.SaveCopy.Title"].Value, Path.GetDirectoryName(full), Path.GetFileName(full));
        if (string.IsNullOrEmpty(target))
        {
            return null;
        }

        if (FilePathGuard.PathComparer.Equals(Path.GetFullPath(target), full))
        {
            return target;
        }

        File.Copy(full, target, overwrite: true);
        return target;
    }

    public async Task OpenOutsideAsync(int rootId, string? path, bool reveal, CancellationToken cancellationToken = default)
    {
        var (_, full) = await ResolveAsync(rootId, path, cancellationToken);
        if (!File.Exists(full) && !Directory.Exists(full))
        {
            throw Error("notFound", "Files.Error.FileMissing");
        }

        if (!_mode.IsDesktop)
        {
            throw Error("unsupported", "Files.Error.DesktopOnly");
        }

        if (!OperatingSystem.IsWindows())
        {
            throw Error("unsupported", "Files.Error.DesktopOnly");
        }

        if (reveal)
        {
            WindowsShell.RevealInExplorer(full);
        }
        else
        {
            WindowsShell.OpenWithDefaultProgram(full);
        }
    }

    /// <summary>0 = same name, 1 = starts with, 2 = contains, 3 = letters in order; -1 = no match.</summary>
    private static int Rank(string name, string query)
    {
        if (name.Equals(query, StringComparison.OrdinalIgnoreCase)) return 0;
        if (name.StartsWith(query, StringComparison.OrdinalIgnoreCase)) return 1;
        if (name.Contains(query, StringComparison.OrdinalIgnoreCase)) return 2;
        var at = 0;
        foreach (var ch in query)
        {
            if (char.IsWhiteSpace(ch)) continue;
            at = name.IndexOf(ch.ToString(), at, StringComparison.OrdinalIgnoreCase);
            if (at < 0) return -1;
            at++;
        }

        return 3;
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>The kind of viewer for a file name: markdown, json, csv, image, pdf, office, text.</summary>
    public static string KindOf(string name)
    {
        var extension = Path.GetExtension(name).TrimStart('.').ToLowerInvariant();
        return extension switch
        {
            "md" or "markdown" or "mdown" or "mkd" => "markdown",
            "json" or "jsonc" or "webmanifest" or "har" => "json",
            "csv" or "tsv" => "csv",
            "png" or "jpg" or "jpeg" or "gif" or "webp" or "bmp" or "ico" or "svg" or "avif" => "image",
            "pdf" => "pdf",
            "doc" or "docx" or "xls" or "xlsx" or "xlsm" or "ppt" or "pptx" or "odt" or "ods" or "odp" or "rtf" or "vsdx" or "mpp" or "msg" => "office",
            "exe" or "dll" or "zip" or "7z" or "rar" or "gz" or "tar" or "iso" or "msi" or "mp3" or "mp4" or "mov" or "avi" or "wav" or "db" or "sqlite" or "bak" or "pfx" or "p12" => "binary",
            _ => "text"
        };
    }

    private IQueryable<FileRoot> UserRoots()
    {
        var userId = _currentUser.UserId;
        return _db.FileRoots.Where(x => x.UserId == userId);
    }

    private async Task<FileRoot> FindRootAsync(int id, CancellationToken cancellationToken) =>
        await UserRoots().FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw Error("notFound", "Files.Error.RootNotFound");

    /// <summary>The root (checked: the user's, still there, still allowed) and the full path inside it.</summary>
    private async Task<(FileRoot Root, string Full)> ResolveAsync(int rootId, string? path, CancellationToken cancellationToken)
    {
        var root = await UserRoots().AsNoTracking().FirstOrDefaultAsync(x => x.Id == rootId, cancellationToken)
            ?? throw Error("notFound", "Files.Error.RootNotFound");
        var problem = RootProblem(root.Path);
        if (problem is not null)
        {
            throw new FileOperationException("unavailable", problem);
        }

        var full = FilePathGuard.Resolve(root.Path, path) ?? throw Error("invalid", "Files.Error.PathInvalid");
        return (root, full);
    }

    private string? RootProblem(string path)
    {
        string full;
        try
        {
            full = FilePathGuard.NormalizeRoot(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return _localizer["Files.Error.FolderInvalid"].Value;
        }

        if (!_access.IsFolderAllowed(full))
        {
            return _localizer["Files.Error.FolderNotAllowed"].Value;
        }

        return Directory.Exists(full) ? null : _localizer["Files.Root.Unavailable"].Value;
    }

    private string CheckFolder(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw Error("invalid", "Files.Error.FolderRequired");
        }

        string full;
        try
        {
            full = FilePathGuard.NormalizeRoot(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw Error("invalid", "Files.Error.FolderInvalid");
        }

        if (!Path.IsPathFullyQualified(full))
        {
            throw Error("invalid", "Files.Error.FolderInvalid");
        }

        if (!_access.IsFolderAllowed(full))
        {
            throw Error("denied", "Files.Error.FolderNotAllowed");
        }

        if (!Directory.Exists(full))
        {
            throw Error("notFound", "Files.Error.FolderMissing");
        }

        try
        {
            using var probe = Directory.EnumerateFileSystemEntries(full).GetEnumerator();
            probe.MoveNext();
        }
        catch (UnauthorizedAccessException)
        {
            throw Error("denied", "Files.Error.NoAccess");
        }

        return full;
    }

    private IReadOnlyList<FolderPickItem> StartingFolders()
    {
        if (_access.RestrictsFolders)
        {
            return _access.AllowedFolders
                .Where(Directory.Exists)
                .Select(x => new FolderPickItem(x, x))
                .ToList();
        }

        var items = new List<FolderPickItem>();
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        foreach (var folder in new[] { documents, home }.Where(x => !string.IsNullOrEmpty(x) && Directory.Exists(x)).Distinct(FilePathGuard.PathComparer))
        {
            items.Add(new FolderPickItem(folder, folder));
        }

        foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType is DriveType.Fixed or DriveType.Removable or DriveType.Network))
        {
            var label = string.IsNullOrWhiteSpace(drive.VolumeLabel) ? drive.Name : $"{drive.Name} ({drive.VolumeLabel})";
            items.Add(new FolderPickItem(label, drive.RootDirectory.FullName));
        }

        return items;
    }

    private FileRootView ToView(FileRoot root) => new(root.Id, root.Name, root.Path, RootProblem(root.Path) is null, RootProblem(root.Path));

    private static FileEntryView ToEntry(string rootPath, FileSystemInfo info, bool hidden)
    {
        var isFolder = info is DirectoryInfo;
        return new FileEntryView(
            info.Name,
            FilePathGuard.Relative(rootPath, info.FullName),
            isFolder,
            info is FileInfo file ? file.Length : 0,
            info.LastWriteTimeUtc,
            isFolder ? "folder" : KindOf(info.Name),
            hidden);
    }

    /// <summary>A link (symbolic link, junction) whose target is outside the root: not listed, and never opened.</summary>
    private static bool LeavesRoot(string rootPath, FileSystemInfo info)
    {
        if (info.LinkTarget is null)
        {
            return false;
        }

        try
        {
            var target = info.ResolveLinkTarget(returnFinalTarget: true);
            return target is null || !FilePathGuard.IsInside(FilePathGuard.NormalizeRoot(rootPath), Path.GetFullPath(target.FullName));
        }
        catch (IOException)
        {
            return true;
        }
    }

    public static bool IsHidden(FileSystemInfo info) =>
        info.Name.StartsWith('.')
        || (info.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0
        || (info is DirectoryInfo && NoiseFolders.Contains(info.Name));

    private static int CompareEntries(FileEntryView a, FileEntryView b)
    {
        if (a.IsFolder != b.IsFolder)
        {
            return a.IsFolder ? -1 : 1;
        }

        return NaturalComparer.Instance.Compare(a.Name, b.Name);
    }

    private static string DefaultName(string full)
    {
        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(full));
        return string.IsNullOrEmpty(name) ? full : name;
    }

    private static string? CleanRootName(string? name)
    {
        var clean = (name ?? string.Empty).Trim();
        return clean.Length == 0 ? null : clean.Length > 120 ? clean[..120] : clean;
    }

    private Task LogAsync(ActivityActionType action, ActivityEntityType entity, int id, string summary) =>
        _activityLog.LogAsync(_currentUser.UserId, action, entity, id.ToString(System.Globalization.CultureInfo.InvariantCulture), summary);

    private FileOperationException Error(string code, string key) => new(code, _localizer[key].Value);
}

/// <summary>Sorts "file2" before "file10", case-insensitively, as Explorer does.</summary>
public sealed class NaturalComparer : IComparer<string>
{
    public static readonly NaturalComparer Instance = new();

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null || y is null)
        {
            return x is null ? -1 : 1;
        }

        int i = 0, j = 0;
        while (i < x.Length && j < y.Length)
        {
            if (char.IsDigit(x[i]) && char.IsDigit(y[j]))
            {
                var startX = i;
                var startY = j;
                while (i < x.Length && char.IsDigit(x[i])) i++;
                while (j < y.Length && char.IsDigit(y[j])) j++;
                var numberX = x[startX..i].TrimStart('0');
                var numberY = y[startY..j].TrimStart('0');
                var byLength = numberX.Length.CompareTo(numberY.Length);
                if (byLength != 0)
                {
                    return byLength;
                }

                var byDigits = string.CompareOrdinal(numberX, numberY);
                if (byDigits != 0)
                {
                    return byDigits;
                }

                continue;
            }

            var byChar = string.Compare(x[i].ToString(), y[j].ToString(), StringComparison.CurrentCultureIgnoreCase);
            if (byChar != 0)
            {
                return byChar;
            }

            i++;
            j++;
        }

        return (x.Length - i).CompareTo(y.Length - j);
    }
}
