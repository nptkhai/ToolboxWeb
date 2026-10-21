using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToolboxWeb.Web.Constants;
using ToolboxWeb.Web.Infrastructure.Security;

namespace ToolboxWeb.Web.Controllers;

[Authorize]
[DevToolsOnly]
public class ComponentsController : Controller
{
    [HttpGet("/" + ToolboxRouteSlugs.Pages.Components + "/{tab?}")]
    [HttpGet("/" + ToolboxRouteSlugs.HtmlPages.Components, Name = ToolboxRouteSlugs.RouteNames.ComponentsHtml)]
    [HttpGet("/component/{tab}.html", Name = ToolboxRouteSlugs.RouteNames.ComponentsTabHtml)]
    public IActionResult Index(string? tab = null)
    {
        var activeTab = ToolboxRouteSlugs.ComponentsTabs.Normalize(tab);
        if (activeTab is null)
        {
            return Redirect(Url.RouteUrl(ToolboxRouteSlugs.RouteNames.ComponentsHtml) ?? "/component.html");
        }

        if (!string.IsNullOrWhiteSpace(tab) && !string.Equals(tab, activeTab, StringComparison.Ordinal))
        {
            return Redirect(Url.RouteUrl(ToolboxRouteSlugs.RouteNames.ComponentsTabHtml, new { tab = activeTab }) ?? $"/component/{activeTab}.html");
        }

        ViewData["ActiveTab"] = activeTab;
        return View();
    }
}
