using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using ToolboxWeb.Web;
using ToolboxWeb.Web.Constants;
using ToolboxWeb.Web.Services;
using ToolboxWeb.Web.ViewModels;

namespace ToolboxWeb.Web.Controllers;

[Authorize]
public class ChecklistController : Controller
{
    private readonly IChecklistService _checklist;
    private readonly ICurrentUserService _currentUser;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public ChecklistController(IChecklistService checklist, ICurrentUserService currentUser, IStringLocalizer<SharedResource> localizer)
    {
        _checklist = checklist;
        _currentUser = currentUser;
        _localizer = localizer;
    }

    [HttpGet("/" + ToolboxRouteSlugs.Pages.Checklist)]
    [HttpGet("/" + ToolboxRouteSlugs.HtmlPages.Checklist, Name = ToolboxRouteSlugs.RouteNames.ChecklistHtml)]
    public async Task<IActionResult> Index()
    {
        return View(new ChecklistIndexViewModel { Items = await _checklist.GetAsync(_currentUser.UserId) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ChecklistItemFormViewModel form)
    {
        if (!ModelState.IsValid)
        {
            return View("Index", new ChecklistIndexViewModel { Items = await _checklist.GetAsync(_currentUser.UserId), Form = form });
        }

        await _checklist.CreateAsync(_currentUser.UserId, form);
        TempData[TempDataKeys.SuccessMessage] = _localizer["Flash.TaskCreated"].Value;
        return Redirect(Url.RouteUrl(ToolboxRouteSlugs.RouteNames.ChecklistHtml) ?? "/checklist.html");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id)
    {
        await _checklist.ToggleAsync(_currentUser.UserId, id);
        return Redirect(Url.RouteUrl(ToolboxRouteSlugs.RouteNames.ChecklistHtml) ?? "/checklist.html");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _checklist.DeleteAsync(_currentUser.UserId, id);
        return Redirect(Url.RouteUrl(ToolboxRouteSlugs.RouteNames.ChecklistHtml) ?? "/checklist.html");
    }
}
