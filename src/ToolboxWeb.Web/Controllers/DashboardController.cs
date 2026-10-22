using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using ToolboxWeb.Web;
using ToolboxWeb.Web.Constants;
using ToolboxWeb.Web.Services;
using ToolboxWeb.Web.ViewModels;

namespace ToolboxWeb.Web.Controllers;

/// <summary>
/// The personal overview. The quick forms on it (add a task, mark one done, add a note) post here
/// and come back to the overview instead of the tool's own page.
/// </summary>
[Authorize]
public class DashboardController : Controller
{
    private readonly IDashboardService _dashboard;
    private readonly IChecklistService _checklist;
    private readonly INoteService _notes;
    private readonly IJiraAuthService _jiraAuth;
    private readonly ICurrentUserService _currentUser;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public DashboardController(
        IDashboardService dashboard,
        IChecklistService checklist,
        INoteService notes,
        IJiraAuthService jiraAuth,
        ICurrentUserService currentUser,
        IStringLocalizer<SharedResource> localizer)
    {
        _dashboard = dashboard;
        _checklist = checklist;
        _notes = notes;
        _jiraAuth = jiraAuth;
        _currentUser = currentUser;
        _localizer = localizer;
    }

    [HttpGet("/" + ToolboxRouteSlugs.Pages.Dashboard)]
    [HttpGet("/" + ToolboxRouteSlugs.HtmlPages.Dashboard, Name = ToolboxRouteSlugs.RouteNames.DashboardHtml)]
    public async Task<IActionResult> Index()
    {
        return View(await BuildAsync());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuickTask([Bind(Prefix = "QuickTask")] DashboardQuickTaskViewModel form)
    {
        if (!ModelState.IsValid)
        {
            TempData[TempDataKeys.ErrorMessage] = _localizer["Dashboard.QuickTaskRequired"].Value;
            return RedirectToDashboard();
        }

        await _dashboard.CreateQuickTaskAsync(_currentUser.UserId, form);
        TempData[TempDataKeys.SuccessMessage] = _localizer["Flash.TaskCreated"].Value;
        return RedirectToDashboard();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteTask(int id)
    {
        if ((await _checklist.CompleteAsync(_currentUser.UserId, [id])).Count > 0)
        {
            TempData[TempDataKeys.SuccessMessage] = _localizer["Dashboard.TaskCompleted"].Value;
        }

        return RedirectToDashboard();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuickNote([Bind(Prefix = "QuickNote")] NoteFormViewModel form)
    {
        // The dashboard only adds plain top-level notes; groups and formats live on the notes page.
        form.ParentId = null;
        form.Format = ToolboxWeb.Web.Enums.NoteFormat.Text;

        if (!ModelState.IsValid)
        {
            var page = await BuildAsync();
            page.QuickNote = form;
            page.OpenQuickNote = true;
            return View("Index", page);
        }

        var result = await _notes.CreateAsync(_currentUser.UserId, form);
        if (result.Succeeded)
        {
            TempData[TempDataKeys.SuccessMessage] = _localizer["Flash.NoteCreated"].Value;
        }
        else
        {
            TempData[TempDataKeys.ErrorMessage] = result.Error;
        }

        return RedirectToDashboard();
    }

    private async Task<DashboardViewModel> BuildAsync()
    {
        var jiraSession = await _jiraAuth.GetCurrentSessionAsync(HttpContext);
        return await _dashboard.BuildAsync(_currentUser.UserId, _currentUser.DisplayName ?? string.Empty, jiraSession is not null);
    }

    private RedirectResult RedirectToDashboard()
    {
        return Redirect(Url.RouteUrl(ToolboxRouteSlugs.RouteNames.DashboardHtml) ?? "/dashboard.html");
    }
}
