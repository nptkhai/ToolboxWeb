namespace ToolboxWeb.Web.ViewModels.Jira;

/// <summary>
/// Input for Views/Jira/_JiraPeriodPicker.cshtml. Values use the ISO forms the server reads:
/// 2026-10-21, 2026-W43, 2026-10, 2026.
/// </summary>
public sealed class JiraPeriodPickerViewModel
{
    public static readonly IReadOnlyList<string> AllPeriods = ["day", "week", "month", "year"];

    /// <summary>Prefix for the input ids, unique on the page.</summary>
    public required string IdPrefix { get; init; }

    public IReadOnlyList<string> Periods { get; init; } = AllPeriods;

    public string PeriodType { get; init; } = "week";

    public string DayValue { get; init; } = string.Empty;

    public string WeekValue { get; init; } = string.Empty;

    public string MonthValue { get; init; } = string.Empty;

    public string YearValue { get; init; } = string.Empty;

    public string ValueFor(string period) => period switch
    {
        "day" => DayValue,
        "month" => MonthValue,
        "year" => YearValue,
        _ => WeekValue
    };
}
