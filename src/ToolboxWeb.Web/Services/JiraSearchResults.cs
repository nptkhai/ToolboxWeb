using System.Globalization;
using ToolboxWeb.Web.ViewModels.Jira;

namespace ToolboxWeb.Web.Services;

/// <summary>Turns Jira issues into the rows of the Search tab (stage 5.2).</summary>
public static class JiraSearchResults
{
    /// <summary>Jira's page size in <c>JiraClient.SearchIssuesAsync</c>; more matches are cut off.</summary>
    public const int Limit = 200;

    public static JiraSearchResultViewModel Build(IReadOnlyList<JiraIssue> issues, string jql, DateTime today)
    {
        return new JiraSearchResultViewModel
        {
            Rows = issues.Select(issue => MapRow(issue, today)).ToArray(),
            Jql = jql,
            Limit = Limit,
            Truncated = issues.Count >= Limit
        };
    }

    private static JiraSearchRowViewModel MapRow(JiraIssue issue, DateTime today)
    {
        var isDone = JiraStatus.IsDone(issue.Status);
        var logDays = (issue.Worklogs ?? [])
            .Where(x => x.Started != DateTime.MinValue)
            .GroupBy(JiraDashboardMetrics.WorklogDay)
            .OrderBy(g => g.Key)
            .Select(g => new JiraLogDayViewModel(
                g.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                g.Sum(x => JiraDashboardMetrics.ParseHours(x.TimeSpent))))
            .ToArray();

        return new JiraSearchRowViewModel
        {
            IssueKey = issue.IssueKey ?? string.Empty,
            Summary = issue.Summary ?? string.Empty,
            Status = issue.Status ?? string.Empty,
            StatusTone = JiraStatus.Tone(issue.Status),
            Type = issue.Type ?? string.Empty,
            DueDate = issue.DueDate,
            IsOverdue = !isDone && issue.DueDate.HasValue && issue.DueDate.Value.Date < today,
            DaysOverdue = JiraDashboardMetrics.DaysOverdue(issue.DueDate, isDone, today),
            Parent = issue.Parent ?? string.Empty,
            ParentSummary = issue.ParentSummary ?? string.Empty,
            EstimateHours = JiraDashboardMetrics.ParseHours(issue.Estimate),
            LoggedHours = logDays.Sum(x => x.Hours),
            LogDays = logDays
        };
    }
}
