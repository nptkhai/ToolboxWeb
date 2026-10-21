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
    public const decimal HoursPerWorkingDay = 8m;

    /// <summary>
    /// The day a worklog counts for, in the app time zone. Jira sends a time with an offset, which
    /// the client parses into server local time; fake data has no kind and is already app time.
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

    /// <summary>Jira time text ("1h 30m", "2d", "1,5h") in hours; a day is 8 h and a week 40 h.</summary>
    public static decimal ParseHours(string? value)
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
                "w" => amount * 5 * HoursPerWorkingDay,
                "d" => amount * HoursPerWorkingDay,
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
    public static decimal ExpectedHours(DateTime rangeStart, DateTime rangeEnd, DateTime today)
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

        return days * HoursPerWorkingDay;
    }

    public static JiraDashboardSummaryViewModel BuildSummary(
        IReadOnlyList<JiraDashboardRowViewModel> rows,
        DateTime rangeStart,
        DateTime rangeEnd,
        DateTime today,
        IReadOnlyList<JiraDashboardRowViewModel>? previousRows)
    {
        var done = rows.Count(x => JiraStatus.IsDone(x.Status));
        var estimate = rows.Sum(x => x.EstimateHours);
        var logged = rows.Sum(x => x.LoggedHours);

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
            UnestimatedCount = rows.Count(x => x.EstimateHours <= 0 && !JiraStatus.IsDone(x.Status)),
            EstimateHours = estimate,
            LoggedHours = logged,
            ExpectedHours = ExpectedHours(rangeStart, rangeEnd, today),
            LogPercent = estimate > 0 ? (int)Math.Round(logged / estimate * 100m, MidpointRounding.AwayFromZero) : null,
            ProgressPercent = rows.Count > 0 ? (int)Math.Round(done * 100m / rows.Count, MidpointRounding.AwayFromZero) : 0,
            PreviousLoggedHours = previousRows?.Sum(x => x.LoggedHours),
            PreviousDoneCount = previousRows?.Count(x => JiraStatus.IsDone(x.Status)),
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
        string currentUsername)
    {
        var hoursByDay = issues
            .SelectMany(issue => OwnWorklogs(issue.Worklogs, rangeStart, rangeEnd, currentUsername))
            .GroupBy(WorklogDay)
            .ToDictionary(g => g.Key, g => g.Sum(x => ParseHours(x.TimeSpent)));

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
                    TargetHours = Enumerable.Range(0, monthEnd.Day).Count(d => IsWorkingDay(month.AddDays(d))) * HoursPerWorkingDay,
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
                TargetHours = IsWorkingDay(day) ? HoursPerWorkingDay : 0,
                IsToday = day == today,
                IsFuture = day > today
            });
        }

        return bars;
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
