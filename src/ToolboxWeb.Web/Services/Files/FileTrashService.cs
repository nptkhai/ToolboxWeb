using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ToolboxWeb.Web.Infrastructure.Files;
using ToolboxWeb.Web.Infrastructure.Hosting;

namespace ToolboxWeb.Web.Services.Files;

/// <summary>A file or folder in Toolkit's trash.</summary>
public sealed record FileTrashItem(string Id, int RootId, string RootName, string Path, string Name, bool IsFolder, long Size, DateTime DeletedAt);

/// <summary>
/// Toolkit's own trash for "Files &amp; folders" where the Windows Recycle Bin cannot be used (the
/// IIS site, whose service account has no usable Recycle Bin, or a desktop that is not Windows):
/// <c>{DataRoot}/files-trash/{user}/{id}/</c> holds the item and <c>item.json</c> says where it came
/// from. Items older than <c>Files:TrashDays</c> are purged by <see cref="FileTrashCleanupService"/>.
/// </summary>
public sealed class FileTrashService
{
    private const string MetaName = "item.json";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly AppModeInfo _mode;
    private readonly IOptionsMonitor<FilesOptions> _options;

    public FileTrashService(AppModeInfo mode, IOptionsMonitor<FilesOptions> options)
    {
        _mode = mode;
        _options = options;
    }

    /// <summary>The Windows Recycle Bin is used on a Windows desktop; everywhere else this trash.</summary>
    public bool UsesRecycleBin => _mode.IsDesktop && WindowsShell.IsAvailable;

    private string TrashRoot => Path.Combine(_mode.DataRoot, "files-trash");

    private string UserFolder(string userId) =>
        Path.Combine(TrashRoot, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(userId)))[..16].ToLowerInvariant());

    public async Task MoveToTrashAsync(string userId, int rootId, string rootName, string relativePath, string fullPath, CancellationToken cancellationToken)
    {
        var isFolder = Directory.Exists(fullPath);
        var id = $"{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..31];
        var holder = Path.Combine(UserFolder(userId), id);
        Directory.CreateDirectory(holder);
        var name = Path.GetFileName(fullPath);
        var size = isFolder ? FolderSize(fullPath) : new FileInfo(fullPath).Length;
        var meta = new TrashMeta(rootId, rootName, relativePath, name, isFolder, size, DateTime.UtcNow);
        await File.WriteAllTextAsync(Path.Combine(holder, MetaName), JsonSerializer.Serialize(meta, Json), cancellationToken);
        try
        {
            MoveAcrossVolumes(fullPath, Path.Combine(holder, "item"), isFolder);
        }
        catch
        {
            Directory.Delete(holder, recursive: true);
            throw;
        }
    }

    public IReadOnlyList<FileTrashItem> List(string userId)
    {
        var folder = UserFolder(userId);
        if (!Directory.Exists(folder))
        {
            return [];
        }

        var items = new List<FileTrashItem>();
        foreach (var holder in Directory.EnumerateDirectories(folder))
        {
            var meta = ReadMeta(holder);
            if (meta is not null)
            {
                items.Add(new FileTrashItem(Path.GetFileName(holder), meta.RootId, meta.RootName, meta.Path, meta.Name, meta.IsFolder, meta.Size, meta.DeletedAt));
            }
        }

        return items.OrderByDescending(x => x.DeletedAt).ToList();
    }

    /// <summary>The item's stored copy and where it came from, or null when the id is unknown.</summary>
    public (string ItemPath, FileTrashItem Item)? Find(string userId, string? id)
    {
        if (string.IsNullOrWhiteSpace(id) || !FilePathGuard.IsValidName(id) || id.Length > 40)
        {
            return null;
        }

        var holder = Path.Combine(UserFolder(userId), id);
        var meta = Directory.Exists(holder) ? ReadMeta(holder) : null;
        var itemPath = Path.Combine(holder, "item");
        if (meta is null || (!File.Exists(itemPath) && !Directory.Exists(itemPath)))
        {
            return null;
        }

        return (itemPath, new FileTrashItem(id, meta.RootId, meta.RootName, meta.Path, meta.Name, meta.IsFolder, meta.Size, meta.DeletedAt));
    }

    /// <summary>Moves the stored copy to <paramref name="target"/> and forgets the item.</summary>
    public void RestoreTo(string userId, string id, string target)
    {
        var found = Find(userId, id) ?? throw new FileNotFoundException();
        MoveAcrossVolumes(found.ItemPath, target, found.Item.IsFolder);
        Directory.Delete(Path.GetDirectoryName(found.ItemPath)!, recursive: true);
    }

    public bool Purge(string userId, string id)
    {
        var found = Find(userId, id);
        if (found is null)
        {
            return false;
        }

        Directory.Delete(Path.GetDirectoryName(found.Value.ItemPath)!, recursive: true);
        return true;
    }

    /// <summary>Deletes items older than Files:TrashDays, for every user.</summary>
    public int PurgeOld()
    {
        if (!Directory.Exists(TrashRoot))
        {
            return 0;
        }

        var limit = DateTime.UtcNow.AddDays(-Math.Max(1, _options.CurrentValue.TrashDays));
        var removed = 0;
        foreach (var holder in Directory.EnumerateDirectories(TrashRoot).SelectMany(Directory.EnumerateDirectories))
        {
            var meta = ReadMeta(holder);
            if (meta is null || meta.DeletedAt < limit)
            {
                try
                {
                    Directory.Delete(holder, recursive: true);
                    removed++;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Tried again next time.
                }
            }
        }

        return removed;
    }

    private static TrashMeta? ReadMeta(string holder)
    {
        try
        {
            return JsonSerializer.Deserialize<TrashMeta>(File.ReadAllText(Path.Combine(holder, MetaName)), Json);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static long FolderSize(string folder)
    {
        try
        {
            return new DirectoryInfo(folder)
                .EnumerateFiles("*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint })
                .Sum(x => x.Length);
        }
        catch (IOException)
        {
            return 0;
        }
    }

    /// <summary>Directory.Move / File.Move cannot cross volumes; copy then delete in that case.</summary>
    public static void MoveAcrossVolumes(string source, string target, bool isFolder)
    {
        try
        {
            if (isFolder)
            {
                Directory.Move(source, target);
            }
            else
            {
                File.Move(source, target);
            }
        }
        catch (IOException) when (!string.Equals(Path.GetPathRoot(Path.GetFullPath(source)), Path.GetPathRoot(Path.GetFullPath(target)), StringComparison.OrdinalIgnoreCase))
        {
            if (isFolder)
            {
                CopyFolder(source, target, overwrite: false);
                Directory.Delete(source, recursive: true);
            }
            else
            {
                File.Copy(source, target);
                File.Delete(source);
            }
        }
    }

    public static void CopyFolder(string source, string target, bool overwrite)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.EnumerateFiles(source))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), overwrite);
        }

        foreach (var dir in new DirectoryInfo(source).EnumerateDirectories())
        {
            if ((dir.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                continue;
            }

            CopyFolder(dir.FullName, Path.Combine(target, dir.Name), overwrite);
        }
    }

    private sealed record TrashMeta(int RootId, string RootName, string Path, string Name, bool IsFolder, long Size, DateTime DeletedAt);
}

/// <summary>Purges Toolkit's file trash at start and twice a day.</summary>
public sealed class FileTrashCleanupService : BackgroundService
{
    private readonly FileTrashService _trash;
    private readonly ILogger<FileTrashCleanupService> _logger;

    public FileTrashCleanupService(FileTrashService trash, ILogger<FileTrashCleanupService> logger)
    {
        _trash = trash;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var removed = _trash.PurgeOld();
                if (removed > 0)
                {
                    _logger.LogInformation("Purged {Count} item(s) from the Files trash.", removed);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Could not purge the Files trash.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromHours(12), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
