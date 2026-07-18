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
    private readonly IJiraAuthService _jiraAuth;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public JiraController(IJiraAuthService jiraAuth, IStringLocalizer<SharedResource> localizer)
    {
        _jiraAuth = jiraAuth;
        _localizer = localizer;
    }

    [HttpGet("/" + ToolboxRouteSlugs.Pages.JiraDashboard)]
    [HttpGet("/" + ToolboxRouteSlugs.HtmlPages.JiraDashboard, Name = ToolboxRouteSlugs.RouteNames.JiraDashboardHtml)]
    public async Task<IActionResult> DashboardPage()
    {
        var session = await GetSessionOrRedirectAsync();
        if (session is null)
        {
            return RedirectToLogin();
        }

        return View("Dashboard", await BuildDashboardPageModelAsync(session));
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

        return Json(JiraApiResponse<object>.Ok(new JiraSessionSummaryViewModel
        {
            DisplayName = session.DisplayName,
            Username = session.Username,
            BaseUrl = session.BaseUrl,
            LoginAt = session.LoginAt,
            ExpiresAt = session.ExpiresAt
        }));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _jiraAuth.SignOutAsync(HttpContext);
        return Json(JiraApiResponse<object>.Ok(new { }));
    }

    [HttpGet]
    public async Task<IActionResult> Dashboard()
    {
        var session = await GetSessionOrSignOutAsync();
        if (session is null)
        {
            return Json(JiraApiResponse<object>.LoginRequired(_localizer["Jira.SessionExpired"].Value));
        }

        var model = await BuildDashboardPageModelAsync(session);
        return Json(JiraApiResponse<object>.Ok(new
        {
            model.AssignedIssues,
            model.DueSubTasks
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

    private async Task<JiraDashboardPageViewModel> BuildDashboardPageModelAsync(JiraSession session)
    {
        var assigned = await SafeSearchAsync(session, JiraDefaults.AssignedIssuesJql);
        var dueSubTasks = await SafeSearchAsync(session, JiraDefaults.DueSubTasksJql);

        return new JiraDashboardPageViewModel
        {
            Session = BuildSessionSummary(session),
            AssignedIssues = assigned,
            DueSubTasks = dueSubTasks
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

    private static async Task<IReadOnlyList<JiraIssue>> SafeSearchAsync(JiraSession session, string jql)
    {
        try
        {
            return await session.Client.SearchIssuesAsync(jql, includeWorklogs: false);
        }
        catch
        {
            return [];
        }
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
