namespace ToolboxWeb.Web.ViewModels.Jira;

public sealed class JiraIssue
{
    public string IssueKey { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Parent { get; set; } = string.Empty;
    public string ParentSummary { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Estimate { get; set; } = string.Empty;
    public string TimeToLog { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public DateTime? CreateDate { get; set; }
    public string Type { get; set; } = string.Empty;
    public string LoggedDates { get; set; } = string.Empty;
    public List<WorklogEntry> Worklogs { get; set; } = [];
}
