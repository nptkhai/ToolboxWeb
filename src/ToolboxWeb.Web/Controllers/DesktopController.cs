using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToolboxWeb.Web.Constants;
using ToolboxWeb.Web.Infrastructure.Hosting;
using ToolboxWeb.Web.Services;
using ToolboxWeb.Web.ViewModels;

namespace ToolboxWeb.Web.Controllers;

/// <summary>
/// Desktop copy only (App:Mode=Desktop): the "is Toolkit running here?" check used by a second
/// start, "Quit Toolkit" from the account menu, and updating to a new version
/// (DesktopUpdateService). Not found on the server. Usable on an ended license, so an update
/// can still be installed.
/// </summary>
[NotInWebMode]
[Route("desktop")]
[AllowWhenUnlicensed]
public class DesktopController : Controller
{
    private readonly AppModeInfo _mode;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly IDesktopUpdateService _updates;
    private readonly IDeviceLicenseService _licenses;

    public DesktopController(AppModeInfo mode, IHostApplicationLifetime lifetime, IDesktopUpdateService updates, IDeviceLicenseService licenses)
    {
        _mode = mode;
        _lifetime = lifetime;
        _updates = updates;
        _licenses = licenses;
    }

    /// <summary>The update page: the new version, its notes, and "Update now" (or the download link on a copy that was not installed).</summary>
    [Authorize]
    [HttpGet("/" + ToolboxRouteSlugs.HtmlPages.Update, Name = ToolboxRouteSlugs.RouteNames.UpdateHtml)]
    public IActionResult Update()
    {
        if (!_mode.IsDesktop)
        {
            return NotFound();
        }

        var status = _licenses.GetStatus();
        return View(new DesktopUpdatePageViewModel
        {
            CurrentVersion = AppVersion.Version,
            Release = status.UpdateAvailable ? status.LatestRelease : null,
            Required = status.UpdateRequired,
            Installed = _updates.IsInstalled,
            Progress = _updates.Progress
        });
    }

    [Authorize]
    [HttpPost("update/start")]
    [ValidateAntiForgeryToken]
    public IActionResult StartUpdate()
    {
        if (!_mode.IsDesktop || !_updates.IsInstalled)
        {
            return NotFound();
        }

        _updates.Start();
        return ProgressJson();
    }

    [Authorize]
    [HttpGet("update/status")]
    public IActionResult UpdateStatus()
    {
        return _mode.IsDesktop ? ProgressJson() : NotFound();
    }

    /// <summary>Applies the downloaded update once this answer has reached the page; the app then quits and comes back.</summary>
    [Authorize]
    [HttpPost("update/apply")]
    [ValidateAntiForgeryToken]
    public IActionResult ApplyUpdate()
    {
        if (!_mode.IsDesktop || _updates.Progress.Stage != DesktopUpdateStage.Ready)
        {
            return NotFound();
        }

        Response.OnCompleted(() =>
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(300);
                _updates.ApplyAndRestart();
            });
            return Task.CompletedTask;
        });
        return Json(new { ok = true, build = AppVersion.Build });
    }

    [AllowAnonymous]
    [HttpGet("ping")]
    public IActionResult Ping()
    {
        return _mode.IsDesktop ? Content(DesktopHost.PingReply) : NotFound();
    }

    [Authorize]
    [HttpPost("exit")]
    [ValidateAntiForgeryToken]
    public IActionResult Exit()
    {
        if (!_mode.IsDesktop)
        {
            return NotFound();
        }

        // Stop once the "Toolkit has quit" page has reached the browser.
        Response.OnCompleted(() =>
        {
            _lifetime.StopApplication();
            return Task.CompletedTask;
        });
        return View("Exited");
    }

    private JsonResult ProgressJson()
    {
        var progress = _updates.Progress;
        return Json(new { stage = progress.Stage.ToString(), percent = progress.Percent, version = progress.Version, errorKey = progress.ErrorKey });
    }
}
