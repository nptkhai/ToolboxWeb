namespace ToolboxWeb.Web.ViewModels.Jira;

public sealed class JiraDashboardPageViewModel
{
    public JiraSessionSummaryViewModel Session { get; init; } = new();
    public JiraDashboardFilterViewModel Filter { get; init; } = new();
    public IReadOnlyList<JiraDashboardStatViewModel> Stats { get; init; } = [];
    public IReadOnlyList<JiraDashboardRowViewModel> Rows { get; init; } = [];
    public string AppliedJql { get; init; } = string.Empty;
    public string? ErrorMessage { get; init; }
}
