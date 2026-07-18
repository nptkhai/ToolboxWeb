using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using ToolboxWeb.Web.Constants;
using ToolboxWeb.Web.Infrastructure.Jira;
using ToolboxWeb.Web.Services;
using ToolboxWeb.Web.ViewModels.Jira;

namespace ToolboxWeb.Web.Controllers;

[Authorize(Policy = "JiraOnly")]
public class JiraController : Controller
{
    private static readonly string[] DoneStatusHints =
    [
        "done",
        "closed",
        "resolved",
        "complete",
        "completed",
        "finished",
        "deployed"
    ];

    private static readonly string[] DoingStatusHints =
    [
        "progress",
        "analysis",
        "review",
        "doing",
        "testing",
        "qa",
        "verify"
    ];

    private readonly IJiraAuthService _jiraAuth;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public JiraController(IJiraAuthService jiraAuth, IStringLocalizer<SharedResource> localizer)
    {
        _jiraAuth = jiraAuth;
        _localizer = localizer;
    }

    [HttpGet("/" + ToolboxRouteSlugs.Pages.JiraDashboard)]
    [HttpGet("/" + ToolboxRouteSlugs.HtmlPages.JiraDashboard, Name = ToolboxRouteSlugs.RouteNames.JiraDashboardHtml)]
    public async Task<IActionResult> DashboardPage([FromQuery] JiraDashboardQueryViewModel query)
    {
        var session = await GetSessionOrRedirectAsync();
        if (session is null)
        {
            return RedirectToLogin();
        }

        return View("Dashboard", await BuildDashboardPageModelAsync(session, query));
    }

    [HttpGet("/" + ToolboxRouteSlugs.Pages.JiraWorklist)]
    [HttpGet("/" + ToolboxRouteSlugs.HtmlPages.JiraWorklist, Name = ToolboxRouteSlugs.RouteNames.JiraWorklistHtml)]
    public async Task<IActionResult> WorklistPage()
    {
        var session = await GetSessionOrRedirectAsync();
        if (session is null)
        {
            return RedirectToLogin();
        }

        return View("Worklist", await BuildWorklistPageModelAsync(session, new JiraSearchRequest
        {
            Assignee = "currentUser()",
            FromDate = DateTime.Today,
            ToDate = DateTime.Today
        }));
    }

    [HttpPost("/" + ToolboxRouteSlugs.HtmlPages.JiraWorklist)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> WorklistPage(JiraSearchRequest search)
    {
        var session = await GetSessionOrRedirectAsync();
        if (session is null)
        {
            return RedirectToLogin();
        }

        return View("Worklist", await BuildWorklistPageModelAsync(session, search, runSearch: true));
    }

    [HttpGet]
    public async Task<IActionResult> Status()
    {
        var session = await GetSessionOrSignOutAsync();
        if (session is null)
        {
            return Json(JiraApiResponse<object>.LoginRequired(_localizer["Jira.SessionExpired"].Value));
        }

        return Json(JiraApiResponse<object>.Ok(BuildSessionSummary(session)));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _jiraAuth.SignOutAsync(HttpContext);
        return Json(JiraApiResponse<object>.Ok(new { }));
    }

    [HttpGet]
    public async Task<IActionResult> Dashboard([FromQuery] JiraDashboardQueryViewModel query)
    {
        var session = await GetSessionOrSignOutAsync();
        if (session is null)
        {
            return Json(JiraApiResponse<object>.LoginRequired(_localizer["Jira.SessionExpired"].Value));
        }

        var model = await BuildDashboardPageModelAsync(session, query);
        return Json(JiraApiResponse<object>.Ok(new
        {
            model.Filter,
            model.Stats,
            model.Rows,
            model.AppliedJql,
            model.ErrorMessage
        }));
    }

    [HttpPost]
    public async Task<IActionResult> Search([FromBody] JiraSearchRequest request)
    {
        var session = await GetSessionOrSignOutAsync();
        if (session is null)
        {
            return Json(JiraApiResponse<object>.LoginRequired(_localizer["Jira.SessionExpired"].Value));
        }

        try
        {
            var jql = JiraQueryBuilder.Build(request, _localizer["Jira.Search.AssigneeRequired"].Value);
            var issues = await session.Client.SearchIssuesAsync(jql);
            return Json(JiraApiResponse<object>.Ok(new
            {
                issues,
                jql
            }));
        }
        catch (Exception ex)
        {
            return Json(JiraApiResponse<object>.Fail(JiraErrorHelper.ToUserMessage(
                ex,
                _localizer["Jira.SearchFailed"].Value,
                _localizer["Jira.SessionExpired"].Value,
                _localizer["Jira.NotFound"].Value,
                _localizer["Jira.InvalidRequest"].Value)));
        }
    }

    [HttpGet]
    public async Task<IActionResult> Filters()
    {
        var session = await GetSessionOrSignOutAsync();
        if (session is null)
        {
            return Json(JiraApiResponse<object>.LoginRequired(_localizer["Jira.SessionExpired"].Value));
        }

        try
        {
            return Json(JiraApiResponse<object>.Ok(await session.Client.GetFavouriteFiltersAsync()));
        }
        catch (Exception ex)
        {
            return Json(JiraApiResponse<object>.Fail(JiraErrorHelper.ToUserMessage(
                ex,
                _localizer["Jira.FiltersFailed"].Value,
                _localizer["Jira.SessionExpired"].Value,
                _localizer["Jira.NotFound"].Value,
                _localizer["Jira.InvalidRequest"].Value)));
        }
    }

    [HttpGet]
    public async Task<IActionResult> Users()
    {
        var session = await GetSessionOrSignOutAsync();
        if (session is null)
        {
            return Json(JiraApiResponse<object>.LoginRequired(_localizer["Jira.SessionExpired"].Value));
        }

        try
        {
            return Json(JiraApiResponse<object>.Ok(await session.Client.GetUsersAsync()));
        }
        catch (Exception ex)
        {
            return Json(JiraApiResponse<object>.Fail(JiraErrorHelper.ToUserMessage(
                ex,
                _localizer["Jira.UsersFailed"].Value,
                _localizer["Jira.SessionExpired"].Value,
                _localizer["Jira.NotFound"].Value,
                _localizer["Jira.InvalidRequest"].Value)));
        }
    }

    private async Task<JiraDashboardPageViewModel> BuildDashboardPageModelAsync(JiraSession session, JiraDashboardQueryViewModel? query)
    {
        var periodType = NormalizePeriodType(query?.PeriodType);
        var sort = NormalizeSort(query?.Sort);
        var direction = NormalizeDirection(query?.Dir);
        var (rangeStart, rangeEnd, periodValue) = ResolveRange(periodType, query?.PeriodValue);

        string? errorMessage = null;
        IReadOnlyList<JiraIssue> issues = [];

        var appliedJql = BuildDashboardJql(rangeStart, rangeEnd);

        try
        {
            issues = await session.Client.SearchIssuesAsync(appliedJql, includeWorklogs: true);
        }
        catch (Exception ex)
        {
            errorMessage = JiraErrorHelper.ToUserMessage(
                ex,
                _localizer["Jira.Dashboard.Error"].Value,
                _localizer["Jira.SessionExpired"].Value,
                _localizer["Jira.NotFound"].Value,
                _localizer["Jira.InvalidRequest"].Value);
        }

        var mappedRows = MapDashboardRows(issues, rangeStart, rangeEnd, session.Username);
        var sortedRows = SortDashboardRows(mappedRows, sort, direction)
            .Select((row, index) => CopyRowWithIndex(row, index + 1))
            .ToArray();

        return new JiraDashboardPageViewModel
        {
            Session = BuildSessionSummary(session),
            Filter = BuildDashboardFilter(periodType, periodValue, sort, direction, rangeStart, rangeEnd),
            Stats = BuildDashboardStats(mappedRows),
            Rows = sortedRows,
            AppliedJql = appliedJql,
            ErrorMessage = errorMessage
        };
    }

    private async Task<JiraWorklistPageViewModel> BuildWorklistPageModelAsync(JiraSession session, JiraSearchRequest search, bool runSearch = false)
    {
        var filters = await SafeFiltersAsync(session);
        var users = await SafeUsersAsync(session);
        var issues = Array.Empty<JiraIssue>();
        var appliedJql = string.Empty;
        string? errorMessage = null;

        if (runSearch)
        {
            try
            {
                appliedJql = JiraQueryBuilder.Build(search, _localizer["Jira.Search.AssigneeRequired"].Value);
                issues = (await session.Client.SearchIssuesAsync(appliedJql)).ToArray();
            }
            catch (Exception ex)
            {
                errorMessage = JiraErrorHelper.ToUserMessage(ex, _localizer["Jira.SearchFailed"].Value);
            }
        }

        return new JiraWorklistPageViewModel
        {
            Session = BuildSessionSummary(session),
            Search = search,
            Issues = issues,
            Filters = filters,
            Users = users,
            AppliedJql = appliedJql,
            ErrorMessage = errorMessage
        };
    }

    private JiraSessionSummaryViewModel BuildSessionSummary(JiraSession session)
    {
        return new JiraSessionSummaryViewModel
        {
            DisplayName = session.DisplayName,
            Username = session.Username,
            BaseUrl = session.BaseUrl,
            LoginAt = session.LoginAt,
            ExpiresAt = session.ExpiresAt
        };
    }

    private JiraDashboardFilterViewModel BuildDashboardFilter(
        string periodType,
        string periodValue,
        string sort,
        string direction,
        DateTime rangeStart,
        DateTime rangeEnd)
    {
        return new JiraDashboardFilterViewModel
        {
            PeriodType = periodType,
            PeriodValue = periodValue,
            DayValue = rangeStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            WeekValue = $"{ISOWeek.GetYear(rangeStart)}-W{ISOWeek.GetWeekOfYear(rangeStart):00}",
            MonthValue = rangeStart.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            YearValue = rangeStart.Year.ToString(CultureInfo.InvariantCulture),
            RangeLabel = BuildRangeLabel(periodType, rangeStart, rangeEnd),
            Sort = sort,
            Direction = direction
        };
    }

    private string BuildRangeLabel(string periodType, DateTime rangeStart, DateTime rangeEnd)
    {
        return periodType == "day"
            ? _localizer["Jira.Dashboard.RangeDay", rangeStart.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)].Value
            : _localizer["Jira.Dashboard.RangePeriod",
                rangeStart.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                rangeEnd.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)].Value;
    }

    private IReadOnlyList<JiraDashboardStatViewModel> BuildDashboardStats(IReadOnlyList<JiraDashboardRowViewModel> rows)
    {
        var issueCount = rows.Select(x => string.IsNullOrWhiteSpace(x.IssueKey) ? x.SubTaskKey : x.IssueKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var subTaskCount = rows.Count;
        var doneCount = rows.Count(IsDoneStatus);
        var openCount = subTaskCount - doneCount;
        var estimateHours = rows.Sum(x => x.EstimateHours);
        var loggedHours = rows.Sum(x => x.LoggedHours);

        return
        [
            new JiraDashboardStatViewModel
            {
                Label = _localizer["Jira.Dashboard.Stats.Issues.Label"].Value,
                Value = issueCount.ToString(CultureInfo.InvariantCulture),
                Hint = _localizer["Jira.Dashboard.Stats.Issues.Hint"].Value,
                AccentClass = "blue"
            },
            new JiraDashboardStatViewModel
            {
                Label = _localizer["Jira.Dashboard.Stats.Subtasks.Label"].Value,
                Value = subTaskCount.ToString(CultureInfo.InvariantCulture),
                Hint = _localizer["Jira.Dashboard.Stats.Subtasks.Hint"].Value,
                AccentClass = "neutral"
            },
            new JiraDashboardStatViewModel
            {
                Label = _localizer["Jira.Dashboard.Stats.Done.Label"].Value,
                Value = doneCount.ToString(CultureInfo.InvariantCulture),
                Hint = _localizer["Jira.Dashboard.Stats.Done.Hint"].Value,
                AccentClass = "mint"
            },
            new JiraDashboardStatViewModel
            {
                Label = _localizer["Jira.Dashboard.Stats.Open.Label"].Value,
                Value = openCount.ToString(CultureInfo.InvariantCulture),
                Hint = _localizer["Jira.Dashboard.Stats.Open.Hint"].Value,
                AccentClass = "amber"
            },
            new JiraDashboardStatViewModel
            {
                Label = _localizer["Jira.Dashboard.Stats.Estimate.Label"].Value,
                Value = FormatHours(estimateHours, dashWhenZero: false),
                Hint = _localizer["Jira.Dashboard.Stats.Estimate.Hint"].Value,
                AccentClass = "rose"
            },
            new JiraDashboardStatViewModel
            {
                Label = _localizer["Jira.Dashboard.Stats.Logged.Label"].Value,
                Value = FormatHours(loggedHours, dashWhenZero: false),
                Hint = _localizer["Jira.Dashboard.Stats.Logged.Hint"].Value,
                AccentClass = "violet"
            }
        ];
    }

    private static IReadOnlyList<JiraDashboardRowViewModel> MapDashboardRows(
        IReadOnlyList<JiraIssue> issues,
        DateTime rangeStart,
        DateTime rangeEnd,
        string currentUsername)
    {
        return issues.Select(issue =>
        {
            var subTaskKey = issue.IssueKey ?? string.Empty;
            var status = issue.Status ?? string.Empty;
            var project = DeriveProject(subTaskKey);
            var estimateHours = ParseHours(issue.Estimate);
            var loggedHours = CalculateLoggedHours(issue.Worklogs, rangeStart, rangeEnd, currentUsername);
            var isDone = IsDoneStatus(status);

            return new JiraDashboardRowViewModel
            {
                Project = project,
                SubTaskKey = subTaskKey,
                SubTaskSummary = issue.Summary ?? string.Empty,
                IssueKey = issue.Parent ?? string.Empty,
                IssueSummary = issue.ParentSummary ?? string.Empty,
                Status = status,
                StatusTone = ResolveStatusTone(status),
                DueDate = issue.DueDate,
                CreateDate = issue.CreateDate,
                DueDateText = issue.DueDate?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "-",
                EstimateTimeText = FormatHours(estimateHours),
                LoggedTimeText = FormatHours(loggedHours),
                EstimateHours = estimateHours,
                LoggedHours = loggedHours,
                IsOverdue = issue.DueDate.HasValue && issue.DueDate.Value.Date < DateTime.Today && !isDone
            };
        }).ToArray();
    }

    private static IReadOnlyList<JiraDashboardRowViewModel> SortDashboardRows(
        IReadOnlyList<JiraDashboardRowViewModel> rows,
        string sort,
        string direction)
    {
        var comparer = StringComparer.OrdinalIgnoreCase;
        var ascending = string.Equals(direction, "asc", StringComparison.OrdinalIgnoreCase);

        Func<JiraDashboardRowViewModel, object?> keySelector = sort switch
        {
            "project" => row => row.Project,
            "subTaskKey" => row => row.SubTaskKey,
            "subTaskSummary" => row => row.SubTaskSummary,
            "issueKey" => row => row.IssueKey,
            "issueSummary" => row => row.IssueSummary,
            "status" => row => row.Status,
            "estimate" => row => row.EstimateHours,
            "logged" => row => row.LoggedHours,
            _ => row => row.DueDate ?? DateTime.MaxValue
        };

        IEnumerable<JiraDashboardRowViewModel> ordered;

        if (sort is "estimate" or "logged" or "dueDate")
        {
            ordered = ascending
                ? rows.OrderBy(keySelector).ThenBy(row => row.SubTaskKey, comparer)
                : rows.OrderByDescending(keySelector).ThenBy(row => row.SubTaskKey, comparer);
        }
        else
        {
            ordered = ascending
                ? rows.OrderBy(row => keySelector(row)?.ToString(), comparer).ThenBy(row => row.SubTaskKey, comparer)
                : rows.OrderByDescending(row => keySelector(row)?.ToString(), comparer).ThenBy(row => row.SubTaskKey, comparer);
        }

        return ordered.ToArray();
    }

    private static JiraDashboardRowViewModel CopyRowWithIndex(JiraDashboardRowViewModel row, int index)
    {
        return new JiraDashboardRowViewModel
        {
            Index = index,
            Project = row.Project,
            SubTaskKey = row.SubTaskKey,
            SubTaskSummary = row.SubTaskSummary,
            IssueKey = row.IssueKey,
            IssueSummary = row.IssueSummary,
            Status = row.Status,
            StatusTone = row.StatusTone,
            DueDate = row.DueDate,
            CreateDate = row.CreateDate,
            DueDateText = row.DueDateText,
            EstimateTimeText = row.EstimateTimeText,
            LoggedTimeText = row.LoggedTimeText,
            EstimateHours = row.EstimateHours,
            LoggedHours = row.LoggedHours,
            IsOverdue = row.IsOverdue
        };
    }

    private static string BuildDashboardJql(DateTime rangeStart, DateTime rangeEnd)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"assignee = currentUser() AND issuetype in subTaskIssueTypes() AND ((duedate >= \"{rangeStart:yyyy-MM-dd}\" AND duedate <= \"{rangeEnd:yyyy-MM-dd}\") OR (worklogDate >= \"{rangeStart:yyyy-MM-dd}\" AND worklogDate <= \"{rangeEnd:yyyy-MM-dd}\"))");
    }

    private static (DateTime Start, DateTime End, string PeriodValue) ResolveRange(string periodType, string? requestedValue)
    {
        var today = DateTime.Today;

        if (periodType == "day")
        {
            if (DateTime.TryParseExact(requestedValue, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
            {
                return (day.Date, day.Date, day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            }

            return (today, today, today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        if (periodType == "month")
        {
            if (DateTime.TryParseExact(requestedValue, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var month))
            {
                var start = new DateTime(month.Year, month.Month, 1);
                return (start, start.AddMonths(1).AddDays(-1), start.ToString("yyyy-MM", CultureInfo.InvariantCulture));
            }

            var currentMonth = new DateTime(today.Year, today.Month, 1);
            return (currentMonth, currentMonth.AddMonths(1).AddDays(-1), currentMonth.ToString("yyyy-MM", CultureInfo.InvariantCulture));
        }

        if (periodType == "year")
        {
            if (int.TryParse(requestedValue, NumberStyles.None, CultureInfo.InvariantCulture, out var year)
                && year is >= 2000 and <= 2100)
            {
                return (new DateTime(year, 1, 1), new DateTime(year, 12, 31), year.ToString(CultureInfo.InvariantCulture));
            }

            return (new DateTime(today.Year, 1, 1), new DateTime(today.Year, 12, 31), today.Year.ToString(CultureInfo.InvariantCulture));
        }

        var normalizedWeek = requestedValue;
        if (!string.IsNullOrWhiteSpace(normalizedWeek))
        {
            var match = Regex.Match(normalizedWeek, @"^(?<year>\d{4})-W(?<week>\d{2})$");
            if (match.Success
                && int.TryParse(match.Groups["year"].Value, out var weekYear)
                && int.TryParse(match.Groups["week"].Value, out var weekNumber))
            {
                var start = ISOWeek.ToDateTime(weekYear, weekNumber, DayOfWeek.Monday);
                return (start.Date, start.Date.AddDays(6), $"{weekYear}-W{weekNumber:00}");
            }
        }

        var currentWeekYear = ISOWeek.GetYear(today);
        var currentWeekNumber = ISOWeek.GetWeekOfYear(today);
        var currentWeekStart = ISOWeek.ToDateTime(currentWeekYear, currentWeekNumber, DayOfWeek.Monday);
        return (currentWeekStart.Date, currentWeekStart.Date.AddDays(6), $"{currentWeekYear}-W{currentWeekNumber:00}");
    }

    private static string NormalizePeriodType(string? periodType)
    {
        return periodType?.Trim().ToLowerInvariant() switch
        {
            "day" => "day",
            "month" => "month",
            "year" => "year",
            _ => "week"
        };
    }

    private static string NormalizeSort(string? sort)
    {
        return sort?.Trim() switch
        {
            "project" => "project",
            "subTaskKey" => "subTaskKey",
            "subTaskSummary" => "subTaskSummary",
            "issueKey" => "issueKey",
            "issueSummary" => "issueSummary",
            "status" => "status",
            "estimate" => "estimate",
            "logged" => "logged",
            _ => "dueDate"
        };
    }

    private static string NormalizeDirection(string? direction)
    {
        return string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase) ? "desc" : "asc";
    }

    private static string DeriveProject(string subTaskKey)
    {
        if (string.IsNullOrWhiteSpace(subTaskKey))
        {
            return "-";
        }

        var dashIndex = subTaskKey.IndexOf('-', StringComparison.Ordinal);
        return dashIndex > 0 ? subTaskKey[..dashIndex] : subTaskKey;
    }

    private static decimal ParseHours(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        var total = 0m;
        foreach (Match match in Regex.Matches(value, @"(?<amount>\d+(?:[.,]\d+)?)\s*(?<unit>[wdhm])", RegexOptions.IgnoreCase))
        {
            if (!decimal.TryParse(
                    match.Groups["amount"].Value.Replace(',', '.'),
                    NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture,
                    out var amount))
            {
                continue;
            }

            total += match.Groups["unit"].Value.ToLowerInvariant() switch
            {
                "w" => amount * 40m,
                "d" => amount * 8m,
                "h" => amount,
                "m" => amount / 60m,
                _ => 0m
            };
        }

        return total;
    }

    private static decimal CalculateLoggedHours(
        IReadOnlyList<WorklogEntry>? worklogs,
        DateTime rangeStart,
        DateTime rangeEnd,
        string currentUsername)
    {
        if (worklogs is null || worklogs.Count == 0)
        {
            return 0;
        }

        var normalizedCurrentUser = NormalizeUserKey(currentUsername);
        var ownedWorklogs = worklogs
            .Where(x => x.Started != DateTime.MinValue)
            .Where(x => x.Started.Date >= rangeStart.Date && x.Started.Date <= rangeEnd.Date)
            .Where(x =>
            {
                var author = NormalizeUserKey(x.AuthorName);
                return string.IsNullOrWhiteSpace(normalizedCurrentUser)
                    || string.IsNullOrWhiteSpace(author)
                    || string.Equals(author, normalizedCurrentUser, StringComparison.OrdinalIgnoreCase);
            });

        return ownedWorklogs.Sum(x => ParseHours(x.TimeSpent));
    }

    private static string NormalizeUserKey(string? value)
    {
        return value?.Trim().ToLowerInvariant() ?? string.Empty;
    }

    private static string FormatHours(decimal hours, bool dashWhenZero = true)
    {
        if (hours <= 0)
        {
            return dashWhenZero ? "-" : "0.00";
        }

        return string.Create(CultureInfo.InvariantCulture, $"{hours:0.00}");
    }

    private static bool IsDoneStatus(JiraDashboardRowViewModel row)
    {
        return IsDoneStatus(row.Status);
    }

    private static bool IsDoneStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return false;
        }

        return DoneStatusHints.Any(hint => status.Contains(hint, StringComparison.OrdinalIgnoreCase));
    }

    private static string ResolveStatusTone(string status)
    {
        if (IsDoneStatus(status))
        {
            return "done";
        }

        return DoingStatusHints.Any(hint => status.Contains(hint, StringComparison.OrdinalIgnoreCase))
            ? "doing"
            : "todo";
    }

    private async Task<JiraSession?> GetSessionOrRedirectAsync()
    {
        return await GetSessionOrSignOutAsync();
    }

    private async Task<JiraSession?> GetSessionOrSignOutAsync()
    {
        var session = await _jiraAuth.GetCurrentSessionAsync(HttpContext);
        if (session is not null)
        {
            return session;
        }

        await _jiraAuth.SignOutAsync(HttpContext);
        TempData["ErrorMessage"] = _localizer["Jira.SessionExpired"].Value;
        return null;
    }

    private RedirectToPageResult RedirectToLogin()
    {
        return RedirectToPage("/Account/Login", new
        {
            area = "Identity",
            returnUrl = $"{Request.Path}{Request.QueryString}"
        });
    }

    private static async Task<IReadOnlyList<JiraFilter>> SafeFiltersAsync(JiraSession session)
    {
        try
        {
            return await session.Client.GetFavouriteFiltersAsync();
        }
        catch
        {
            return [];
        }
    }

    private static async Task<IReadOnlyList<JiraUser>> SafeUsersAsync(JiraSession session)
    {
        try
        {
            return await session.Client.GetUsersAsync();
        }
        catch
        {
            return [];
        }
    }
}
