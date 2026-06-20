using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using ToolboxWeb.Web;
using ToolboxWeb.Web.Constants;
using ToolboxWeb.Web.Services;
using ToolboxWeb.Web.ViewModels;

namespace ToolboxWeb.Web.Controllers;

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

    public async Task<IActionResult> Index(string? search = null)
    {
        return View(new NotesIndexViewModel
        {
            Search = search,
            Notes = await _notes.GetAsync(_currentUser.UserId, search)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(NoteFormViewModel form)
    {
        if (!ModelState.IsValid)
        {
            return View("Index", new NotesIndexViewModel { Notes = await _notes.GetAsync(_currentUser.UserId), Form = form });
        }

        await _notes.CreateAsync(_currentUser.UserId, form);
        TempData[TempDataKeys.SuccessMessage] = _localizer["Flash.NoteCreated"].Value;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _notes.DeleteAsync(_currentUser.UserId, id);
        return RedirectToAction(nameof(Index));
    }
}
