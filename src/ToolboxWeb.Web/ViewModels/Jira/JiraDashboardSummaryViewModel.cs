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

    /// <summary>
    /// "Logged against the estimate": only finished subtasks that have an estimate, with every hour
    /// logged on them (not only inside the period), as a percentage of their estimate. Null when
    /// no such subtask is in the list. Subtasks without an estimate are support work, counted apart.
    /// </summary>
    public int? LogPercent { get; init; }

    public int EstimatedDoneCount { get; init; }
    public decimal EstimatedDoneLoggedHours { get; init; }
    public decimal EstimatedDoneEstimateHours { get; init; }

    /// <summary>Hours the user logged in the period on subtasks without an estimate (support work).</summary>
    public decimal SupportHours { get; init; }

    /// <summary>Subtasks without an estimate that have hours logged in the period.</summary>
    public int SupportCount { get; init; }

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
