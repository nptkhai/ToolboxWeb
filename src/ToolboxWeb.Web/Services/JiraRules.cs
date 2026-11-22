using ToolboxWeb.Web.ViewModels.Jira;

namespace ToolboxWeb.Web.Services;

/// <summary>
/// How the Jira screens read an issue: its tone (from Jira's status category), whether it is
/// late, and how many hours its estimate and worklogs are. One instance per request, built by
/// <see cref="IJiraCatalogService.GetRulesAsync"/> from the user's settings and Jira's time
/// tracking configuration.
/// </summary>
public sealed class JiraRules
{
    /// <summary>
    /// Statuses that wait on someone else (owner decision 2026-11-20): they count as in progress
    /// but never as late. Names, not ids: some of them only exist in projects the user cannot see.
    /// </summary>
    public static readonly IReadOnlyList<string> DefaultWaitingStatuses =
    [
        "Pending",
        "Waiting for support",
        "Waiting for customer",
        "Waiting for approval",
        "Escalated",
        "In Following Up",
        "Work in progress"
    ];

    public const decimal DefaultHoursPerDay = 8m;

    public static JiraRules Default { get; } = new(DefaultHoursPerDay, DefaultWaitingStatuses);

    private readonly HashSet<string> _waiting;

    public JiraRules(decimal hoursPerDay, IEnumerable<string>? waitingStatuses)
    {
        HoursPerDay = hoursPerDay > 0 ? hoursPerDay : DefaultHoursPerDay;
        _waiting = new HashSet<string>(
            (waitingStatuses ?? []).Select(x => x?.Trim() ?? string.Empty).Where(x => x.Length > 0),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Working hours in a Jira day ("1d"), from Jira's time tracking configuration.</summary>
    public decimal HoursPerDay { get; }

    public IReadOnlyCollection<string> WaitingStatuses => _waiting;

    public string Tone(JiraIssue issue) => JiraStatus.Tone(issue.Status, issue.StatusCategory);

    public bool IsDone(JiraIssue issue) => Tone(issue) == JiraStatus.Done;

    public bool IsWaiting(string? status) => !string.IsNullOrWhiteSpace(status) && _waiting.Contains(status.Trim());

    /// <summary>Days past the due date; 0 when done, waiting on someone, or not due yet.</summary>
    public int DaysOverdue(JiraIssue issue, DateTime today)
    {
        return IsDone(issue) || IsWaiting(issue.Status)
            ? 0
            : JiraDashboardMetrics.DaysOverdue(issue.DueDate, isDone: false, today);
    }

    public decimal EstimateHours(JiraIssue issue)
    {
        return issue.EstimateSeconds is > 0
            ? issue.EstimateSeconds.Value / 3600m
            : JiraDashboardMetrics.ParseHours(issue.Estimate, HoursPerDay);
    }

    public decimal Hours(WorklogEntry worklog)
    {
        return worklog.Seconds is > 0
            ? worklog.Seconds.Value / 3600m
            : JiraDashboardMetrics.ParseHours(worklog.TimeSpent, HoursPerDay);
    }
}
