namespace ToolboxWeb.Web.Infrastructure.Files;

/// <summary>
/// "Files" section of appsettings for the "Files &amp; folders" screen. The desktop needs none of
/// it (the owner may open any folder of their computer). On the IIS site an account must be
/// listed (<see cref="AllowedUsers"/> or <see cref="AllowedJiraUsers"/>) and the folders must be
/// listed, or nobody gets the screen: the files are the server's.
/// </summary>
public sealed class FilesOptions
{
    /// <summary>IIS site only: user names (emails) of the local accounts allowed to use the screen.</summary>
    public List<string> AllowedUsers { get; set; } = [];

    /// <summary>
    /// IIS site only: Jira accounts allowed to use the screen, each with the Jira address it signs
    /// in to. The address is part of the match because people type it on the sign-in page.
    /// </summary>
    public List<FilesJiraUser> AllowedJiraUsers { get; set; } = [];

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

/// <summary>One entry of <see cref="FilesOptions.AllowedJiraUsers"/>.</summary>
public sealed class FilesJiraUser
{
    /// <summary>The Jira address, as typed on the sign-in page (for example https://jira.example.com).</summary>
    public string? BaseUrl { get; set; }

    /// <summary>The Jira user name (not case-sensitive).</summary>
    public string? Username { get; set; }
}
