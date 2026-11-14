using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToolboxWeb.Web.Infrastructure.Hosting;

namespace ToolboxWeb.Web.Controllers;

/// <summary>
/// Desktop copy only (App:Mode=Desktop): the "is Toolkit running here?" check used by a second
/// start, and "Quit Toolkit" from the account menu. Not found on the server.
/// </summary>
[Route("desktop")]
public class DesktopController : Controller
{
    private readonly AppModeInfo _mode;
    private readonly IHostApplicationLifetime _lifetime;

    public DesktopController(AppModeInfo mode, IHostApplicationLifetime lifetime)
    {
        _mode = mode;
        _lifetime = lifetime;
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
}
