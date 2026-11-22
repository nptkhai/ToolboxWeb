namespace ToolboxWeb.Web.Domain;

/// <summary>
/// A folder the user opened in "Files &amp; folders" (/files.html): the first node of the tree.
/// Removing it only forgets the folder, never touches the files. The path belongs to one
/// machine, so roots are not part of the data export; deleted with the account.
/// </summary>
public class FileRoot
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;

    /// <summary>The name shown in the tree; the folder name unless the user renamed it.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Full path of the folder on this machine (the desktop, or the IIS server).</summary>
    public string Path { get; set; } = string.Empty;

    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ApplicationUser? User { get; set; }
}
