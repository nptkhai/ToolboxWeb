using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using ToolboxWeb.Web.Constants;
using ToolboxWeb.Web.Infrastructure.Security;
using ToolboxWeb.Web.Services;
using ToolboxWeb.Web.ViewModels;

namespace ToolboxWeb.Web.Controllers;

[Authorize]
[DevToolsOnly]
public class TabulatorDemoController : Controller
{
    private readonly ITabulatorTaskService _tasks;
    private readonly ICurrentUserService _currentUser;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public TabulatorDemoController(ITabulatorTaskService tasks, ICurrentUserService currentUser, IStringLocalizer<SharedResource> localizer)
    {
        _tasks = tasks;
        _currentUser = currentUser;
        _localizer = localizer;
    }

    [HttpGet("/" + ToolboxRouteSlugs.Pages.TabulatorDemo)]
    [HttpGet("/" + ToolboxRouteSlugs.HtmlPages.TabulatorDemo, Name = ToolboxRouteSlugs.RouteNames.TabulatorDemoHtml)]
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Data()
    {
        return Json(await _tasks.GetAsync(_currentUser.UserId));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save([FromBody] TabulatorTaskBatchSaveRequest request)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(x => x.Errors)
                .Select(x => string.IsNullOrWhiteSpace(x.ErrorMessage) ? _localizer["TabulatorDemo.SaveValidationFailed"].Value : x.ErrorMessage)
                .Distinct()
                .ToList();

            return BadRequest(new
            {
                message = _localizer["TabulatorDemo.SaveValidationFailed"].Value,
                errors
            });
        }

        var result = await _tasks.SaveAsync(_currentUser.UserId, request);
        if (!result.Success)
        {
            return BadRequest(new
            {
                message = result.Message,
                errors = result.Errors
            });
        }

        return Json(new
        {
            message = result.Message,
            items = result.Items
        });
    }
}
