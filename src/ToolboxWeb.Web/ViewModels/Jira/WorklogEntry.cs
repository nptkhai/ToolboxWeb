namespace ToolboxWeb.Web.ViewModels.Jira;

public sealed class WorklogEntry
{
    /// <summary>
    /// Start of the work in the app time zone (App:TimeZone), with no kind: Jira sends UTC
    /// ("+0000") and the client converts it, so the day a log counts for is simply Started.Date.
    /// </summary>
    public DateTime Started { get; set; }

    public string TimeSpent { get; set; } = string.Empty;

    /// <summary>Time spent in seconds as Jira counts it; preferred over <see cref="TimeSpent"/>.</summary>
    public long? Seconds { get; set; }

    public string AuthorName { get; set; } = string.Empty;
}
