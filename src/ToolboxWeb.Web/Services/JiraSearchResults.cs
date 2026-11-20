using System.Globalization;
using ToolboxWeb.Web.Infrastructure.Jira;
using ToolboxWeb.Web.ViewModels.Jira;

namespace ToolboxWeb.Web.Services;

/// <summary>Turns Jira issues into the rows of the Search tab (stage 5.2).</summary>
public static class JiraSearchResults
{
    /// <summary>The most issues one search reads (<see cref="JiraClient.MaxIssues"/>); more matches are cut off.</summary>
    public const int Limit = JiraClient.MaxIssues;

    public static JiraSearchResultViewModel Build(IReadOnlyList<JiraIssue> issues, string jql, DateTime today, JiraRules? rules = null)
    {
        var r = rules ?? JiraRules.Default;
        return new JiraSearchResultViewModel
        {
            Rows = issues.Select(issue => MapRow(issue, today, r)).ToArray(),
            Jql = jql,
            Limit = Limit,
            Truncated = issues.Count >= Limit
        };
    }

    private static JiraSearchRowViewModel MapRow(JiraIssue issue, DateTime today, JiraRules rules)
    {
        var logDays = (issue.Worklogs ?? [])
            .Where(x => x.Started != DateTime.MinValue)
            .GroupBy(JiraDashboardMetrics.WorklogDay)
            .OrderBy(g => g.Key)
            .Select(g => new JiraLogDayViewModel(
                g.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                g.Sum(rules.Hours)))
            .ToArray();
        var daysOverdue = rules.DaysOverdue(issue, today);

        return new JiraSearchRowViewModel
        {
            IssueKey = issue.IssueKey ?? string.Empty,
            Summary = issue.Summary ?? string.Empty,
            Status = issue.Status ?? string.Empty,
            StatusTone = rules.Tone(issue),
            Type = issue.Type ?? string.Empty,
            SubTaskType = issue.SubTaskType ?? string.Empty,
            IsWaiting = rules.IsWaiting(issue.Status),
            DueDate = issue.DueDate,
            IsOverdue = daysOverdue > 0,
            DaysOverdue = daysOverdue,
            Parent = issue.Parent ?? string.Empty,
            ParentSummary = issue.ParentSummary ?? string.Empty,
            EstimateHours = rules.EstimateHours(issue),
            LoggedHours = logDays.Sum(x => x.Hours),
            LogDays = logDays
        };
    }
}
