using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using ToolboxWeb.Web.Constants;
using ToolboxWeb.Web.Infrastructure.Hosting;
using ToolboxWeb.Web.Infrastructure.Jira;
using ToolboxWeb.Web.Services;
using ToolboxWeb.Web.ViewModels.Jira;
using ToolboxWeb.Web.Extensions;
using ToolboxWeb.Web.Infrastructure.Time;

namespace ToolboxWeb.Web.Controllers;

[Authorize(Policy = "JiraOnly")]
public class JiraController : Controller
{
    private const string MySubtasksBaseJql =
        "assignee = currentUser() AND issuetype in subTaskIssueTypes()";

    // Auto-scheduling only ever plans work that is still outstanding.
    private const string MyOpenSubtasksBaseJql =
        MySubtasksBaseJql + " AND statusCategory != Done";

    // Auto-schedule asks for the days it may fill: about three months from the start date.
    private const int MaxScheduledRangeDays = 120;

    private readonly IJiraAuthService _jiraAuth;
    private readonly IJiraDueDateService _dueDates;
    private readonly ICurrentUserService _currentUser;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly AppModeInfo _mode;
    private readonly IDesktopJiraService _desktopJira;
    private readonly IJiraCatalogService _catalogs;

    public JiraController(
        IJiraAuthService jiraAuth,
        IJiraDueDateService dueDates,
        ICurrentUserService currentUser,
        IStringLocalizer<SharedResource> localizer,
        AppModeInfo mode,
        IDesktopJiraService desktopJira,
        IJiraCatalogService catalogs)
    {
        _catalogs = catalogs;
        _jiraAuth = jiraAuth;
        _dueDates = dueDates;
        _currentUser = currentUser;
        _localizer = localizer;
        _mode = mode;
        _desktopJira = desktopJira;
    }

    /// <summary>Old addresses of Statistics and Task management (bookmarks, installed apps): permanent redirect, query kept.</summary>
    [HttpGet("/" + ToolboxRouteSlugs.LegacyPages.JiraDashboard)]
    [HttpGet("/" + ToolboxRouteSlugs.LegacyPages.JiraDashboardHtml)]
    [AllowAnonymous]
    public IActionResult LegacyDashboard() =>
        RedirectPermanent((Url.RouteUrl(ToolboxRouteSlugs.RouteNames.JiraDashboardHtml) ?? "/" + ToolboxRouteSlugs.HtmlPages.JiraDashboard) + Request.QueryString);

    [HttpGet("/" + ToolboxRouteSlugs.LegacyPages.JiraWorklist)]
    [HttpGet("/" + ToolboxRouteSlugs.LegacyPages.JiraWorklistHtml)]
    [AllowAnonymous]
    public IActionResult LegacyWorklist() =>
        RedirectPermanent((Url.RouteUrl(ToolboxRouteSlugs.RouteNames.JiraWorklistHtml) ?? "/" + ToolboxRouteSlugs.HtmlPages.JiraWorklist) + Request.QueryString);

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
            FromDate = AppClock.Today,
            ToDate = AppClock.Today
        }));
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
    [AllowWhenUnlicensed]
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
            model.Summary,
            model.LogBarUnit,
            model.LogBars,
            model.Rows,
            model.HoursByType,
            model.AppliedJql,
            model.ErrorMessage
        }));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [AllowWhenUnlicensed]
    public async Task<IActionResult> Search([FromBody] JiraSearchRequest request)
    {
        var session = await GetSessionOrSignOutAsync();
        if (session is null)
        {
            return Json(JiraApiResponse<object>.LoginRequired(_localizer["Jira.SessionExpired"].Value));
        }

        if (request is null)
        {
            return Json(JiraApiResponse<object>.Fail(_localizer["Jira.SearchFailed"].Value));
        }

        var invalid = JiraQueryBuilder.Validate(
            request,
            _localizer["Jira.Search.AssigneeRequired"].Value,
            _localizer["Jira.Search.DateOrder"].Value);
        if (invalid is not null)
        {
            return Json(JiraApiResponse<object>.Fail(invalid));
        }

        try
        {
            var jql = JiraQueryBuilder.Build(request, _localizer["Jira.Search.AssigneeRequired"].Value);
            var rules = await _catalogs.GetRulesAsync(session.BaseUrl);
            var issues = await session.Client.SearchIssuesAsync(jql);
            return Json(JiraApiResponse<object>.Ok(JiraSearchResults.Build(issues, jql, AppClock.Today, rules)));
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

    /// <summary>Writes due dates moved on the board to Jira; answers with one result per issue.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateDueDates([FromBody] JiraDueDateUpdateRequest? request)
    {
        var session = await GetSessionOrSignOutAsync();
        if (session is null)
        {
            return Json(JiraApiResponse<object>.LoginRequired(_localizer["Jira.SessionExpired"].Value));
        }

        if (request?.Changes is not { Count: > 0 })
        {
            return Json(JiraApiResponse<object>.Fail(_localizer["Jira.Save.Invalid"].Value));
        }

        var results = await _dueDates.UpdateAsync(session.Client, _currentUser.UserId, request.Changes, HttpContext.RequestAborted);
        return Json(JiraApiResponse<object>.Ok(results));
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

    [HttpGet]
    public async Task<IActionResult> TaskBoard([FromQuery] JiraDashboardQueryViewModel query)
    {
        var session = await GetSessionOrSignOutAsync();
        if (session is null)
        {
            return Json(JiraApiResponse<object>.LoginRequired(_localizer["Jira.SessionExpired"].Value));
        }

        var periodType = NormalizePeriodType(query.PeriodType);
        var (rangeStart, rangeEnd, periodValue) = ResolveRange(periodType, query.PeriodValue);
        var jql = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{MySubtasksBaseJql} AND duedate >= \"{rangeStart:yyyy-MM-dd}\" AND duedate <= \"{rangeEnd:yyyy-MM-dd}\" ORDER BY duedate ASC");

        string? errorMessage = null;
        IReadOnlyList<JiraDashboardRowViewModel> rows = [];

        try
        {
            var rules = await _catalogs.GetRulesAsync(session.BaseUrl);
            var issues = await session.Client.SearchIssuesAsync(jql, includeWorklogs: true);
            rows = MapSubtaskRows(issues, rules);
        }
        catch (Exception ex)
        {
            errorMessage = JiraErrorHelper.ToUserMessage(
                ex,
                _localizer["Jira.TaskBoard.Error"].Value,
                _localizer["Jira.SessionExpired"].Value,
                _localizer["Jira.NotFound"].Value,
                _localizer["Jira.InvalidRequest"].Value);
        }

        var filter = BuildDashboardFilter(periodType, periodValue, "dueDate", "asc", rangeStart, rangeEnd);

        return Json(JiraApiResponse<object>.Ok(new
        {
            filter,
            rows,
            errorMessage
        }));
    }

    [HttpGet]
    public async Task<IActionResult> AutoScheduleSearch([FromQuery] JiraAutoScheduleSearchRequest request)
    {
        var session = await GetSessionOrSignOutAsync();
        if (session is null)
        {
            return Json(JiraApiResponse<object>.LoginRequired(_localizer["Jira.SessionExpired"].Value));
        }

        var jql = BuildAutoScheduleJql(request);
        string? errorMessage = null;
        IReadOnlyList<JiraDashboardRowViewModel> rows = [];

        try
        {
            var rules = await _catalogs.GetRulesAsync(session.BaseUrl);
            var issues = await session.Client.SearchIssuesAsync(jql, includeWorklogs: true);
            rows = MapSubtaskRows(issues, rules);
        }
        catch (Exception ex)
        {
            errorMessage = JiraErrorHelper.ToUserMessage(
                ex,
                _localizer["Jira.AutoSchedule.Error"].Value,
                _localizer["Jira.SessionExpired"].Value,
                _localizer["Jira.NotFound"].Value,
                _localizer["Jira.InvalidRequest"].Value);
        }

        return Json(JiraApiResponse<object>.Ok(new
        {
            rows,
            appliedJql = jql,
            errorMessage
        }));
    }

    /// <summary>
    /// Hours the user logged in Jira today, for the card on the personal overview. The card only
    /// asks when a Jira session is open, so a missing session is a plain "no data".
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> TodayHours()
    {
        var session = await _jiraAuth.GetCurrentSessionAsync(HttpContext);
        if (session is null)
        {
            return Json(JiraApiResponse<object>.LoginRequired(_localizer["Jira.SessionExpired"].Value));
        }

        var today = AppClock.Today;
        try
        {
            var rules = await _catalogs.GetRulesAsync(session.BaseUrl);
            var issues = await session.Client.SearchIssuesAsync(
                string.Create(CultureInfo.InvariantCulture, $"worklogAuthor = currentUser() AND worklogDate = \"{today:yyyy-MM-dd}\""),
                includeWorklogs: true);
            return Json(JiraApiResponse<object>.Ok(new
            {
                hours = JiraDashboardMetrics.LoggedHours(issues, today, today, session.Username, rules),
                target = JiraDashboardMetrics.IsWorkingDay(today) ? rules.HoursPerDay : 0m
            }));
        }
        catch (Exception ex)
        {
            return Json(JiraApiResponse<object>.Fail(JiraErrorHelper.ToUserMessage(
                ex,
                _localizer["Dashboard.JiraHoursFailed"].Value,
                _localizer["Jira.SessionExpired"].Value,
                _localizer["Jira.NotFound"].Value,
                _localizer["Jira.InvalidRequest"].Value)));
        }
    }

    /// <summary>
    /// My subtasks due between two dates (any status). Auto-schedule reads them to know how many
    /// hours each day already holds before it adds more.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ScheduledSubtasks([FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        var session = await GetSessionOrSignOutAsync();
        if (session is null)
        {
            return Json(JiraApiResponse<object>.LoginRequired(_localizer["Jira.SessionExpired"].Value));
        }

        if (from == default || to < from || (to - from).TotalDays > MaxScheduledRangeDays)
        {
            return Json(JiraApiResponse<object>.Fail(_localizer["Jira.InvalidRequest"].Value));
        }

        var jql = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{MySubtasksBaseJql} AND duedate >= \"{from:yyyy-MM-dd}\" AND duedate <= \"{to:yyyy-MM-dd}\" ORDER BY duedate ASC");

        try
        {
            var rules = await _catalogs.GetRulesAsync(session.BaseUrl);
            var issues = await session.Client.SearchIssuesAsync(jql, includeWorklogs: true);
            return Json(JiraApiResponse<object>.Ok(new { rows = MapSubtaskRows(issues, rules) }));
        }
        catch (Exception ex)
        {
            return Json(JiraApiResponse<object>.Fail(JiraErrorHelper.ToUserMessage(
                ex,
                _localizer["Jira.AutoSchedule.ExistingLoadFailed"].Value,
                _localizer["Jira.SessionExpired"].Value,
                _localizer["Jira.NotFound"].Value,
                _localizer["Jira.InvalidRequest"].Value)));
        }
    }

    private static string BuildAutoScheduleJql(JiraAutoScheduleSearchRequest request)
    {
        var parts = new List<string> { MyOpenSubtasksBaseJql };

        if (!string.IsNullOrWhiteSpace(request.Project))
        {
            parts.Add($"project = \"{request.Project.Trim().Replace("\"", "\\\"")}\"");
        }

        switch (request.Status?.Trim().ToLowerInvariant())
        {
            case "todo":
                parts.Add("statusCategory = \"To Do\"");
                break;
            case "doing":
                parts.Add("statusCategory = \"In Progress\"");
                break;
        }

        if (!string.IsNullOrWhiteSpace(request.Text))
        {
            var text = request.Text.Trim().Replace("\"", "\\\"");
            parts.Add($"(summary ~ \"{text}\" OR key = \"{text}\")");
        }

        if (request.DueFrom.HasValue)
        {
            parts.Add($"duedate >= \"{request.DueFrom.Value:yyyy-MM-dd}\"");
        }

        if (request.DueTo.HasValue)
        {
            parts.Add($"duedate <= \"{request.DueTo.Value:yyyy-MM-dd}\"");
        }

        return string.Join(" AND ", parts) + " ORDER BY duedate ASC";
    }

    private static IReadOnlyList<JiraDashboardRowViewModel> MapSubtaskRows(IReadOnlyList<JiraIssue> issues, JiraRules rules)
    {
        return issues.Select((issue, index) =>
        {
            var subTaskKey = issue.IssueKey ?? string.Empty;
            var status = issue.Status ?? string.Empty;
            var estimateHours = rules.EstimateHours(issue);
            var loggedHours = issue.Worklogs.Sum(rules.Hours);
            var daysOverdue = rules.DaysOverdue(issue, AppClock.Today);

            return new JiraDashboardRowViewModel
            {
                Index = index + 1,
                Project = DeriveProject(subTaskKey),
                SubTaskKey = subTaskKey,
                SubTaskSummary = issue.Summary ?? string.Empty,
                IssueKey = issue.Parent ?? string.Empty,
                IssueSummary = issue.ParentSummary ?? string.Empty,
                Status = status,
                StatusTone = rules.Tone(issue),
                IsWaiting = rules.IsWaiting(status),
                SubTaskType = issue.SubTaskType ?? string.Empty,
                DueDate = issue.DueDate,
                CreateDate = issue.CreateDate,
                DueDateText = issue.DueDate.ToDisplayDate(),
                EstimateTimeText = FormatHours(estimateHours),
                LoggedTimeText = FormatHours(loggedHours),
                EstimateHours = estimateHours,
                LoggedHours = loggedHours,
                TotalLoggedHours = loggedHours,
                IsOverdue = daysOverdue > 0,
                DaysOverdue = daysOverdue
            };
        }).ToArray();
    }

    private async Task<JiraDashboardPageViewModel> BuildDashboardPageModelAsync(JiraSession session, JiraDashboardQueryViewModel? query)
    {
        var periodType = NormalizePeriodType(query?.PeriodType);
        var sort = NormalizeSort(query?.Sort);
        var direction = NormalizeDirection(query?.Dir);
        var (rangeStart, rangeEnd, periodValue) = ResolveRange(periodType, query?.PeriodValue);

        string? errorMessage = null;
        IReadOnlyList<JiraIssue> issues = [];
        IReadOnlyList<JiraDashboardRowViewModel>? previousRows = null;
        var today = AppClock.Today;
        var rules = await _catalogs.GetRulesAsync(session.BaseUrl);

        var appliedJql = BuildDashboardJql(rangeStart, rangeEnd);
        var (previousStart, previousEnd) = JiraDashboardMetrics.PreviousRange(periodType, rangeStart);

        // The previous period only feeds the "compared with last period" lines; if it fails the
        // cards simply leave the comparison out.
        var currentTask = session.Client.SearchIssuesAsync(appliedJql, includeWorklogs: true);
        var previousTask = session.Client.SearchIssuesAsync(BuildDashboardJql(previousStart, previousEnd), includeWorklogs: true);

        try
        {
            issues = await currentTask;
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

        try
        {
            previousRows = MapDashboardRows(await previousTask, previousStart, previousEnd, session.Username, rules);
        }
        catch (Exception)
        {
            previousRows = null;
        }

        var mappedRows = MapDashboardRows(issues, rangeStart, rangeEnd, session.Username, rules);
        var sortedRows = SortDashboardRows(mappedRows, sort, direction)
            .Select((row, index) => CopyRowWithIndex(row, index + 1))
            .ToArray();

        return new JiraDashboardPageViewModel
        {
            Session = BuildSessionSummary(session),
            Filter = BuildDashboardFilter(periodType, periodValue, sort, direction, rangeStart, rangeEnd),
            Summary = JiraDashboardMetrics.BuildSummary(mappedRows, rangeStart, rangeEnd, today, errorMessage is null ? previousRows : null, rules.HoursPerDay),
            LogBarUnit = periodType == "year" ? "month" : "day",
            LogBars = JiraDashboardMetrics.BuildLogBars(issues, periodType, rangeStart, rangeEnd, today, session.Username, rules),
            Rows = sortedRows,
            HoursByType = JiraDashboardMetrics.HoursByType(mappedRows),
            AppliedJql = appliedJql,
            ErrorMessage = errorMessage
        };
    }

    // The Search tab runs its searches through POST /Jira/Search; the page only brings the saved
    // filters and users for the form.
    private async Task<JiraWorklistPageViewModel> BuildWorklistPageModelAsync(JiraSession session, JiraSearchRequest search)
    {
        return new JiraWorklistPageViewModel
        {
            Session = BuildSessionSummary(session),
            Search = search,
            Filters = await SafeFiltersAsync(session),
            Users = await _catalogs.GetUsersAsync(session.BaseUrl) ?? await SafeUsersAsync(session)
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
            ? _localizer["Jira.Dashboard.RangeDay", rangeStart.ToDisplayDate()].Value
            : _localizer["Jira.Dashboard.RangePeriod",
                rangeStart.ToDisplayDate(),
                rangeEnd.ToDisplayDate()].Value;
    }

    private static IReadOnlyList<JiraDashboardRowViewModel> MapDashboardRows(
        IReadOnlyList<JiraIssue> issues,
        DateTime rangeStart,
        DateTime rangeEnd,
        string currentUsername,
        JiraRules rules)
    {
        return issues.Select(issue =>
        {
            var subTaskKey = issue.IssueKey ?? string.Empty;
            var status = issue.Status ?? string.Empty;
            var project = DeriveProject(subTaskKey);
            var estimateHours = rules.EstimateHours(issue);
            var loggedHours = JiraDashboardMetrics.OwnWorklogs(issue.Worklogs, rangeStart, rangeEnd, currentUsername)
                .Sum(rules.Hours);
            var totalLoggedHours = issue.Worklogs.Sum(rules.Hours);
            var daysOverdue = rules.DaysOverdue(issue, AppClock.Today);

            return new JiraDashboardRowViewModel
            {
                Project = project,
                SubTaskKey = subTaskKey,
                SubTaskSummary = issue.Summary ?? string.Empty,
                IssueKey = issue.Parent ?? string.Empty,
                IssueSummary = issue.ParentSummary ?? string.Empty,
                Status = status,
                StatusTone = rules.Tone(issue),
                IsWaiting = rules.IsWaiting(status),
                SubTaskType = issue.SubTaskType ?? string.Empty,
                DueDate = issue.DueDate,
                CreateDate = issue.CreateDate,
                DueDateText = issue.DueDate.ToDisplayDate(),
                EstimateTimeText = FormatHours(estimateHours),
                LoggedTimeText = FormatHours(loggedHours),
                EstimateHours = estimateHours,
                LoggedHours = loggedHours,
                TotalLoggedHours = totalLoggedHours,
                IsOverdue = daysOverdue > 0,
                DaysOverdue = daysOverdue
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
            IsWaiting = row.IsWaiting,
            SubTaskType = row.SubTaskType,
            DueDate = row.DueDate,
            CreateDate = row.CreateDate,
            DueDateText = row.DueDateText,
            EstimateTimeText = row.EstimateTimeText,
            LoggedTimeText = row.LoggedTimeText,
            EstimateHours = row.EstimateHours,
            LoggedHours = row.LoggedHours,
            TotalLoggedHours = row.TotalLoggedHours,
            IsOverdue = row.IsOverdue,
            DaysOverdue = row.DaysOverdue
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
        var today = AppClock.Today;

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

    private static string FormatHours(decimal hours, bool dashWhenZero = true)
    {
        if (hours <= 0)
        {
            return dashWhenZero ? "-" : 0m.ToDisplayNumber();
        }

        // Follows the UI culture: 17,5 in vi-VN, 17.5 in en-US.
        return hours.ToDisplayNumber();
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

        if (_mode.IsDesktop)
        {
            // No sign-in page on a desktop: Settings says whether Jira is not set up or what failed.
            var status = _desktopJira.GetStatus();
            if (status.Configured)
            {
                TempData[TempDataKeys.ErrorMessage] = status.LastError ?? _localizer["Settings.Jira.Unreachable"].Value;
            }
            else
            {
                TempData[TempDataKeys.InfoMessage] = _localizer["Settings.Jira.Required"].Value;
            }

            return null;
        }

        await _jiraAuth.SignOutAsync(HttpContext);
        TempData["ErrorMessage"] = _localizer["Jira.SessionExpired"].Value;
        return null;
    }

    private IActionResult RedirectToLogin()
    {
        var returnUrl = $"{Request.Path}{Request.QueryString}";
        if (_mode.IsDesktop)
        {
            return RedirectToRoute(ToolboxRouteSlugs.RouteNames.SettingsHtml, new { returnUrl });
        }

        return RedirectToPage("/Account/Login", new
        {
            area = "Identity",
            returnUrl
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
