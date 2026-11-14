using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using ToolboxWeb.Web.Constants;
using ToolboxWeb.Web.Enums;
using ToolboxWeb.Web.Infrastructure.Hosting;
using ToolboxWeb.Web.Services;
using ToolboxWeb.Web.ViewModels;
using ToolboxWeb.Web.ViewModels.Jira;

namespace ToolboxWeb.Web.Controllers;

/// <summary>
/// Settings of a desktop copy (App:Mode=Desktop): what belongs to this computer, starting with
/// the Jira connection (DesktopJiraService). Not found on the server, where Jira is signed in per session.
/// </summary>
[Authorize]
public class SettingsController : Controller
{
    private readonly AppModeInfo _mode;
    private readonly IDesktopJiraService _jira;
    private readonly IActivityLogService _activityLog;
    private readonly ICurrentUserService _currentUser;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly JiraOptions _jiraOptions;

    public SettingsController(
        AppModeInfo mode,
        IDesktopJiraService jira,
        IActivityLogService activityLog,
        ICurrentUserService currentUser,
        IStringLocalizer<SharedResource> localizer,
        IOptions<JiraOptions> jiraOptions)
    {
        _mode = mode;
        _jira = jira;
        _activityLog = activityLog;
        _currentUser = currentUser;
        _localizer = localizer;
        _jiraOptions = jiraOptions.Value;
    }

    [HttpGet("/" + ToolboxRouteSlugs.HtmlPages.Settings, Name = ToolboxRouteSlugs.RouteNames.SettingsHtml)]
    public IActionResult Index(string? returnUrl = null)
    {
        if (!_mode.IsDesktop)
        {
            return NotFound();
        }

        var status = _jira.GetStatus();
        return View(new SettingsPageViewModel
        {
            Jira = status,
            Form = new JiraSettingsForm
            {
                BaseUrl = status.BaseUrl ?? _jiraOptions.DefaultBaseUrl,
                Kind = status.Kind,
                Username = status.Kind == JiraSignInKind.Password ? status.Username : null
            },
            ReturnUrl = LocalOrNull(returnUrl)
        });
    }

    /// <summary>Tries the form's values without saving them (the "Test connection" button).</summary>
    [HttpPost("/settings/jira/test")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestJira(JiraSettingsForm form, string? secret)
    {
        if (!_mode.IsDesktop)
        {
            return NotFound();
        }

        var result = await _jira.TestAsync(ToInput(form, secret), HttpContext.RequestAborted);
        return Json(new { ok = result.Succeeded, message = result.Message });
    }

    [HttpPost("/settings/jira")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveJira(JiraSettingsForm form, string? secret, string? returnUrl = null)
    {
        if (!_mode.IsDesktop)
        {
            return NotFound();
        }

        var result = await _jira.SaveAsync(ToInput(form, secret), HttpContext.RequestAborted);
        if (!result.Succeeded)
        {
            return View("Index", new SettingsPageViewModel
            {
                Jira = _jira.GetStatus(),
                Form = form,
                FormError = result.Message,
                ReturnUrl = LocalOrNull(returnUrl)
            });
        }

        var status = _jira.GetStatus();
        await _activityLog.LogAsync(_currentUser.UserId, ActivityActionType.Update, ActivityEntityType.UserProfile, null,
            _localizer["ActivityLog.JiraConnectedSummary", status.BaseUrl ?? string.Empty, result.Username ?? string.Empty].Value);
        TempData[TempDataKeys.SuccessMessage] = result.Message;
        return LocalOrNull(returnUrl) is { } back
            ? LocalRedirect(back)
            : RedirectToRoute(ToolboxRouteSlugs.RouteNames.SettingsHtml);
    }

    [HttpPost("/settings/jira/remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveJira()
    {
        if (!_mode.IsDesktop)
        {
            return NotFound();
        }

        var baseUrl = _jira.GetStatus().BaseUrl;
        if (baseUrl is not null)
        {
            await _jira.RemoveAsync();
            await _activityLog.LogAsync(_currentUser.UserId, ActivityActionType.Delete, ActivityEntityType.UserProfile, null,
                _localizer["ActivityLog.JiraDisconnectedSummary", baseUrl].Value);
            TempData[TempDataKeys.SuccessMessage] = _localizer["Settings.Jira.Removed"].Value;
        }

        return RedirectToRoute(ToolboxRouteSlugs.RouteNames.SettingsHtml);
    }

    private static DesktopJiraInput ToInput(JiraSettingsForm form, string? secret) =>
        new(form.BaseUrl, form.Kind, form.Username, secret);

    private string? LocalOrNull(string? url) =>
        !string.IsNullOrWhiteSpace(url) && Url.IsLocalUrl(url) ? url : null;
}
