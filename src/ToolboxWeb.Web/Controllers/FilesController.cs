using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Net.Http.Headers;
using ToolboxWeb.Web.Constants;
using ToolboxWeb.Web.Infrastructure.Files;
using ToolboxWeb.Web.Infrastructure.Hosting;
using ToolboxWeb.Web.Infrastructure.Web;
using ToolboxWeb.Web.Services;
using ToolboxWeb.Web.Services.Files;
using ToolboxWeb.Web.ViewModels.Files;

namespace ToolboxWeb.Web.Controllers;

/// <summary>
/// "Files &amp; folders" (/files.html): the page is drawn by Content/js/files/*.js from these JSON
/// actions. Desktop and IIS site only (<see cref="FilesAccess"/>); the web copy has no files.
/// Answers are <c>{ success, code, message, data }</c>.
/// </summary>
[Authorize]
[NotInWebMode]
[FilesAccessRequired]
public class FilesController : Controller
{
    private readonly IFileWorkspaceService _files;
    private readonly ICurrentUserService _currentUser;
    private readonly AppModeInfo _mode;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public FilesController(IFileWorkspaceService files, ICurrentUserService currentUser, AppModeInfo mode, IStringLocalizer<SharedResource> localizer)
    {
        _files = files;
        _currentUser = currentUser;
        _mode = mode;
        _localizer = localizer;
    }

    [HttpGet("/" + ToolboxRouteSlugs.Pages.Files)]
    [HttpGet("/" + ToolboxRouteSlugs.HtmlPages.Files, Name = ToolboxRouteSlugs.RouteNames.FilesHtml)]
    public IActionResult Index()
    {
        return View(new FilesPageViewModel
        {
            StateStorageKey = BrowserStorageKey.ForUser("toolbox.files.state.v1", _currentUser.UserId),
            IsDesktop = _mode.IsDesktop,
            HasWindowsDialog = _mode.IsDesktop && WindowsShell.IsAvailable
        });
    }

    // ------------------------------------------------------------------ roots

    [HttpGet("/Files/Roots")]
    public Task<IActionResult> Roots() => Run(async () => await _files.GetRootsAsync(HttpContext.RequestAborted));

    [HttpPost("/Files/PickFolder")]
    [ValidateAntiForgeryToken]
    [AllowWhenUnlicensed]
    public Task<IActionResult> PickFolder([FromBody] AddFileRootRequest? request) =>
        Run(async () => new { path = await _files.PickFolderAsync(request?.Path) });

    [HttpGet("/Files/Browse")]
    public Task<IActionResult> Browse(string? path) => Run(() => Task.FromResult<object>(_files.BrowseFolders(path)));

    [HttpPost("/Files/AddRoot")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> AddRoot([FromBody] AddFileRootRequest? request) =>
        Run(async () => await _files.AddRootAsync(request?.Path, request?.Name, HttpContext.RequestAborted));

    [HttpPost("/Files/RenameRoot")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> RenameRoot([FromBody] RenameFileRootRequest? request) =>
        Run(async () => await _files.RenameRootAsync(request?.Id ?? 0, request?.Name, HttpContext.RequestAborted));

    [HttpPost("/Files/RelinkRoot")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> RelinkRoot([FromBody] RelinkFileRootRequest? request) =>
        Run(async () => await _files.RelinkRootAsync(request?.Id ?? 0, request?.Path, HttpContext.RequestAborted));

    [HttpPost("/Files/MoveRoot")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> MoveRoot([FromBody] MoveFileRootRequest? request) =>
        Run(async () =>
        {
            await _files.MoveRootAsync(request?.Id ?? 0, request?.Offset ?? 0, HttpContext.RequestAborted);
            return await _files.GetRootsAsync(HttpContext.RequestAborted);
        });

    [HttpPost("/Files/RemoveRoot")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> RemoveRoot([FromBody] FileRootIdRequest? request) =>
        Run(async () =>
        {
            await _files.RemoveRootAsync(request?.Id ?? 0, HttpContext.RequestAborted);
            return new { };
        });

    // ------------------------------------------------------------------ reading

    [HttpGet("/Files/List")]
    public Task<IActionResult> List(int root, string? path, bool hidden = false) =>
        Run(async () => await _files.ListAsync(root, path, hidden, HttpContext.RequestAborted));

    [HttpGet("/Files/Stats")]
    public Task<IActionResult> Stats(int root, bool hidden = false) =>
        Run(async () => await _files.GetStatsAsync(root, hidden, HttpContext.RequestAborted));

    [HttpGet("/Files/Read")]
    public Task<IActionResult> Read(int root, string? path) =>
        Run(async () => await _files.ReadAsync(root, path, HttpContext.RequestAborted));

    /// <summary>
    /// The file itself. Only images and PDF are shown in the page; everything else is sent as a
    /// download, so a stored HTML or SVG file never runs as a Toolkit page.
    /// </summary>
    [HttpGet("/Files/Raw")]
    public async Task<IActionResult> Raw(int root, string? path, bool download = false)
    {
        try
        {
            var (full, contentType, name) = await _files.GetRawAsync(root, path, HttpContext.RequestAborted);
            Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
            Response.Headers[HeaderNames.CacheControl] = "no-store";
            // Even shown inline, an SVG gets no scripts and no access to the page.
            Response.Headers[HeaderNames.ContentSecurityPolicy] = "sandbox; default-src 'none'; img-src 'self' data:; style-src 'unsafe-inline'";
            var inline = !download && (contentType == "application/pdf" || (contentType.StartsWith("image/", StringComparison.Ordinal)));
            if (contentType == "application/pdf" && inline)
            {
                // The browser's own PDF viewer needs its scripts.
                Response.Headers.Remove(HeaderNames.ContentSecurityPolicy);
            }

            return inline
                ? PhysicalFile(full, contentType, enableRangeProcessing: true)
                : PhysicalFile(full, contentType, name, enableRangeProcessing: true);
        }
        catch (FileOperationException ex)
        {
            return ex.Code == "notFound" ? NotFound() : BadRequest(ex.Message);
        }
    }

    [HttpGet("/Files/Info")]
    public Task<IActionResult> Info(int root, string? path) =>
        Run(async () => (object?)await _files.GetInfoAsync(root, path, HttpContext.RequestAborted));

    [HttpPost("/Files/Save")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(64 * 1024 * 1024)]
    public Task<IActionResult> Save([FromBody] FileSaveRequest? request) =>
        Run(async () => await _files.SaveAsync(request ?? new FileSaveRequest(), HttpContext.RequestAborted));

    [HttpGet("/Files/Find")]
    public Task<IActionResult> Find(string? q, bool hidden = false) =>
        Run(async () => await _files.FindByNameAsync(q, hidden, HttpContext.RequestAborted));

    /// <summary>Desktop: "Save a copy" with the Windows dialog. Copies a file the user already has, so it works on a read-only (ended) license too.</summary>
    [HttpPost("/Files/SaveCopyAs")]
    [ValidateAntiForgeryToken]
    [AllowWhenUnlicensed]
    public Task<IActionResult> SaveCopyAs([FromBody] FileRefRequest? request) =>
        Run(async () => new { path = await _files.SaveCopyAsAsync(request?.Root ?? 0, request?.Path, HttpContext.RequestAborted) });

    [HttpPost("/Files/OpenDefault")]
    [ValidateAntiForgeryToken]
    [AllowWhenUnlicensed]
    public Task<IActionResult> OpenDefault([FromBody] FileRefRequest? request) =>
        Run(async () =>
        {
            await _files.OpenOutsideAsync(request?.Root ?? 0, request?.Path, reveal: false, HttpContext.RequestAborted);
            return new { };
        });

    [HttpPost("/Files/Reveal")]
    [ValidateAntiForgeryToken]
    [AllowWhenUnlicensed]
    public Task<IActionResult> Reveal([FromBody] FileRefRequest? request) =>
        Run(async () =>
        {
            await _files.OpenOutsideAsync(request?.Root ?? 0, request?.Path, reveal: true, HttpContext.RequestAborted);
            return new { };
        });

    // ------------------------------------------------------------------ plumbing

    private async Task<IActionResult> Run<T>(Func<Task<T>> work)
    {
        try
        {
            return Json(new { success = true, data = await work() });
        }
        catch (FileOperationException ex)
        {
            return Json(new { success = false, code = ex.Code, message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Json(new { success = false, code = "denied", message = _localizer["Files.Error.NoAccess"].Value });
        }
        catch (IOException ex)
        {
            return Json(new { success = false, code = "io", message = _localizer["Files.Error.Io", ex.Message].Value });
        }
    }
}
