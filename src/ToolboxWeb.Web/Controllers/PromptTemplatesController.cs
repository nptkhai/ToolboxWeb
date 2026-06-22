using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using ToolboxWeb.Web;
using ToolboxWeb.Web.Constants;
using ToolboxWeb.Web.Services;
using ToolboxWeb.Web.ViewModels;

namespace ToolboxWeb.Web.Controllers;

[Authorize]
public class PromptTemplatesController : Controller
{
    private readonly IPromptTemplateService _prompts;
    private readonly ICurrentUserService _currentUser;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public PromptTemplatesController(IPromptTemplateService prompts, ICurrentUserService currentUser, IStringLocalizer<SharedResource> localizer)
    {
        _prompts = prompts;
        _currentUser = currentUser;
        _localizer = localizer;
    }

    public async Task<IActionResult> Index(int? editId = null)
    {
        var form = new PromptTemplateFormViewModel();
        if (editId.HasValue)
        {
            var template = await _prompts.GetByIdAsync(_currentUser.UserId, editId.Value);
            if (template is not null)
            {
                form = new PromptTemplateFormViewModel
                {
                    Id = template.Id,
                    Name = template.Name,
                    Category = template.Category,
                    Description = template.Description,
                    InputVariables = template.InputVariables,
                    OutputFormat = template.OutputFormat,
                    Content = template.Content
                };
            }
        }

        return View(new PromptTemplatesIndexViewModel
        {
            Templates = await _prompts.GetAsync(_currentUser.UserId),
            Form = form
        });
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
        TempData[TempDataKeys.SuccessMessage] = _localizer["Flash.PromptCreated"].Value;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(PromptTemplateFormViewModel form)
    {
        if (!ModelState.IsValid)
        {
            return View("Index", new PromptTemplatesIndexViewModel { Templates = await _prompts.GetAsync(_currentUser.UserId), Form = form });
        }

        var updated = await _prompts.UpdateAsync(_currentUser.UserId, form);
        TempData[updated ? TempDataKeys.SuccessMessage : TempDataKeys.ErrorMessage] = updated
            ? _localizer["Flash.PromptUpdated"].Value
            : _localizer["Flash.PromptNotFound"].Value;
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
