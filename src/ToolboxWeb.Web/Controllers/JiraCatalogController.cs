using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using ToolboxWeb.Web.Constants;
using ToolboxWeb.Web.Infrastructure.Hosting;
using ToolboxWeb.Web.Infrastructure.Jira;
using ToolboxWeb.Web.Services;
using ToolboxWeb.Web.ViewModels.Jira;

namespace ToolboxWeb.Web.Controllers;

/// <summary>
/// Catalogs (/catalogs.html, Jira group): reference data read from Jira only when the user asks
/// (Services/JiraCatalogService.cs). The page itself is drawn by Content/js/jira-catalog-page.js
/// from GET State, so the web copy, whose catalogs live in the browser, uses the same page.
/// </summary>
[Authorize(Policy = "JiraOnly")]
public class JiraCatalogController : Controller
{
    private readonly IJiraAuthService _jiraAuth;
    private readonly IJiraCatalogService _catalogs;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly AppModeInfo _mode;
    private readonly IDesktopJiraService _desktopJira;

    public JiraCatalogController(
        IJiraAuthService jiraAuth,
        IJiraCatalogService catalogs,
        IStringLocalizer<SharedResource> localizer,
        AppModeInfo mode,
        IDesktopJiraService desktopJira)
    {
        _jiraAuth = jiraAuth;
        _catalogs = catalogs;
        _localizer = localizer;
        _mode = mode;
        _desktopJira = desktopJira;
    }

    [HttpGet("/" + ToolboxRouteSlugs.Pages.JiraCatalog)]
    [HttpGet("/" + ToolboxRouteSlugs.HtmlPages.JiraCatalog, Name = ToolboxRouteSlugs.RouteNames.JiraCatalogHtml)]
    public async Task<IActionResult> Index()
    {
        var session = await _jiraAuth.GetCurrentSessionAsync(HttpContext);
        if (session is null)
        {
            return await NoSessionAsync();
        }

        return View(model: session.BaseUrl);
    }

    [HttpGet("/JiraCatalog/State")]
    public async Task<IActionResult> State()
    {
        var session = await _jiraAuth.GetCurrentSessionAsync(HttpContext);
        if (session is null)
        {
            return Json(JiraApiResponse<object>.LoginRequired(_localizer["Jira.SessionExpired"].Value));
        }

        return Json(JiraApiResponse<object>.Ok(await _catalogs.GetStateAsync(session.BaseUrl, HttpContext.RequestAborted)));
    }

    [HttpPost("/JiraCatalog/Sync")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sync([FromBody] JiraCatalogSyncRequest? request)
    {
        var session = await _jiraAuth.GetCurrentSessionAsync(HttpContext);
        if (session is null)
        {
            return Json(JiraApiResponse<object>.LoginRequired(_localizer["Jira.SessionExpired"].Value));
        }

        if (request?.Kinds is not { Count: > 0 } || !request.Kinds.All(JiraCatalogKinds.IsKnown))
        {
            return Json(JiraApiResponse<object>.Fail(_localizer["Jira.InvalidRequest"].Value));
        }

        return Json(JiraApiResponse<object>.Ok(await _catalogs.SyncAsync(session, request, HttpContext.RequestAborted)));
    }

    [HttpPost("/JiraCatalog/Settings")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Settings([FromBody] JiraCatalogSettings? settings)
    {
        var session = await _jiraAuth.GetCurrentSessionAsync(HttpContext);
        if (session is null)
        {
            return Json(JiraApiResponse<object>.LoginRequired(_localizer["Jira.SessionExpired"].Value));
        }

        if (settings is null)
        {
            return Json(JiraApiResponse<object>.Fail(_localizer["Jira.InvalidRequest"].Value));
        }

        return Json(JiraApiResponse<object>.Ok(await _catalogs.SaveSettingsAsync(session.BaseUrl, settings, HttpContext.RequestAborted)));
    }

    // Same answers as the other Jira screens: a desktop goes to Settings, the other copies to the Jira sign-in.
    private async Task<IActionResult> NoSessionAsync()
    {
        var returnUrl = $"{Request.Path}{Request.QueryString}";
        if (_mode.IsDesktop)
        {
            var status = _desktopJira.GetStatus();
            if (status.Configured)
            {
                TempData[TempDataKeys.ErrorMessage] = status.LastError ?? _localizer["Settings.Jira.Unreachable"].Value;
            }
            else
            {
                TempData[TempDataKeys.InfoMessage] = _localizer["Settings.Jira.Required"].Value;
            }

            return RedirectToRoute(ToolboxRouteSlugs.RouteNames.SettingsHtml, new { returnUrl });
        }

        await _jiraAuth.SignOutAsync(HttpContext);
        TempData[TempDataKeys.ErrorMessage] = _localizer["Jira.SessionExpired"].Value;
        return RedirectToPage("/Account/Login", new { area = "Identity", returnUrl });
    }
}
