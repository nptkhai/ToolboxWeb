namespace ToolboxWeb.Web.ViewModels.Jira;

public sealed class JiraDashboardQueryViewModel
{
    public string? PeriodType { get; init; }
    public string? PeriodValue { get; init; }
    public string? Sort { get; init; }
    public string? Dir { get; init; }
}
