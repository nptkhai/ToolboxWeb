namespace ToolboxWeb.Web.ViewModels.Jira;

public sealed class JiraWorklistPageViewModel
{
    public JiraSessionSummaryViewModel Session { get; init; } = new();
    public JiraSearchRequest Search { get; init; } = new();
    public IReadOnlyList<JiraFilter> Filters { get; init; } = [];
    public IReadOnlyList<JiraUser> Users { get; init; } = [];
}
