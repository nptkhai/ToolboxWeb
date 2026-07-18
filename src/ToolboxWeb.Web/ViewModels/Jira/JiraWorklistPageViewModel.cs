namespace ToolboxWeb.Web.ViewModels.Jira;

public sealed class JiraWorklistPageViewModel
{
    public JiraSessionSummaryViewModel Session { get; init; } = new();
    public JiraSearchRequest Search { get; init; } = new();
    public IReadOnlyList<JiraIssue> Issues { get; init; } = [];
    public IReadOnlyList<JiraFilter> Filters { get; init; } = [];
    public IReadOnlyList<JiraUser> Users { get; init; } = [];
    public string AppliedJql { get; init; } = string.Empty;
    public string? ErrorMessage { get; init; }
}
