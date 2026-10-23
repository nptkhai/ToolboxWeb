using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToolboxWeb.Web.Services;

namespace ToolboxWeb.Web.Controllers;

/// <summary>Data search for the command palette (Ctrl+K, Content/js/command-palette.js).</summary>
[Authorize]
public class SearchController : Controller
{
    private readonly IQuickSearchService _search;
    private readonly ICurrentUserService _currentUser;

    public SearchController(IQuickSearchService search, ICurrentUserService currentUser)
    {
        _search = search;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> Mine(string? q)
    {
        var query = (q ?? string.Empty).Trim();
        if (query.Length > 100)
        {
            query = query[..100];
        }

        return Json(new { items = await _search.SearchAsync(_currentUser.UserId, query, Url) });
    }
}
