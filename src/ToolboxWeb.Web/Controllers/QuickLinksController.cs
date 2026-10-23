using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using ToolboxWeb.Web;
using ToolboxWeb.Web.Constants;
using ToolboxWeb.Web.Extensions;
using ToolboxWeb.Web.Infrastructure.Bookmarks;
using ToolboxWeb.Web.Infrastructure.Time;
using ToolboxWeb.Web.Services;
using ToolboxWeb.Web.ViewModels;

namespace ToolboxWeb.Web.Controllers;

[Authorize]
public class QuickLinksController : Controller
{
    private const string SortCookie = "toolbox.links.sort";
    private const long MaxImportBytes = 5 * 1024 * 1024;

    private readonly IQuickLinkService _quickLinks;
    private readonly ICurrentUserService _currentUser;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public QuickLinksController(IQuickLinkService quickLinks, ICurrentUserService currentUser, IStringLocalizer<SharedResource> localizer)
    {
        _quickLinks = quickLinks;
        _currentUser = currentUser;
        _localizer = localizer;
    }

    [HttpGet("/" + ToolboxRouteSlugs.Pages.QuickLinks)]
    [HttpGet("/" + ToolboxRouteSlugs.HtmlPages.QuickLinks, Name = ToolboxRouteSlugs.RouteNames.QuickLinksHtml)]
    public async Task<IActionResult> Index(string? group = null, string? search = null, string? sort = null)
    {
        // The list order is remembered between visits, so redirects after a save keep it.
        var chosen = QuickLinkSorts.Normalize(sort);
        if (chosen is not null)
        {
            Response.Cookies.Append(SortCookie, chosen, new CookieOptions { MaxAge = TimeSpan.FromDays(365), IsEssential = true, HttpOnly = true, SameSite = SameSiteMode.Lax });
        }

        return View(await _quickLinks.GetListAsync(_currentUser.UserId, Query(group, search, chosen)));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(QuickLinkFormViewModel form)
    {
        ValidateUrl(form);
        if (!ModelState.IsValid)
        {
            return await ReopenForm(form);
        }

        await _quickLinks.CreateAsync(_currentUser.UserId, form);
        TempData[TempDataKeys.SuccessMessage] = _localizer["Flash.QuickLinkCreated"].Value;
        return RedirectToList(form.ReturnGroup, form.ReturnSearch);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(QuickLinkFormViewModel form)
    {
        ValidateUrl(form);
        if (!ModelState.IsValid)
        {
            return await ReopenForm(form);
        }

        var updated = await _quickLinks.UpdateAsync(_currentUser.UserId, form);
        TempData[updated ? TempDataKeys.SuccessMessage : TempDataKeys.ErrorMessage] = updated
            ? _localizer["Flash.QuickLinkUpdated"].Value
            : _localizer["Flash.QuickLinkNotFound"].Value;
        return RedirectToList(form.ReturnGroup, form.ReturnSearch);
    }

    /// <summary>The page calls this in the background when a link is opened; the link itself opens in a new tab.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkOpened(int id)
    {
        var link = await _quickLinks.MarkOpenedAsync(_currentUser.UserId, id);
        return link is null
            ? NotFound()
            : Json(new
            {
                openedText = OpenedText(link.OpenCount, link.LastOpenedAt)
            });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TogglePin(int id, string? group = null, string? search = null)
    {
        await _quickLinks.TogglePinAsync(_currentUser.UserId, id);
        return RedirectToList(group, search);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reorder([FromForm] int[] ids)
    {
        await _quickLinks.ReorderAsync(_currentUser.UserId, ids ?? []);
        return Json(new { ok = true });
    }

    [HttpGet]
    public async Task<IActionResult> CheckUrl(string? url, int? id)
    {
        var normalized = QuickLinkUrl.Normalize(url);
        var duplicate = normalized is null ? null : await _quickLinks.FindDuplicateAsync(_currentUser.UserId, normalized, id);
        return Json(new
        {
            valid = normalized is not null,
            url = normalized,
            duplicate = duplicate is null ? null : _localizer["QuickLinks.Duplicate", duplicate].Value
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, string? group = null, string? search = null)
    {
        await _quickLinks.DeleteAsync(_currentUser.UserId, id);
        return RedirectToList(group, search);
    }

    [HttpGet]
    public async Task<IActionResult> Export()
    {
        var html = BookmarkHtml.Write(await _quickLinks.ExportAsync(_currentUser.UserId));
        var name = string.Create(CultureInfo.InvariantCulture, $"quick-links-{DateTime.UtcNow.ToAppTime():yyyyMMdd-HHmm}.html");
        return File(new UTF8Encoding(false).GetBytes(html), "text/html", name);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxImportBytes + 64 * 1024)]
    public async Task<IActionResult> Import(IFormFile? file, string? group = null, string? search = null)
    {
        IReadOnlyList<BookmarkEntry> entries = [];
        if (file is { Length: > 0 and <= MaxImportBytes })
        {
            using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            entries = BookmarkHtml.Parse(await reader.ReadToEndAsync());
        }

        if (entries.Count == 0)
        {
            TempData[TempDataKeys.ErrorMessage] = _localizer["Flash.QuickLinksImportInvalid"].Value;
            return RedirectToList(group, search);
        }

        var result = await _quickLinks.ImportAsync(_currentUser.UserId, entries);
        TempData[result.Created > 0 ? TempDataKeys.SuccessMessage : TempDataKeys.ErrorMessage] = result.Skipped > 0
            ? _localizer["Flash.QuickLinksImportedSkipped", result.Created, result.Skipped].Value
            : _localizer["Flash.QuickLinksImported", result.Created].Value;
        return RedirectToList(null, null);
    }

    private string OpenedText(int count, DateTime? lastOpened)
    {
        var text = _localizer["QuickLinks.OpenedCount", count].Value;
        return lastOpened is { } at ? text + " · " + at.ToDisplayAppDateTime() : text;
    }

    // "jira.company.vn" is completed to "https://jira.company.vn" before it is checked.
    private void ValidateUrl(QuickLinkFormViewModel form)
    {
        if (string.IsNullOrWhiteSpace(form.Url))
        {
            return;
        }

        var normalized = QuickLinkUrl.Normalize(form.Url);
        if (normalized is null)
        {
            ModelState.AddModelError(nameof(form.Url), _localizer["QuickLinks.Validation.Url"].Value);
        }
        else
        {
            form.Url = normalized;
        }
    }

    private async Task<IActionResult> ReopenForm(QuickLinkFormViewModel form)
    {
        // Reopen the dialog with what was typed instead of throwing it away.
        var page = await _quickLinks.GetListAsync(_currentUser.UserId, Query(form.ReturnGroup, form.ReturnSearch, null));
        page.Form = form;
        page.OpenForm = true;
        return View("Index", page);
    }

    private QuickLinkListQuery Query(string? group, string? search, string? sort)
    {
        return new QuickLinkListQuery(
            string.IsNullOrWhiteSpace(group) ? null : group.Trim().ToLowerInvariant(),
            string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
            sort ?? QuickLinkSorts.Normalize(Request.Cookies[SortCookie]) ?? QuickLinkSorts.Manual);
    }

    private IActionResult RedirectToList(string? group, string? search)
    {
        return Redirect(Url.RouteUrl(ToolboxRouteSlugs.RouteNames.QuickLinksHtml, new
        {
            group = string.IsNullOrWhiteSpace(group) ? null : group,
            search = string.IsNullOrWhiteSpace(search) ? null : search
        }) ?? "/quick-links.html");
    }
}
