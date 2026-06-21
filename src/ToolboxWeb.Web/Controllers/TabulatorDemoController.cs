using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToolboxWeb.Web.Services;
using ToolboxWeb.Web.ViewModels;

namespace ToolboxWeb.Web.Controllers;

[Authorize]
public class TabulatorDemoController : Controller
{
    private readonly ITabulatorTaskService _tasks;
    private readonly ICurrentUserService _currentUser;

    public TabulatorDemoController(ITabulatorTaskService tasks, ICurrentUserService currentUser)
    {
        _tasks = tasks;
        _currentUser = currentUser;
    }

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
                .Select(x => string.IsNullOrWhiteSpace(x.ErrorMessage) ? "Invalid request." : x.ErrorMessage)
                .Distinct()
                .ToList();

            return BadRequest(new
            {
                message = "Validation failed.",
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
