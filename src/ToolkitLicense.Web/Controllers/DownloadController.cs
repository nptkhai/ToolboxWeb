using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToolkitLicense.Web.Infrastructure;
using ToolkitLicense.Web.Services;
using ToolkitLicense.Web.ViewModels;

namespace ToolkitLicense.Web.Controllers;

/// <summary>
/// The public "Download Toolkit" page: the newest release, the installer from the updates folder,
/// and how to install and activate it. Open to anyone; it shows nothing about devices or users.
/// </summary>
[AllowAnonymous]
public sealed class DownloadController(ReleaseService releases, UpdateFiles updateFiles) : Controller
{
    [HttpGet("/download")]
    public async Task<IActionResult> Index() => View(new DownloadViewModel
    {
        Latest = await releases.LatestAsync(),
        Setup = updateFiles.Setup
    });
}
