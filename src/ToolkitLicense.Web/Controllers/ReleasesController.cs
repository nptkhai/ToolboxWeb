using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using ToolkitLicense.Web.Services;
using ToolkitLicense.Web.ViewModels;

namespace ToolkitLicense.Web.Controllers;

/// <summary>Published versions, and which version the devices report.</summary>
public sealed class ReleasesController(ReleaseService releases, IStringLocalizer<SharedResource> T) : Controller
{
    public async Task<IActionResult> Index() => View(await BuildAsync(new ReleasesViewModel()));

    [HttpPost]
    public async Task<IActionResult> Create(ReleasesViewModel input)
    {
        var error = await releases.CreateAsync(new ReleaseInput(input.Version, input.Notes, input.Mandatory, input.DownloadUrl));
        if (error is not null)
        {
            ModelState.AddModelError(error == "Release.Error.Url" ? nameof(input.DownloadUrl) : nameof(input.Version), T[error]);
            return View(nameof(Index), await BuildAsync(input));
        }

        TempData["Success"] = T["Release.Created", input.Version!.Trim()].Value;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var error = await releases.DeleteAsync(id);
        TempData[error is null ? "Success" : "Error"] = T[error ?? "Release.Deleted"].Value;
        return RedirectToAction(nameof(Index));
    }

    private async Task<ReleasesViewModel> BuildAsync(ReleasesViewModel model)
    {
        model.Releases = await releases.AllAsync();
        model.InUse = await releases.VersionsInUseAsync();
        return model;
    }
}
