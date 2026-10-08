using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using ToolboxWeb.Web;
using ToolboxWeb.Web.Constants;
using ToolboxWeb.Web.Services;
using ToolboxWeb.Web.ViewModels;

namespace ToolboxWeb.Web.Controllers;

/// <summary>
/// Server-rendered notes page. What is on screen lives in the query string
/// (<c>?format=js&amp;search=…&amp;id=12</c>), so every write is a POST that redirects back to
/// the same view (PRG) and refresh, back and bookmarks all behave.
/// </summary>
[Authorize]
public class NotesController : Controller
{
    private readonly INoteService _notes;
    private readonly ICurrentUserService _currentUser;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public NotesController(INoteService notes, ICurrentUserService currentUser, IStringLocalizer<SharedResource> localizer)
    {
        _notes = notes;
        _currentUser = currentUser;
        _localizer = localizer;
    }

    [HttpGet("/" + ToolboxRouteSlugs.Pages.Notes)]
    [HttpGet("/" + ToolboxRouteSlugs.HtmlPages.Notes, Name = ToolboxRouteSlugs.RouteNames.NotesHtml)]
    public async Task<IActionResult> Index(string? format = null, string? search = null, int? id = null)
    {
        return View(await _notes.GetPageAsync(_currentUser.UserId, new NotePageQuery(format, search, id)));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(NoteFormViewModel form)
    {
        if (!ModelState.IsValid)
        {
            if (form.ParentId is null)
            {
                // Reopen the dialog with what was typed instead of throwing it away.
                var page = await _notes.GetPageAsync(_currentUser.UserId, new NotePageQuery(form.ReturnFormat, form.ReturnSearch, null));
                page.Form = form;
                page.OpenCreateModal = true;
                return View("Index", page);
            }

            TempData[TempDataKeys.ErrorMessage] = FirstError();
            return RedirectToNotes(form.ReturnFormat, form.ReturnSearch, form.ParentId);
        }

        var result = await _notes.CreateAsync(_currentUser.UserId, form);
        if (!result.Succeeded)
        {
            TempData[TempDataKeys.ErrorMessage] = result.Error;
            return RedirectToNotes(form.ReturnFormat, form.ReturnSearch, form.ParentId);
        }

        if (form.ParentId is null)
        {
            // A new group may not match the filter the user was on; drop it so the note is
            // visible in the list as well as in the detail pane.
            TempData[TempDataKeys.SuccessMessage] = _localizer["Flash.NoteCreated"].Value;
            return RedirectToNotes(null, null, result.GroupId);
        }

        TempData[TempDataKeys.SuccessMessage] = _localizer["Flash.NoteChildCreated"].Value;
        return RedirectToNotes(form.ReturnFormat, form.ReturnSearch, result.GroupId, result.NoteId);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(NoteFormViewModel form)
    {
        if (!ModelState.IsValid)
        {
            TempData[TempDataKeys.ErrorMessage] = FirstError();
            return RedirectToNotes(form.ReturnFormat, form.ReturnSearch, form.ParentId ?? form.Id);
        }

        var result = await _notes.UpdateAsync(_currentUser.UserId, form);
        if (!result.Succeeded)
        {
            TempData[TempDataKeys.ErrorMessage] = result.Error;
            return RedirectToNotes(form.ReturnFormat, form.ReturnSearch, null);
        }

        TempData[TempDataKeys.SuccessMessage] = _localizer["Flash.NoteUpdated"].Value;
        var focusChild = result.NoteId != result.GroupId ? result.NoteId : null;
        return RedirectToNotes(form.ReturnFormat, form.ReturnSearch, result.GroupId, focusChild);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, string? returnFormat = null, string? returnSearch = null)
    {
        var result = await _notes.DeleteAsync(_currentUser.UserId, id);
        if (result is null)
        {
            TempData[TempDataKeys.ErrorMessage] = _localizer["Notes.NotFound"].Value;
            return RedirectToNotes(returnFormat, returnSearch, null);
        }

        TempData[TempDataKeys.SuccessMessage] = _localizer["Flash.NoteDeleted"].Value;
        return RedirectToNotes(returnFormat, returnSearch, result.GroupIdToShow);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TogglePin(int id, string? returnFormat = null, string? returnSearch = null)
    {
        var pinned = await _notes.TogglePinAsync(_currentUser.UserId, id);
        if (pinned is null)
        {
            TempData[TempDataKeys.ErrorMessage] = _localizer["Notes.NotFound"].Value;
        }

        return RedirectToNotes(returnFormat, returnSearch, pinned is null ? null : id);
    }

    /// <summary>
    /// Returns a stored server password on explicit request. Never cached, and never part of
    /// the rendered page.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RevealSecret(int id)
    {
        Response.Headers.CacheControl = "no-store";

        var result = await _notes.RevealServerPasswordAsync(_currentUser.UserId, id);
        return result.Status switch
        {
            NoteSecretStatus.Found => Json(new { success = true, password = result.Password }),
            NoteSecretStatus.NoPassword => Json(new { success = false, message = _localizer["Notes.Server.PasswordNone"].Value }),
            NoteSecretStatus.DecryptFailed => Json(new { success = false, message = _localizer["Notes.Server.DecryptFailed"].Value }),
            _ => NotFound()
        };
    }

    private IActionResult RedirectToNotes(string? format, string? search, int? id, int? focusChildId = null)
    {
        var url = Url.RouteUrl(ToolboxRouteSlugs.RouteNames.NotesHtml, new
        {
            format = string.IsNullOrWhiteSpace(format) ? null : format,
            search = string.IsNullOrWhiteSpace(search) ? null : search,
            id
        }) ?? "/notes.html";

        if (focusChildId is int childId)
        {
            url += "#child-" + childId;
        }

        return Redirect(url);
    }

    private string FirstError() =>
        ModelState.Values
            .SelectMany(entry => entry.Errors)
            .Select(error => error.ErrorMessage)
            .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message))
        ?? _localizer["Notes.SaveFailed"].Value;
}
