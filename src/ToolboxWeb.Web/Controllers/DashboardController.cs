using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToolboxWeb.Web.Constants;
using ToolboxWeb.Web.Services;

namespace ToolboxWeb.Web.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly IDashboardService _dashboard;
    private readonly ICurrentUserService _currentUser;

    public DashboardController(IDashboardService dashboard, ICurrentUserService currentUser)
    {
        _dashboard = dashboard;
        _currentUser = currentUser;
    }

    [HttpGet("/" + ToolboxRouteSlugs.Pages.Dashboard)]
    [HttpGet("/" + ToolboxRouteSlugs.HtmlPages.Dashboard, Name = ToolboxRouteSlugs.RouteNames.DashboardHtml)]
    public async Task<IActionResult> Index()
    {
        return View(await _dashboard.BuildAsync(_currentUser.UserId));
    }
}
