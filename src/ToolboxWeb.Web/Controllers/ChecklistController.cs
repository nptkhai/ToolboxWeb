using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using ToolboxWeb.Web;
using ToolboxWeb.Web.Constants;
using ToolboxWeb.Web.Infrastructure.Time;
using ToolboxWeb.Web.Services;
using ToolboxWeb.Web.ViewModels;

namespace ToolboxWeb.Web.Controllers;

/// <summary>
/// The to-do list (stage 5.6). Filter, search and sort live in the query string and every write
/// posts them back, so the list comes back as it was (PRG). Completing and deleting offer "Undo"
/// until the next page load.
/// </summary>
[Authorize]
public class ChecklistController : Controller
{
    // The sort order is remembered between visits.
    private const string SortCookie = "toolbox.checklist.sort";
    private const string UndoKey = "ChecklistUndo";

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
    public async Task<IActionResult> Index(string? filter, string? search, string? sort)
    {
        var chosen = ChecklistSorts.Normalize(sort);
        if (chosen is not null)
        {
            Response.Cookies.Append(SortCookie, chosen, new CookieOptions { MaxAge = TimeSpan.FromDays(365), IsEssential = true, HttpOnly = true, SameSite = SameSiteMode.Lax });
        }

        return View(await BuildPageAsync(Query(filter, search, sort), new ChecklistItemFormViewModel(), openForm: false));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ChecklistItemFormViewModel form, string? filter, string? search, string? sort)
    {
        if (!ModelState.IsValid)
        {
            return View("Index", await BuildPageAsync(Query(filter, search, sort), form, openForm: true));
        }

        await _checklist.CreateAsync(_currentUser.UserId, form);
        TempData[TempDataKeys.SuccessMessage] = _localizer["Flash.TaskCreated"].Value;
        return RedirectToList(filter, search, sort);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(ChecklistItemFormViewModel form, string? filter, string? search, string? sort)
    {
        if (!ModelState.IsValid || form.Id is null)
        {
            return View("Index", await BuildPageAsync(Query(filter, search, sort), form, openForm: true));
        }

        var updated = await _checklist.UpdateAsync(_currentUser.UserId, form);
        TempData[updated ? TempDataKeys.SuccessMessage : TempDataKeys.ErrorMessage] =
            _localizer[updated ? "Flash.TaskUpdated" : "Checklist.NotFound"].Value;
        return RedirectToList(filter, search, sort);
    }

    /// <summary>The round check box: done marks it done (with Undo), a done item reopens.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id, bool done, string? filter, string? search, string? sort)
    {
        if (done)
        {
            await _checklist.ToggleAsync(_currentUser.UserId, id);
        }
        else
        {
            SetUndo(ChecklistUndoKinds.Complete, await _checklist.CompleteAsync(_currentUser.UserId, [id]), "Checklist.Undo.Completed");
        }

        return RedirectToList(filter, search, sort);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetDue(int id, string? due, string? filter, string? search, string? sort)
    {
        await _checklist.SetDueAsync(_currentUser.UserId, id, due ?? string.Empty);
        return RedirectToList(filter, search, sort);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, string? filter, string? search, string? sort)
    {
        SetUndo(ChecklistUndoKinds.Delete, await _checklist.DeleteAsync(_currentUser.UserId, [id]), "Checklist.Undo.Deleted");
        return RedirectToList(filter, search, sort);
    }

    /// <summary>Several selected items at once: "complete" or "delete".</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Bulk(string? operation, int[]? ids, string? filter, string? search, string? sort)
    {
        var selected = ids ?? [];
        if (operation == ChecklistUndoKinds.Delete)
        {
            SetUndo(ChecklistUndoKinds.Delete, await _checklist.DeleteAsync(_currentUser.UserId, selected), "Checklist.Undo.Deleted");
        }
        else if (operation == ChecklistUndoKinds.Complete)
        {
            SetUndo(ChecklistUndoKinds.Complete, await _checklist.CompleteAsync(_currentUser.UserId, selected), "Checklist.Undo.Completed");
        }

        return RedirectToList(filter, search, sort);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClearDone(string? filter, string? search, string? sort)
    {
        SetUndo(ChecklistUndoKinds.Delete, await _checklist.ClearDoneAsync(_currentUser.UserId), "Checklist.Undo.Cleared");
        return RedirectToList(filter, search, sort);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Undo(string? kind, int[]? ids, string? filter, string? search, string? sort)
    {
        var restored = await _checklist.UndoAsync(_currentUser.UserId, kind ?? string.Empty, ids ?? []);
        if (restored > 0)
        {
            TempData[TempDataKeys.SuccessMessage] = _localizer["Checklist.Undo.Done", restored].Value;
        }

        return RedirectToList(filter, search, sort);
    }

    private ChecklistListQuery Query(string? filter, string? search, string? sort)
    {
        var chosen = ChecklistSorts.Normalize(sort) ?? ChecklistSorts.Normalize(Request.Cookies[SortCookie]) ?? ChecklistSorts.Due;
        return new ChecklistListQuery(ChecklistFilters.Normalize(filter), search, chosen);
    }

    private async Task<ChecklistIndexViewModel> BuildPageAsync(ChecklistListQuery query, ChecklistItemFormViewModel form, bool openForm)
    {
        var list = await _checklist.GetListAsync(_currentUser.UserId, query);
        return new ChecklistIndexViewModel
        {
            Groups = list.Groups,
            Counts = list.Counts,
            Week = list.Week,
            HasAnyItems = list.HasAnyItems,
            Query = list.Query,
            Form = form,
            OpenForm = openForm,
            Today = AppClock.Today,
            Undo = ReadUndo()
        };
    }

    private void SetUndo(string kind, IReadOnlyList<int> ids, string messageKey)
    {
        if (ids.Count == 0)
        {
            return;
        }

        TempData[UndoKey] = JsonSerializer.Serialize(new ChecklistUndoViewModel(kind, ids, _localizer[messageKey, ids.Count].Value));
    }

    private ChecklistUndoViewModel? ReadUndo()
    {
        try
        {
            return TempData[UndoKey] is string json ? JsonSerializer.Deserialize<ChecklistUndoViewModel>(json) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // Every write keeps the filter, search and sort the user was looking at.
    private IActionResult RedirectToList(string? filter, string? search, string? sort)
    {
        var query = Query(filter, search, sort);
        var url = Url.RouteUrl(ToolboxRouteSlugs.RouteNames.ChecklistHtml, new
        {
            filter = query.Filter == ChecklistFilters.All ? null : query.Filter,
            search = string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
            sort = ChecklistSorts.Normalize(sort)
        });
        return Redirect(url ?? "/checklist.html");
    }
}
