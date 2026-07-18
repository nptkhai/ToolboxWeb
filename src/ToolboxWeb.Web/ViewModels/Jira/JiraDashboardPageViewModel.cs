namespace ToolboxWeb.Web.ViewModels.Jira;

public sealed class JiraDashboardPageViewModel
{
    public JiraSessionSummaryViewModel Session { get; init; } = new();
    public IReadOnlyList<JiraIssue> AssignedIssues { get; init; } = [];
    public IReadOnlyList<JiraIssue> DueSubTasks { get; init; } = [];
}
