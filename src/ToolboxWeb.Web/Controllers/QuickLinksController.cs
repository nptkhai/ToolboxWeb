using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToolboxWeb.Web.Services;
using ToolboxWeb.Web.ViewModels;

namespace ToolboxWeb.Web.Controllers;

[Authorize]
public class QuickLinksController : Controller
{
    private readonly IQuickLinkService _quickLinks;
    private readonly ICurrentUserService _currentUser;

    public QuickLinksController(IQuickLinkService quickLinks, ICurrentUserService currentUser)
    {
        _quickLinks = quickLinks;
        _currentUser = currentUser;
    }

    public async Task<IActionResult> Index()
    {
        return View(new QuickLinksIndexViewModel { Links = await _quickLinks.GetAsync(_currentUser.UserId) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(QuickLinkFormViewModel form)
    {
        if (!ModelState.IsValid)
        {
            return View("Index", new QuickLinksIndexViewModel { Links = await _quickLinks.GetAsync(_currentUser.UserId), Form = form });
        }

        await _quickLinks.CreateAsync(_currentUser.UserId, form);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _quickLinks.DeleteAsync(_currentUser.UserId, id);
        return RedirectToAction(nameof(Index));
    }
}
