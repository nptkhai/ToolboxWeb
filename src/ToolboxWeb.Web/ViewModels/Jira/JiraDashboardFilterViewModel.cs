namespace ToolboxWeb.Web.ViewModels.Jira;

public sealed class JiraDashboardFilterViewModel
{
    public string PeriodType { get; init; } = "week";
    public string PeriodValue { get; init; } = string.Empty;
    public string DayValue { get; init; } = string.Empty;
    public string WeekValue { get; init; } = string.Empty;
    public string MonthValue { get; init; } = string.Empty;
    public string YearValue { get; init; } = string.Empty;
    public string RangeLabel { get; init; } = string.Empty;
    public string Sort { get; init; } = "dueDate";
    public string Direction { get; init; } = "asc";
}
