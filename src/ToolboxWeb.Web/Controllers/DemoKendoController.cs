using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToolboxWeb.Web.Constants;

namespace ToolboxWeb.Web.Controllers;

[Authorize]
public class DemoKendoController : Controller
{
    [HttpGet("/" + ToolboxRouteSlugs.Pages.KendoDemo + "/{tab?}")]
    [HttpGet("/" + ToolboxRouteSlugs.HtmlPages.KendoDemo, Name = ToolboxRouteSlugs.RouteNames.KendoDemoHtml)]
    [HttpGet("/demo-kendo/{tab}.html", Name = ToolboxRouteSlugs.RouteNames.KendoDemoTabHtml)]
    public IActionResult Index(string? tab = null)
    {
        var activeTab = ToolboxRouteSlugs.KendoDemoTabs.Normalize(tab);
        if (activeTab is null)
        {
            return Redirect(Url.RouteUrl(ToolboxRouteSlugs.RouteNames.KendoDemoHtml) ?? "/demo-kendo.html");
        }

        if (!string.IsNullOrWhiteSpace(tab) && !string.Equals(tab, activeTab, StringComparison.Ordinal))
        {
            return Redirect(Url.RouteUrl(ToolboxRouteSlugs.RouteNames.KendoDemoTabHtml, new { tab = activeTab }) ?? $"/demo-kendo/{activeTab}.html");
        }

        ViewData["ActiveTab"] = activeTab;
        return View();
    }
}
