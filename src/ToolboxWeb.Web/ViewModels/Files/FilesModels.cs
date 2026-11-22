namespace ToolboxWeb.Web.ViewModels.Files;

/// <summary>A root as the page shows it. <paramref name="Problem"/> says why it cannot be opened (moved, unplugged…).</summary>
public sealed record FileRootView(int Id, string Name, string Path, bool Available, string? Problem);

/// <summary>One file or folder in a listing. <paramref name="Path"/> is relative to the root, with "/".</summary>
public sealed record FileEntryView(string Name, string Path, bool IsFolder, long Size, DateTime Modified, string Kind, bool Hidden);

/// <param name="Total">Entries in the folder (shown or not); <paramref name="Entries"/> stops at the list limit.</param>
public sealed record FileListView(int RootId, string Path, IReadOnlyList<FileEntryView> Entries, int Total, bool Truncated);

/// <param name="Partial">True when counting stopped early (a very large root).</param>
public sealed record FileStatsView(int Files, int Folders, long Bytes, bool Partial);

/// <summary>A folder in the folder picker drawn in the page (IIS site, or a desktop without the Windows dialog).</summary>
public sealed record FolderPickItem(string Name, string Path);

/// <param name="Path">The folder listed; null for the starting list (drives, or the allowed folders).</param>
/// <param name="Parent">The folder one level up, or null at the top.</param>
public sealed record FolderPickView(string? Path, string? Parent, IReadOnlyList<FolderPickItem> Folders);

/// <summary>A file opened in a tab.</summary>
public sealed class FileReadView
{
    public int RootId { get; init; }
    public string Path { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public long Size { get; init; }
    public DateTime Modified { get; init; }

    /// <summary>markdown, json, csv, image, pdf, office, text, binary.</summary>
    public string Kind { get; init; } = "text";

    /// <summary>The text, for text kinds that are not too large; null otherwise.</summary>
    public string? Text { get; init; }

    public string Encoding { get; init; } = "utf-8";
    public bool Bom { get; init; }
    public string LineEnding { get; init; } = "LF";

    /// <summary>Content hash when read; sent back on save to notice a change made elsewhere.</summary>
    public string Hash { get; init; } = string.Empty;

    /// <summary>Whether the file may be edited here.</summary>
    public bool Editable { get; init; }

    /// <summary>Why it is read-only, or null.</summary>
    public string? ReadOnlyReason { get; init; }

    /// <summary>Too large to show as text: only the file's details and "open with" / download.</summary>
    public bool TooLarge { get; init; }

    /// <summary>Shown as plain text (no colours, no rendering) because it is large.</summary>
    public bool Plain { get; init; }
}

/// <summary>Size and last write time (UTC) of an open file, polled to notice outside changes.</summary>
public sealed record FileInfoView(long Size, DateTime Modified, bool ReadOnly);

/// <summary>Saving a text file: the text and how the file is stored (as read), plus the hash it was read with.</summary>
public sealed class FileSaveRequest
{
    public int Root { get; set; }
    public string? Path { get; set; }
    public string? Text { get; set; }
    public string? ExpectedHash { get; set; }
    public string? Encoding { get; set; }
    public bool Bom { get; set; }
    public string? LineEnding { get; set; }

    /// <summary>Overwrite even if the file changed (or was deleted) since it was read.</summary>
    public bool Force { get; set; }
}

public sealed record FileSavedView(int RootId, string Path, string Hash, long Size, DateTime Modified, string Encoding, bool Bom, string LineEnding);

/// <summary>A quick-open match: a file in one of the user's roots.</summary>
public sealed record FileFindItem(int RootId, string RootName, string Path, string Name, string Kind);

/// <summary>A file in a root, for actions that name one.</summary>
public sealed record FileRefRequest(int Root, string? Path);

public sealed record FileCreateRequest(int Root, string? Folder, string? Name, bool IsFolder);
public sealed record FileRenameRequest(int Root, string? Path, string? Name);
public sealed record FileMoveRequest(int Root, string? Path, int TargetRoot, string? TargetFolder, bool Copy, bool Overwrite);
public sealed record FileTrashRequest(string? Id);

public sealed record AddFileRootRequest(string? Path, string? Name);
public sealed record RenameFileRootRequest(int Id, string? Name);
public sealed record FileRootIdRequest(int Id);
public sealed record RelinkFileRootRequest(int Id, string? Path);
public sealed record MoveFileRootRequest(int Id, int Offset);

/// <summary>Page settings for Views/Files/Index.cshtml.</summary>
public sealed class FilesPageViewModel
{
    /// <summary>localStorage key for open tabs, expanded folders and view choices (per user).</summary>
    public string StateStorageKey { get; init; } = string.Empty;

    public bool IsDesktop { get; init; }

    /// <summary>The Windows folder and "Save as" dialogs are available (desktop on Windows).</summary>
    public bool HasWindowsDialog { get; init; }

    /// <summary>Deleted files go to Toolkit's trash (IIS site, non-Windows desktop) instead of the Windows Recycle Bin.</summary>
    public bool UsesToolkitTrash { get; init; }
}
