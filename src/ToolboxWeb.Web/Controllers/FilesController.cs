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
    private readonly FileChangeFeed _changes;
    private readonly FileTrashService _trash;
    private readonly FilesAccess _access;

    public FilesController(IFileWorkspaceService files, ICurrentUserService currentUser, AppModeInfo mode, IStringLocalizer<SharedResource> localizer, FileChangeFeed changes, FileTrashService trash, FilesAccess access)
    {
        _access = access;
        _changes = changes;
        _trash = trash;
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
            HasWindowsDialog = _mode.IsDesktop && WindowsShell.IsAvailable,
            UsesToolkitTrash = !_trash.UsesRecycleBin,
            CanOpenOnThisMachine = _access.CanOpenOnThisMachine
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

    // ------------------------------------------------------------------ managing files

    [HttpPost("/Files/Create")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Create([FromBody] FileCreateRequest? request) =>
        Run(async () => await _files.CreateAsync(request?.Root ?? 0, request?.Folder, request?.Name, request?.IsFolder ?? false, HttpContext.RequestAborted));

    [HttpPost("/Files/Rename")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Rename([FromBody] FileRenameRequest? request) =>
        Run(async () => await _files.RenameAsync(request?.Root ?? 0, request?.Path, request?.Name, HttpContext.RequestAborted));

    [HttpPost("/Files/Duplicate")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Duplicate([FromBody] FileRefRequest? request) =>
        Run(async () => await _files.DuplicateAsync(request?.Root ?? 0, request?.Path, HttpContext.RequestAborted));

    [HttpPost("/Files/Delete")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Delete([FromBody] FileRefRequest? request) =>
        Run(async () => new { toolkitTrash = await _files.DeleteAsync(request?.Root ?? 0, request?.Path, HttpContext.RequestAborted) });

    [HttpPost("/Files/Move")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Move([FromBody] FileMoveRequest? request) =>
        Run(async () => await _files.MoveAsync(request?.Root ?? 0, request?.Path, request?.TargetRoot ?? 0, request?.TargetFolder, request?.Copy ?? false, request?.Overwrite ?? false, HttpContext.RequestAborted));

    /// <summary>Files dropped from Explorer: form fields root, folder, overwrite, and per file "files" plus its relative path in "paths".</summary>
    [HttpPost("/Files/Upload")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(512L * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 512L * 1024 * 1024, ValueCountLimit = 4096)]
    public Task<IActionResult> Upload([FromForm] int root, [FromForm] string? folder, [FromForm] bool overwrite, [FromForm] List<string>? paths) =>
        Run(async () =>
        {
            var files = Request.Form.Files;
            var list = new List<(string, IFormFile)>();
            for (var i = 0; i < files.Count; i++)
            {
                var relative = paths is not null && i < paths.Count && !string.IsNullOrWhiteSpace(paths[i]) ? paths[i] : files[i].FileName;
                list.Add((relative, files[i]));
            }

            return new { count = await _files.UploadAsync(root, folder, list, overwrite, HttpContext.RequestAborted) };
        });

    [HttpGet("/Files/Search")]
    public Task<IActionResult> Search(string? q, bool matchCase = false, bool hidden = false, int? root = null) =>
        Run(async () => await _files.SearchContentAsync(q, matchCase, hidden, root, HttpContext.RequestAborted));

    /// <summary>Which linked paths are missing (Markdown link check). Reads only.</summary>
    [HttpPost("/Files/Missing")]
    [ValidateAntiForgeryToken]
    [AllowWhenUnlicensed]
    public Task<IActionResult> Missing([FromBody] FileMissingRequest? request) =>
        Run(async () => await _files.MissingAsync(request?.Root ?? 0, request?.Paths ?? [], HttpContext.RequestAborted));

    /// <summary>Desktop: a file opened from Explorer (/files.html?open=…). Adds its folder as a root only when asked.</summary>
    [HttpPost("/Files/OpenPath")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> OpenPath([FromBody] FileOpenPathRequest? request) =>
        Run(async () => await _files.OpenPathAsync(request?.Path, request?.AddRoot ?? false, HttpContext.RequestAborted));

    [HttpGet("/Files/Trash")]
    public Task<IActionResult> Trash() => Run(() => Task.FromResult<object>(_files.GetTrash()));

    [HttpPost("/Files/Restore")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Restore([FromBody] FileTrashRequest? request) =>
        Run(async () => await _files.RestoreAsync(request?.Id, HttpContext.RequestAborted));

    [HttpPost("/Files/Purge")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Purge([FromBody] FileTrashRequest? request) =>
        Run(() => Task.FromResult<object>(new { removed = _files.PurgeTrash(request?.Id) }));

    /// <summary>
    /// Folders changed outside Toolkit since <paramref name="since"/> (the cursor of the previous
    /// answer; 0 the first time), for the page's tree. See <see cref="FileChangeFeed"/>.
    /// </summary>
    [HttpGet("/Files/Changes")]
    public Task<IActionResult> Changes(long since = 0) =>
        Run(async () =>
        {
            var roots = await _files.GetWatchableRootsAsync(HttpContext.RequestAborted);
            var (cursor, changes) = _changes.Read(roots, since);
            return new { cursor, changes };
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
