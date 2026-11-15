using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using ToolboxWeb.Web.Infrastructure.Hosting;

namespace ToolboxWeb.Web.Controllers;

/// <summary>
/// App-wide endpoints for the installed app (PWA) in every run mode: the running version, which
/// open pages poll to offer a reload after a deployment, and the page the service worker shows
/// when there is no network (Content/sw.js).
/// </summary>
[AllowAnonymous]
public class AppController : Controller
{
    [HttpGet("/app/version")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Version()
    {
        return Json(new { version = AppVersion.Version, build = AppVersion.Build });
    }

    /// <summary>
    /// Stored once by the service worker, then shown in whatever language the person uses, so it
    /// carries the text in both languages and picks one in the browser.
    /// </summary>
    [HttpGet("/offline.html")]
    public IActionResult Offline([FromServices] IStringLocalizer<SharedResource> localizer)
    {
        OfflineText Read(string culture)
        {
            var previous = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentUICulture = new CultureInfo(culture);
                return new OfflineText(culture, localizer["Offline.Title"].Value, localizer["Offline.Text"].Value, localizer["Offline.Retry"].Value);
            }
            finally
            {
                CultureInfo.CurrentUICulture = previous;
            }
        }

        return View(new[] { Read("vi-VN"), Read("en-US") });
    }
}

public sealed record OfflineText(string Culture, string Title, string Text, string Retry);
