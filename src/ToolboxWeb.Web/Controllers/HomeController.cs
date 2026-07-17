using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ToolboxWeb.Web.Constants;
using ToolboxWeb.Web.Models;

namespace ToolboxWeb.Web.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return Redirect(Url.RouteUrl(ToolboxRouteSlugs.RouteNames.DashboardHtml) ?? "/dashboard.html");
        }

        return Redirect("/Identity/Account/Login");
    }

    [HttpGet("/" + ToolboxRouteSlugs.Pages.Privacy)]
    [HttpGet("/" + ToolboxRouteSlugs.HtmlPages.Privacy, Name = ToolboxRouteSlugs.RouteNames.PrivacyHtml)]
    public IActionResult Privacy()
    {
        return View();
    }

    [HttpGet("/" + ToolboxRouteSlugs.Pages.Error)]
    [HttpGet("/" + ToolboxRouteSlugs.HtmlPages.Error, Name = ToolboxRouteSlugs.RouteNames.ErrorHtml)]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
