using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToolboxWeb.Web.Services;
using ToolboxWeb.Web.ViewModels;

namespace ToolboxWeb.Web.Controllers;

[Authorize]
public class PromptTemplatesController : Controller
{
    private readonly IPromptTemplateService _prompts;
    private readonly ICurrentUserService _currentUser;

    public PromptTemplatesController(IPromptTemplateService prompts, ICurrentUserService currentUser)
    {
        _prompts = prompts;
        _currentUser = currentUser;
    }

    public async Task<IActionResult> Index()
    {
        return View(new PromptTemplatesIndexViewModel { Templates = await _prompts.GetAsync(_currentUser.UserId) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PromptTemplateFormViewModel form)
    {
        if (!ModelState.IsValid)
        {
            return View("Index", new PromptTemplatesIndexViewModel { Templates = await _prompts.GetAsync(_currentUser.UserId), Form = form });
        }

        await _prompts.CreateAsync(_currentUser.UserId, form);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkUsed(int id)
    {
        await _prompts.MarkUsedAsync(_currentUser.UserId, id);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _prompts.DeleteAsync(_currentUser.UserId, id);
        return RedirectToAction(nameof(Index));
    }
}
