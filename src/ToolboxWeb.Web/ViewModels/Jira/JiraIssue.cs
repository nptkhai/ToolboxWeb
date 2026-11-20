namespace ToolboxWeb.Web.ViewModels.Jira;

public sealed class JiraIssue
{
    public string IssueKey { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Parent { get; set; } = string.Empty;
    public string ParentSummary { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;

    /// <summary>Jira's group of the status: "new", "indeterminate" or "done" (empty when unknown).</summary>
    public string StatusCategory { get; set; } = string.Empty;

    /// <summary>Jira's display text of the original estimate ("0.25d", "1.5h"); kept for fallbacks.</summary>
    public string Estimate { get; set; } = string.Empty;

    /// <summary>Original estimate in seconds as Jira counts it; preferred over <see cref="Estimate"/>.</summary>
    public long? EstimateSeconds { get; set; }

    public string TimeToLog { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }

    /// <summary>Creation time in the app time zone.</summary>
    public DateTime? CreateDate { get; set; }

    public string Type { get; set; } = string.Empty;

    /// <summary>The "Sub-task Type" custom field (Developing, Meeting, Annual Leave…), trimmed.</summary>
    public string SubTaskType { get; set; } = string.Empty;

    public string LoggedDates { get; set; } = string.Empty;
    public List<WorklogEntry> Worklogs { get; set; } = [];
}
