using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using ToolboxWeb.Web;
using ToolboxWeb.Web.Constants;
using ToolboxWeb.Web.Infrastructure.Time;
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
    public async Task<IActionResult> Index(string? filter, string? search)
    {
        return View(await BuildPageAsync(filter, search, new ChecklistItemFormViewModel(), openCreateForm: false));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ChecklistItemFormViewModel form, string? filter, string? search)
    {
        if (!ModelState.IsValid)
        {
            return View("Index", await BuildPageAsync(filter, search, form, openCreateForm: true));
        }

        await _checklist.CreateAsync(_currentUser.UserId, form);
        TempData[TempDataKeys.SuccessMessage] = _localizer["Flash.TaskCreated"].Value;
        return RedirectToList(filter, search);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id, string? filter, string? search)
    {
        await _checklist.ToggleAsync(_currentUser.UserId, id);
        return RedirectToList(filter, search);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, string? filter, string? search)
    {
        await _checklist.DeleteAsync(_currentUser.UserId, id);
        return RedirectToList(filter, search);
    }

    private async Task<ChecklistIndexViewModel> BuildPageAsync(string? filter, string? search, ChecklistItemFormViewModel form, bool openCreateForm)
    {
        var list = await _checklist.GetListAsync(_currentUser.UserId, filter, search);
        return new ChecklistIndexViewModel
        {
            Items = list.Items,
            Counts = list.Counts,
            HasAnyItems = list.HasAnyItems,
            Filter = list.Filter,
            Search = list.Search,
            Form = form,
            OpenCreateForm = openCreateForm,
            Today = AppClock.Today
        };
    }

    // Toggling or deleting keeps the filter and search the user was looking at.
    private IActionResult RedirectToList(string? filter, string? search)
    {
        var normalized = ChecklistFilters.Normalize(filter);
        var url = Url.RouteUrl(ToolboxRouteSlugs.RouteNames.ChecklistHtml, new
        {
            filter = normalized == ChecklistFilters.All ? null : normalized,
            search = string.IsNullOrWhiteSpace(search) ? null : search.Trim()
        });
        return Redirect(url ?? "/checklist.html");
    }
}
