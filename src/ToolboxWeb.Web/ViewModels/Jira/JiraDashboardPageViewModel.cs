namespace ToolboxWeb.Web.ViewModels.Jira;

public sealed class JiraDashboardPageViewModel
{
    public JiraSessionSummaryViewModel Session { get; init; } = new();
    public JiraDashboardFilterViewModel Filter { get; init; } = new();
    public JiraDashboardSummaryViewModel Summary { get; init; } = new();

    /// <summary>"day" bars for Day/Week/Month, "month" bars for Year.</summary>
    public string LogBarUnit { get; init; } = "day";
    public IReadOnlyList<JiraDashboardLogBarViewModel> LogBars { get; init; } = [];
    public IReadOnlyList<JiraDashboardRowViewModel> Rows { get; init; } = [];
    public string AppliedJql { get; init; } = string.Empty;
    public string? ErrorMessage { get; init; }
}
