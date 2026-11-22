namespace ToolboxWeb.Web.Domain;

/// <summary>
/// One catalog (reference data) read from Jira for one user and one Jira server: projects,
/// statuses, users… stored as JSON. A cache the user refreshes with "Sync"
/// (Services/JiraCatalogService.cs); not part of the data export, and deleted with the account.
/// The row of kind "settings" holds the user's own choices (waiting statuses, version projects).
/// </summary>
public class JiraCatalog
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;

    /// <summary>Jira address, lower case without a trailing slash: another server starts empty.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>One of <see cref="Constants.JiraCatalogKinds"/>.</summary>
    public string Kind { get; set; } = string.Empty;

    public string Data { get; set; } = "[]";
    public int ItemCount { get; set; }

    /// <summary>When it was read from Jira (UTC).</summary>
    public DateTime SyncedAt { get; set; } = DateTime.UtcNow;

    public ApplicationUser? User { get; set; }
}
