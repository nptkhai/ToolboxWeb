using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using ToolboxWeb.Web;
using ToolboxWeb.Web.Constants;
using ToolboxWeb.Web.Enums;
using ToolboxWeb.Web.Extensions;
using ToolboxWeb.Web.Infrastructure.Time;
using ToolboxWeb.Web.Services;
using ToolboxWeb.Web.ViewModels;

namespace ToolboxWeb.Web.Controllers;

[Authorize]
public class PromptTemplatesController : Controller
{
    private const string SortCookie = "toolbox.prompts.sort";
    private const long MaxImportBytes = 2 * 1024 * 1024;

    private static readonly JsonSerializerOptions ExportJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly IPromptTemplateService _prompts;
    private readonly ICurrentUserService _currentUser;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public PromptTemplatesController(IPromptTemplateService prompts, ICurrentUserService currentUser, IStringLocalizer<SharedResource> localizer)
    {
        _prompts = prompts;
        _currentUser = currentUser;
        _localizer = localizer;
    }

    [HttpGet("/" + ToolboxRouteSlugs.Pages.PromptTemplates)]
    [HttpGet("/" + ToolboxRouteSlugs.HtmlPages.PromptTemplates, Name = ToolboxRouteSlugs.RouteNames.PromptTemplatesHtml)]
    public async Task<IActionResult> Index(string? category = null, string? search = null, string? sort = null, int? editId = null)
    {
        // The list order is remembered between visits, so redirects after a save keep it.
        var chosen = PromptSorts.Normalize(sort);
        if (chosen is not null)
        {
            Response.Cookies.Append(SortCookie, chosen, new CookieOptions { MaxAge = TimeSpan.FromDays(365), IsEssential = true, HttpOnly = true, SameSite = SameSiteMode.Lax });
        }

        var page = await _prompts.GetListAsync(_currentUser.UserId, Query(category, search, chosen));
        page.Seeds = await _prompts.GetSeedStatusAsync(_currentUser.UserId);

        // An old link (?editId=…) still opens the edit dialog.
        if (editId is int id && await _prompts.GetByIdAsync(_currentUser.UserId, id) is { } template)
        {
            page.Form = new PromptTemplateFormViewModel
            {
                Id = template.Id,
                Name = template.Name,
                Category = template.Category,
                Description = template.Description,
                InputVariables = template.InputVariables,
                OutputFormat = template.OutputFormat,
                Content = template.Content
            };
            page.OpenForm = true;
        }

        return View(page);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PromptTemplateFormViewModel form)
    {
        if (!ModelState.IsValid)
        {
            return await ReopenForm(form);
        }

        await _prompts.CreateAsync(_currentUser.UserId, form);
        TempData[TempDataKeys.SuccessMessage] = _localizer["Flash.PromptCreated"].Value;
        return RedirectToList(form.ReturnCategory, form.ReturnSearch);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(PromptTemplateFormViewModel form)
    {
        if (!ModelState.IsValid)
        {
            return await ReopenForm(form);
        }

        var updated = await _prompts.UpdateAsync(_currentUser.UserId, form);
        TempData[updated ? TempDataKeys.SuccessMessage : TempDataKeys.ErrorMessage] = updated
            ? _localizer["Flash.PromptUpdated"].Value
            : _localizer["Flash.PromptNotFound"].Value;
        return RedirectToList(form.ReturnCategory, form.ReturnSearch);
    }

    /// <summary>
    /// Copying a prompt counts as using it. The page calls this in the background and gets the
    /// new count back; without script it is a normal form post.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkUsed(int id, string? category = null, string? search = null)
    {
        var template = await _prompts.MarkUsedAsync(_currentUser.UserId, id);
        if (Request.Headers.XRequestedWith == "XMLHttpRequest")
        {
            return template is null
                ? NotFound()
                : Json(new
                {
                    useCount = template.UseCount,
                    usedText = _localizer["PromptTemplates.UsedCount", template.UseCount].Value,
                    lastUsed = template.LastUsedAt?.ToDisplayAppDateTime()
                });
        }

        return RedirectToList(category, search);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TogglePin(int id, string? category = null, string? search = null)
    {
        await _prompts.TogglePinAsync(_currentUser.UserId, id);
        return RedirectToList(category, search);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Duplicate(int id, string? category = null, string? search = null)
    {
        var copyId = await _prompts.DuplicateAsync(_currentUser.UserId, id);
        if (copyId is null)
        {
            TempData[TempDataKeys.ErrorMessage] = _localizer["Flash.PromptNotFound"].Value;
            return RedirectToList(category, search);
        }

        // Open the copy straight away: a copy is made to be changed.
        TempData[TempDataKeys.SuccessMessage] = _localizer["Flash.PromptDuplicated"].Value;
        return Redirect(ListUrl(category, search, copyId));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, string? category = null, string? search = null)
    {
        await _prompts.DeleteAsync(_currentUser.UserId, id);
        return RedirectToList(category, search);
    }

    [HttpGet]
    public async Task<IActionResult> Export()
    {
        var file = await _prompts.ExportAsync(_currentUser.UserId);
        var name = string.Create(CultureInfo.InvariantCulture, $"prompts-{DateTime.UtcNow.ToAppTime():yyyyMMdd-HHmm}.json");
        return File(JsonSerializer.SerializeToUtf8Bytes(file, ExportJson), "application/json", name);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxImportBytes + 64 * 1024)]
    public async Task<IActionResult> Import(IFormFile? file, string? category = null, string? search = null)
    {
        var items = await ReadImportAsync(file);
        if (items is null)
        {
            TempData[TempDataKeys.ErrorMessage] = _localizer["Flash.PromptImportInvalid"].Value;
            return RedirectToList(category, search);
        }

        var result = await _prompts.ImportAsync(_currentUser.UserId, items);
        TempData[result.Created > 0 ? TempDataKeys.SuccessMessage : TempDataKeys.ErrorMessage] = result.Skipped > 0
            ? _localizer["Flash.PromptImportedSkipped", result.Created, result.Skipped].Value
            : _localizer["Flash.PromptImported", result.Created].Value;
        return RedirectToList(category, search);
    }

    /// <summary>
    /// "Restore built-in prompts": adds the missing ones that were ticked and puts the current text
    /// back into the changed ones that were ticked (the dialog names each of them).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestoreSeeds(string[]? add, string[]? replace, string? category = null, string? search = null)
    {
        var result = await _prompts.RestoreSeedsAsync(_currentUser.UserId, add ?? [], replace ?? []);
        TempData[result.Added + result.Replaced > 0 ? TempDataKeys.SuccessMessage : TempDataKeys.ErrorMessage] =
            _localizer["Flash.PromptSeedsRestored", result.Added, result.Replaced].Value;
        return RedirectToList(category, search);
    }

    // Accepts the file Export writes, or a bare array of prompts.
    private static async Task<IReadOnlyList<PromptExportItem>?> ReadImportAsync(IFormFile? file)
    {
        if (file is null || file.Length == 0 || file.Length > MaxImportBytes)
        {
            return null;
        }

        try
        {
            await using var stream = file.OpenReadStream();
            using var document = await JsonDocument.ParseAsync(stream);
            var prompts = document.RootElement.ValueKind switch
            {
                JsonValueKind.Array => document.RootElement,
                JsonValueKind.Object when document.RootElement.TryGetProperty("prompts", out var list)
                    || document.RootElement.TryGetProperty("Prompts", out list) => list,
                _ => default
            };

            return prompts.ValueKind == JsonValueKind.Array
                ? prompts.Deserialize<List<PromptExportItem>>(ExportJson)
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<IActionResult> ReopenForm(PromptTemplateFormViewModel form)
    {
        // Reopen the dialog with what was typed instead of throwing it away.
        var page = await _prompts.GetListAsync(_currentUser.UserId, Query(form.ReturnCategory, form.ReturnSearch, null));
        page.Seeds = await _prompts.GetSeedStatusAsync(_currentUser.UserId);
        page.Form = form;
        page.OpenForm = true;
        return View("Index", page);
    }

    private PromptListQuery Query(string? category, string? search, string? sort)
    {
        return new PromptListQuery(
            Enum.TryParse<PromptCategory>(category, true, out var parsed) && Enum.IsDefined(parsed) ? parsed : null,
            string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
            sort ?? PromptSorts.Normalize(Request.Cookies[SortCookie]) ?? PromptSorts.Name);
    }

    private string ListUrl(string? category, string? search, int? editId = null)
    {
        return Url.RouteUrl(ToolboxRouteSlugs.RouteNames.PromptTemplatesHtml, new
        {
            category = string.IsNullOrWhiteSpace(category) ? null : category,
            search = string.IsNullOrWhiteSpace(search) ? null : search,
            editId
        }) ?? "/prompt-templates.html";
    }

    private IActionResult RedirectToList(string? category, string? search) => Redirect(ListUrl(category, search));
}
