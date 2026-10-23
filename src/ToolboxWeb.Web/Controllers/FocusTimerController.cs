using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using ToolboxWeb.Web;
using ToolboxWeb.Web.Constants;
using ToolboxWeb.Web.Enums;
using ToolboxWeb.Web.Services;
using ToolboxWeb.Web.ViewModels;

namespace ToolboxWeb.Web.Controllers;

[Authorize]
public class FocusTimerController : Controller
{
    private readonly IFocusSessionService _focusSessions;
    private readonly IChecklistService _checklist;
    private readonly ICurrentUserService _currentUser;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public FocusTimerController(IFocusSessionService focusSessions, IChecklistService checklist, ICurrentUserService currentUser, IStringLocalizer<SharedResource> localizer)
    {
        _focusSessions = focusSessions;
        _checklist = checklist;
        _currentUser = currentUser;
        _localizer = localizer;
    }

    [HttpGet("/" + ToolboxRouteSlugs.Pages.FocusTimer)]
    [HttpGet("/" + ToolboxRouteSlugs.HtmlPages.FocusTimer, Name = ToolboxRouteSlugs.RouteNames.FocusTimerHtml)]
    public async Task<IActionResult> Index(int? minutes = null, bool start = false)
    {
        return View(await PageAsync(new FocusSessionFormViewModel(), minutes is >= 1 and <= 240 ? minutes : null, start));
    }

    /// <summary>
    /// The browser posts this when a focus session ends, from whichever page is open; a stop
    /// before the end posts the minutes actually spent. Answers JSON to script, redirects otherwise.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(FocusSessionFormViewModel form)
    {
        var isScript = Request.Headers.XRequestedWith == "XMLHttpRequest";
        if (!ModelState.IsValid)
        {
            return isScript
                ? BadRequest(new { error = ModelState.Values.SelectMany(x => x.Errors).Select(x => x.ErrorMessage).FirstOrDefault() })
                : View("Index", await PageAsync(form, null, false));
        }

        var id = await _focusSessions.CompleteAsync(_currentUser.UserId, form);
        if (isScript)
        {
            return Json(new { saved = id is not null });
        }

        TempData[TempDataKeys.SuccessMessage] = _localizer["Flash.FocusSessionSaved", form.DurationMinutes].Value;
        return RedirectToTimer();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateNote(FocusSessionNoteViewModel form)
    {
        if (ModelState.IsValid && await _focusSessions.UpdateNoteAsync(_currentUser.UserId, form.Id, form.Note))
        {
            TempData[TempDataKeys.SuccessMessage] = _localizer["Flash.FocusSessionUpdated"].Value;
        }
        else
        {
            TempData[TempDataKeys.ErrorMessage] = _localizer["Flash.FocusSessionNotUpdated"].Value;
        }

        return RedirectToTimer();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _focusSessions.DeleteAsync(_currentUser.UserId, id);
        return RedirectToTimer();
    }

    private async Task<FocusTimerPageViewModel> PageAsync(FocusSessionFormViewModel form, int? minutes, bool start)
    {
        var todos = await _checklist.GetAsync(_currentUser.UserId, 200);
        return new FocusTimerPageViewModel
        {
            Form = form,
            Stats = await _focusSessions.GetStatsAsync(_currentUser.UserId),
            Todos = todos.Where(x => x.Status == ChecklistStatus.Todo).Select(x => new FocusTodoOption(x.Id, x.Title)).ToList(),
            AutoStart = start,
            RequestedMinutes = minutes
        };
    }

    private IActionResult RedirectToTimer() => Redirect(Url.RouteUrl(ToolboxRouteSlugs.RouteNames.FocusTimerHtml) ?? "/focus-timer.html");
}
