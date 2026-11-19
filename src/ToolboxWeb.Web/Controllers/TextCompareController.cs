using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToolboxWeb.Web.Constants;
using ToolboxWeb.Web.Services;
using ToolboxWeb.Web.ViewModels;

namespace ToolboxWeb.Web.Controllers;

/// <summary>
/// Compare text. Everything happens in the browser (Content/js/text-compare.js): the two texts
/// and the last comparisons never reach the server, so the page works the same on the IIS site,
/// the desktop and the web copy, and needs no license check for writing.
/// </summary>
[Authorize]
public class TextCompareController : Controller
{
    private readonly ICurrentUserService _currentUser;

    public TextCompareController(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    [HttpGet("/" + ToolboxRouteSlugs.Pages.TextCompare)]
    [HttpGet("/" + ToolboxRouteSlugs.HtmlPages.TextCompare, Name = ToolboxRouteSlugs.RouteNames.TextCompareHtml)]
    public IActionResult Index()
    {
        return View(new TextComparePageViewModel
        {
            RecentStorageKey = "toolbox.textCompare.recent.v1:" + UserKey(_currentUser.UserId)
        });
    }

    // A short hash rather than the account id itself: enough to keep two accounts on one browser apart.
    private static string UserKey(string userId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(userId ?? string.Empty));
        return Convert.ToHexString(hash, 0, 8).ToLowerInvariant();
    }
}
