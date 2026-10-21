namespace ToolboxWeb.Web.ViewModels.Jira;

/// <summary>
/// Numbers behind the Jira Dashboard cards (stage 5.1). Raw values only: the page formats them in
/// the UI culture. Previous* are null when the previous period could not be loaded.
/// </summary>
public sealed class JiraDashboardSummaryViewModel
{
    public int SubtaskCount { get; init; }
    public int IssueCount { get; init; }
    public int DoneCount { get; init; }
    public int OpenCount { get; init; }
    public int OverdueCount { get; init; }

    /// <summary>Subtasks still open that have no estimate.</summary>
    public int UnestimatedCount { get; init; }

    public decimal EstimateHours { get; init; }
    public decimal LoggedHours { get; init; }

    /// <summary>Working hours (Monday to Friday, 8 h a day) from the start of the period to today or its end.</summary>
    public decimal ExpectedHours { get; init; }

    /// <summary>Logged as a percentage of the estimate; null without an estimate.</summary>
    public int? LogPercent { get; init; }

    public int ProgressPercent { get; init; }

    public decimal? PreviousLoggedHours { get; init; }
    public int? PreviousDoneCount { get; init; }
    public int? PreviousSubtaskCount { get; init; }
}

/// <summary>One bar of the "hours logged" chart: a day, or a month in the Year view.</summary>
public sealed class JiraDashboardLogBarViewModel
{
    /// <summary>yyyy-MM-dd (day) or yyyy-MM (month).</summary>
    public string Key { get; init; } = string.Empty;
    public decimal Hours { get; init; }

    /// <summary>8 on a working day, 0 on a weekend; working days × 8 for a month.</summary>
    public decimal TargetHours { get; init; }
    public bool IsToday { get; init; }
    public bool IsFuture { get; init; }
}
