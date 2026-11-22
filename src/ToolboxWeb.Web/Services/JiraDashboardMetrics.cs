using System.Globalization;
using System.Text.RegularExpressions;
using ToolboxWeb.Web.Infrastructure.Time;
using ToolboxWeb.Web.ViewModels.Jira;

namespace ToolboxWeb.Web.Services;

/// <summary>
/// Figures for the Jira Dashboard (stage 5.1): the summary cards and the hours-logged chart.
/// Pure functions over the rows and worklogs the controller already loaded.
/// </summary>
public static partial class JiraDashboardMetrics
{
    /// <summary>Default working day; the real value comes from Jira (<see cref="JiraRules.HoursPerDay"/>).</summary>
    public const decimal HoursPerWorkingDay = JiraRules.DefaultHoursPerDay;

    /// <summary>
    /// The day a worklog counts for, in the app time zone. The client already converts Jira's UTC
    /// times to the app time zone (no kind); a value with a kind is converted here.
    /// </summary>
    public static DateTime WorklogDay(WorklogEntry worklog)
    {
        return worklog.Started.Kind == DateTimeKind.Unspecified
            ? worklog.Started.Date
            : worklog.Started.ToAppTime().Date;
    }

    /// <summary>Worklogs by the signed-in user (or without an author) inside the range.</summary>
    public static IEnumerable<WorklogEntry> OwnWorklogs(
        IEnumerable<WorklogEntry>? worklogs,
        DateTime rangeStart,
        DateTime rangeEnd,
        string currentUsername)
    {
        var user = currentUsername?.Trim() ?? string.Empty;
        return (worklogs ?? [])
            .Where(x => x.Started != DateTime.MinValue)
            .Where(x =>
            {
                var day = WorklogDay(x);
                return day >= rangeStart.Date && day <= rangeEnd.Date;
            })
            .Where(x => string.IsNullOrWhiteSpace(user)
                || string.IsNullOrWhiteSpace(x.AuthorName)
                || string.Equals(x.AuthorName.Trim(), user, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Hours the signed-in user logged on these issues inside the range.</summary>
    public static decimal LoggedHours(IEnumerable<JiraIssue> issues, DateTime rangeStart, DateTime rangeEnd, string currentUsername, JiraRules? rules = null)
    {
        var r = rules ?? JiraRules.Default;
        return issues
            .SelectMany(issue => OwnWorklogs(issue.Worklogs, rangeStart, rangeEnd, currentUsername))
            .Sum(r.Hours);
    }

    /// <summary>
    /// Jira time text ("1h 30m", "0.25d", "1,5h") in hours. Only a fallback: Jira also sends the
    /// value in seconds, which <see cref="JiraRules"/> prefers. A week is five days.
    /// </summary>
    public static decimal ParseHours(string? value, decimal hoursPerDay = HoursPerWorkingDay)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        var total = 0m;
        foreach (Match match in JiraDuration().Matches(value))
        {
            if (!decimal.TryParse(match.Groups["amount"].Value.Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount))
            {
                continue;
            }

            total += match.Groups["unit"].Value.ToLowerInvariant() switch
            {
                "w" => amount * 5 * hoursPerDay,
                "d" => amount * hoursPerDay,
                "h" => amount,
                "m" => amount / 60m,
                _ => 0m
            };
        }

        return total;
    }

    public static int DaysOverdue(DateTime? dueDate, bool isDone, DateTime today)
    {
        return !isDone && dueDate.HasValue && dueDate.Value.Date < today
            ? (today - dueDate.Value.Date).Days
            : 0;
    }

    /// <summary>
    /// Working hours expected so far in the period: Monday to Friday, 8 h a day, from the start of
    /// the period to today (or the end, if the period is over). Public holidays are not known.
    /// </summary>
    public static decimal ExpectedHours(DateTime rangeStart, DateTime rangeEnd, DateTime today, decimal hoursPerDay = HoursPerWorkingDay)
    {
        var last = rangeEnd.Date < today ? rangeEnd.Date : today;
        var days = 0;
        for (var day = rangeStart.Date; day <= last; day = day.AddDays(1))
        {
            if (IsWorkingDay(day))
            {
                days++;
            }
        }

        return days * hoursPerDay;
    }

    public static JiraDashboardSummaryViewModel BuildSummary(
        IReadOnlyList<JiraDashboardRowViewModel> rows,
        DateTime rangeStart,
        DateTime rangeEnd,
        DateTime today,
        IReadOnlyList<JiraDashboardRowViewModel>? previousRows,
        decimal hoursPerDay = HoursPerWorkingDay)
    {
        static bool IsDone(JiraDashboardRowViewModel row) => row.StatusTone == JiraStatus.Done;
        var done = rows.Count(IsDone);
        var estimate = rows.Sum(x => x.EstimateHours);
        var logged = rows.Sum(x => x.LoggedHours);

        // The estimate is for the whole subtask, so it is compared with every hour logged on it,
        // and only once the subtask is finished. Subtasks without an estimate are support work.
        var estimatedDone = rows.Where(x => x.EstimateHours > 0 && IsDone(x)).ToArray();
        var doneEstimate = estimatedDone.Sum(x => x.EstimateHours);
        var doneLogged = estimatedDone.Sum(x => x.TotalLoggedHours);
        var support = rows.Where(x => x.EstimateHours <= 0 && x.LoggedHours > 0).ToArray();

        return new JiraDashboardSummaryViewModel
        {
            SubtaskCount = rows.Count,
            IssueCount = rows
                .Select(x => string.IsNullOrWhiteSpace(x.IssueKey) ? x.SubTaskKey : x.IssueKey)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count(),
            DoneCount = done,
            OpenCount = rows.Count - done,
            OverdueCount = rows.Count(x => x.IsOverdue),
            UnestimatedCount = rows.Count(x => x.EstimateHours <= 0 && !IsDone(x)),
            EstimateHours = estimate,
            LoggedHours = logged,
            ExpectedHours = ExpectedHours(rangeStart, rangeEnd, today, hoursPerDay),
            LogPercent = doneEstimate > 0 ? (int)Math.Round(doneLogged / doneEstimate * 100m, MidpointRounding.AwayFromZero) : null,
            EstimatedDoneCount = estimatedDone.Length,
            EstimatedDoneLoggedHours = doneLogged,
            EstimatedDoneEstimateHours = doneEstimate,
            SupportHours = support.Sum(x => x.LoggedHours),
            SupportCount = support.Length,
            ProgressPercent = rows.Count > 0 ? (int)Math.Round(done * 100m / rows.Count, MidpointRounding.AwayFromZero) : 0,
            PreviousLoggedHours = previousRows?.Sum(x => x.LoggedHours),
            PreviousDoneCount = previousRows?.Count(IsDone),
            PreviousSubtaskCount = previousRows?.Count
        };
    }

    /// <summary>
    /// Hours the user logged per day of the period (per month in the Year view), across every
    /// loaded issue, with the 8 h target of each working day.
    /// </summary>
    public static IReadOnlyList<JiraDashboardLogBarViewModel> BuildLogBars(
        IReadOnlyList<JiraIssue> issues,
        string periodType,
        DateTime rangeStart,
        DateTime rangeEnd,
        DateTime today,
        string currentUsername,
        JiraRules? rules = null)
    {
        var r = rules ?? JiraRules.Default;
        var hoursByDay = issues
            .SelectMany(issue => OwnWorklogs(issue.Worklogs, rangeStart, rangeEnd, currentUsername))
            .GroupBy(WorklogDay)
            .ToDictionary(g => g.Key, g => g.Sum(r.Hours));

        if (periodType == "year")
        {
            return Enumerable.Range(0, 12).Select(offset =>
            {
                var month = new DateTime(rangeStart.Year, 1, 1).AddMonths(offset);
                var monthEnd = month.AddMonths(1).AddDays(-1);
                return new JiraDashboardLogBarViewModel
                {
                    Key = month.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                    Hours = hoursByDay.Where(x => x.Key >= month && x.Key <= monthEnd).Sum(x => x.Value),
                    TargetHours = Enumerable.Range(0, monthEnd.Day).Count(d => IsWorkingDay(month.AddDays(d))) * r.HoursPerDay,
                    IsToday = today >= month && today <= monthEnd,
                    IsFuture = month > today
                };
            }).ToArray();
        }

        var bars = new List<JiraDashboardLogBarViewModel>();
        for (var day = rangeStart.Date; day <= rangeEnd.Date; day = day.AddDays(1))
        {
            bars.Add(new JiraDashboardLogBarViewModel
            {
                Key = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Hours = hoursByDay.GetValueOrDefault(day),
                TargetHours = IsWorkingDay(day) ? r.HoursPerDay : 0,
                IsToday = day == today,
                IsFuture = day > today
            });
        }

        return bars;
    }

    /// <summary>
    /// Hours the user logged in the period per Sub-task Type (Developing, Meeting, Annual Leave…),
    /// most first; subtasks without a type come last.
    /// </summary>
    public static IReadOnlyList<JiraHoursByTypeViewModel> HoursByType(IReadOnlyList<JiraDashboardRowViewModel> rows)
    {
        return rows
            .Where(x => x.LoggedHours > 0)
            .GroupBy(x => x.SubTaskType?.Trim() ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Select(g => new JiraHoursByTypeViewModel(g.Key, g.Sum(x => x.LoggedHours), g.Count()))
            .OrderBy(x => x.Type.Length == 0)
            .ThenByDescending(x => x.Hours)
            .ToArray();
    }

    public static bool IsWorkingDay(DateTime day) => day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);

    /// <summary>The period of the same length just before this one: last week, last month, last year.</summary>
    public static (DateTime Start, DateTime End) PreviousRange(string periodType, DateTime rangeStart)
    {
        return periodType switch
        {
            "day" => (rangeStart.AddDays(-1), rangeStart.AddDays(-1)),
            "month" => (rangeStart.AddMonths(-1), rangeStart.AddDays(-1)),
            "year" => (rangeStart.AddYears(-1), rangeStart.AddDays(-1)),
            _ => (rangeStart.AddDays(-7), rangeStart.AddDays(-1))
        };
    }

    [GeneratedRegex(@"(?<amount>\d+(?:[.,]\d+)?)\s*(?<unit>[wdhm])", RegexOptions.IgnoreCase)]
    private static partial Regex JiraDuration();
}
