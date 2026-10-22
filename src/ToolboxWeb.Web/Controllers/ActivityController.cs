using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToolboxWeb.Web.Constants;
using ToolboxWeb.Web.Enums;
using ToolboxWeb.Web.Services;

namespace ToolboxWeb.Web.Controllers;

/// <summary>Every activity entry of the user, newest first, with a kind filter ("See all" on the overview).</summary>
[Authorize]
public class ActivityController : Controller
{
    private readonly IActivityLogService _activity;
    private readonly ICurrentUserService _currentUser;

    public ActivityController(IActivityLogService activity, ICurrentUserService currentUser)
    {
        _activity = activity;
        _currentUser = currentUser;
    }

    [HttpGet("/" + ToolboxRouteSlugs.Pages.Activity)]
    [HttpGet("/" + ToolboxRouteSlugs.HtmlPages.Activity, Name = ToolboxRouteSlugs.RouteNames.ActivityHtml)]
    public async Task<IActionResult> Index(ActivityEntityType? type, int page = 1)
    {
        var entityType = type is { } value && Enum.IsDefined(value) ? value : (ActivityEntityType?)null;
        return View(await _activity.GetPageAsync(_currentUser.UserId, entityType, page));
    }
}
