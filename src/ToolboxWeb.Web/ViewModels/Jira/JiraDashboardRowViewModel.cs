namespace ToolboxWeb.Web.ViewModels.Jira;

public sealed class JiraDashboardRowViewModel
{
    public int Index { get; init; }
    public string Project { get; init; } = string.Empty;
    public string SubTaskKey { get; init; } = string.Empty;
    public string SubTaskSummary { get; init; } = string.Empty;
    public string IssueKey { get; init; } = string.Empty;
    public string IssueSummary { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string StatusTone { get; init; } = "todo";
    public DateTime? DueDate { get; init; }
    public DateTime? CreateDate { get; init; }
    public string DueDateText { get; init; } = "-";
    public string EstimateTimeText { get; init; } = "-";
    public string LoggedTimeText { get; init; } = "-";
    public decimal EstimateHours { get; init; }
    public decimal LoggedHours { get; init; }

    /// <summary>Every hour logged on the subtask, by anyone and at any time: what the estimate is compared with.</summary>
    public decimal TotalLoggedHours { get; init; }
    public bool IsOverdue { get; init; }

    /// <summary>Days past the due date while still open; 0 when not overdue.</summary>
    public int DaysOverdue { get; init; }
}
