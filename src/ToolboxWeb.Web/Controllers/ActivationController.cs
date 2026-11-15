using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using ToolboxWeb.Web.Constants;
using ToolboxWeb.Web.Enums;
using ToolboxWeb.Web.Infrastructure.Hosting;
using ToolboxWeb.Web.Services;
using ToolboxWeb.Web.ViewModels;

namespace ToolboxWeb.Web.Controllers;

/// <summary>
/// Desktop copy only: the activation screen. A license is asked for online (sent to the license
/// server, then polled until the owner approves) or by hand (copy the token, paste the license).
/// It stays reachable while the license is missing or has ended (DesktopLicenseMiddleware).
/// </summary>
[NotInWebMode]
[Authorize]
[AllowWhenUnlicensed]
public class ActivationController : Controller
{
    private readonly AppModeInfo _mode;
    private readonly IDeviceLicenseService _licenses;
    private readonly IDeviceIdentityService _identity;
    private readonly IActivityLogService _activityLog;
    private readonly ICurrentUserService _currentUser;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public ActivationController(
        AppModeInfo mode,
        IDeviceLicenseService licenses,
        IDeviceIdentityService identity,
        IActivityLogService activityLog,
        ICurrentUserService currentUser,
        IStringLocalizer<SharedResource> localizer)
    {
        _mode = mode;
        _licenses = licenses;
        _identity = identity;
        _activityLog = activityLog;
        _currentUser = currentUser;
        _localizer = localizer;
    }

    [HttpGet("/" + ToolboxRouteSlugs.HtmlPages.Activate, Name = ToolboxRouteSlugs.RouteNames.ActivateHtml)]
    public IActionResult Index()
    {
        return _mode.IsDesktop ? View(Build()) : NotFound();
    }

    /// <summary>Sends the activation request to the license server.</summary>
    [HttpPost("/activate/request")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendRequest()
    {
        if (!_mode.IsDesktop)
        {
            return NotFound();
        }

        var (result, change) = await _licenses.RequestActivationAsync(HttpContext.RequestAborted);
        if (!result.Succeeded)
        {
            TempData[TempDataKeys.ErrorMessage] = Message(result);
            return RedirectToAction(nameof(Index));
        }

        if (await LogAsync(change))
        {
            return RedirectToApp();
        }

        TempData[TempDataKeys.InfoMessage] = _localizer["Activation.Sent"].Value;
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Polled by activation.js while a request waits: asks the server, answers whether the app can open.</summary>
    [HttpGet("/activate/status")]
    public async Task<IActionResult> Status()
    {
        if (!_mode.IsDesktop)
        {
            return NotFound();
        }

        var (result, change) = await _licenses.PollAsync(HttpContext.RequestAborted);
        await LogAsync(change);
        var status = result.Status;
        if (status.State == DeviceLicenseState.Active)
        {
            // The page opens the app next; it shows this there.
            TempData[TempDataKeys.SuccessMessage] = _localizer["Activation.Done"].Value;
        }

        return Json(new
        {
            state = status.State.ToString(),
            activation = status.ActivationStatus,
            active = status.State == DeviceLicenseState.Active,
            message = result.Succeeded ? null : Message(result)
        });
    }

    /// <summary>A license pasted by hand.</summary>
    [HttpPost("/activate/license")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Apply(string? licenseText)
    {
        if (!_mode.IsDesktop)
        {
            return NotFound();
        }

        var before = _licenses.GetStatus().State;
        var result = _licenses.ApplyLicenseText(licenseText);
        if (!result.Succeeded)
        {
            var model = Build();
            model.LicenseText = licenseText;
            model.LicenseError = Message(result);
            return View(nameof(Index), model);
        }

        await LogAsync(before == DeviceLicenseState.Active ? LicenseChange.Renewed : LicenseChange.Activated);
        return RedirectToApp();
    }

    private ActivationPageViewModel Build()
    {
        var identity = _identity.Get();
        return new ActivationPageViewModel
        {
            Status = _licenses.GetStatus(),
            DeviceId = identity.DeviceId,
            MachineName = identity.MachineName,
            Token = _identity.CreateToken()
        };
    }

    /// <returns>True when the license is now active.</returns>
    private async Task<bool> LogAsync(LicenseChange change)
    {
        if (change is LicenseChange.Activated or LicenseChange.Renewed or LicenseChange.Revoked)
        {
            await _activityLog.LogAsync(_currentUser.UserId, ActivityActionType.Update, ActivityEntityType.License,
                _licenses.GetStatus().LicenseId, _localizer[$"License.Log.{change}"].Value);
        }

        return change is LicenseChange.Activated or LicenseChange.Renewed && _licenses.GetStatus().State == DeviceLicenseState.Active;
    }

    private IActionResult RedirectToApp()
    {
        TempData[TempDataKeys.SuccessMessage] = _localizer["Activation.Done"].Value;
        return Redirect("/");
    }

    private string Message(LicenseActionResult result)
    {
        var text = _localizer[result.ErrorKey!, result.ErrorArgument ?? string.Empty];
        return text.ResourceNotFound ? _localizer["License.Error.Unknown", result.ErrorKey!].Value : text.Value;
    }
}
