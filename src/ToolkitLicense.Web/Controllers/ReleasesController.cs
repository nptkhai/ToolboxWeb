using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using ToolkitLicense.Web.Infrastructure;
using ToolkitLicense.Web.Services;
using ToolkitLicense.Web.ViewModels;

namespace ToolkitLicense.Web.Controllers;

/// <summary>Published versions, and which version the devices report.</summary>
public sealed class ReleasesController(ReleaseService releases, UpdateFiles updateFiles, IStringLocalizer<SharedResource> T) : Controller
{
    public async Task<IActionResult> Index()
    {
        var model = await BuildAsync(new ReleasesViewModel());
        // A package uploaded without a release record yet: offer its version in the form.
        if (model.FeedVersion is { } feed && !model.Releases.Any(x => ReleaseService.Compare(x.Version, feed) == 0))
        {
            model.Version = feed;
            model.DownloadUrl = $"{Request.Scheme}://{Request.Host}/download";
        }

        return View(model);
    }

    /// <summary>Velopack output (installer, packages, feed) from tools/release, saved to the updates folder.</summary>
    [HttpPost]
    [RequestSizeLimit(UploadLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadLimit)]
    public async Task<IActionResult> Upload(List<IFormFile> files)
    {
        var (saved, error) = await updateFiles.SaveAsync(files);
        if (error is not null)
        {
            var model = await BuildAsync(new ReleasesViewModel());
            model.UploadError = T[error].Value;
            return View(nameof(Index), model);
        }

        TempData["Success"] = T["Updates.Uploaded", saved.Count].Value;
        return RedirectToAction(nameof(Index));
    }

    private const long UploadLimit = 1024L * 1024 * 1024;

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
        model.Files = updateFiles.List();
        model.FeedVersion = updateFiles.FeedVersion();
        return model;
    }
}
