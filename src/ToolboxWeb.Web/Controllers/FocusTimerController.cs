using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToolboxWeb.Web.Services;
using ToolboxWeb.Web.ViewModels;

namespace ToolboxWeb.Web.Controllers;

[Authorize]
public class FocusTimerController : Controller
{
    private readonly IFocusSessionService _focusSessions;
    private readonly ICurrentUserService _currentUser;

    public FocusTimerController(IFocusSessionService focusSessions, ICurrentUserService currentUser)
    {
        _focusSessions = focusSessions;
        _currentUser = currentUser;
    }

    public async Task<IActionResult> Index()
    {
        ViewBag.RecentSessions = await _focusSessions.GetRecentAsync(_currentUser.UserId, 20);
        return View(new FocusSessionFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(FocusSessionFormViewModel form)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.RecentSessions = await _focusSessions.GetRecentAsync(_currentUser.UserId, 20);
            return View("Index", form);
        }

        await _focusSessions.CompleteAsync(_currentUser.UserId, form);
        return RedirectToAction(nameof(Index));
    }
}
