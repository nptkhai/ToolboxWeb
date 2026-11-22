namespace ToolboxWeb.Web.Infrastructure.Files;

/// <summary>
/// "Files" section of appsettings for the "Files &amp; folders" screen. The desktop needs none of
/// it (the owner may open any folder of their computer). On the IIS site both lists must be
/// filled, or nobody gets the screen: the files are the server's.
/// </summary>
public sealed class FilesOptions
{
    /// <summary>IIS site only: user names (emails) of the local accounts allowed to use the screen.</summary>
    public List<string> AllowedUsers { get; set; } = [];

    /// <summary>IIS site only: folders of the server under which roots may be chosen.</summary>
    public List<string> AllowedFolders { get; set; } = [];

    /// <summary>Days a deleted file stays in Toolkit's trash folder on the IIS site.</summary>
    public int TrashDays { get; set; } = 30;

    /// <summary>Text files up to this size can be edited.</summary>
    public long MaxEditBytes { get; set; } = 5 * 1024 * 1024;

    /// <summary>Text files up to this size are shown (plain, without colours, above <see cref="MaxEditBytes"/>).</summary>
    public long MaxViewBytes { get; set; } = 20 * 1024 * 1024;

    /// <summary>Entries listed per folder; the rest is counted.</summary>
    public int MaxListEntries { get; set; } = 2000;
}
