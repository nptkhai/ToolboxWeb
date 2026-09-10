using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using ToolboxWeb.Web.Constants;
using ToolboxWeb.Web.Infrastructure.Excel;
using ToolboxWeb.Web.Infrastructure.SqlSchema;
using ToolboxWeb.Web.Services;
using ToolboxWeb.Web.ViewModels.SchemaCompare;
using ToolboxWeb.Web.ViewModels.SqlProfiler;

namespace ToolboxWeb.Web.Controllers;

[Authorize]
public class SqlProfilerController : Controller
{
    private const string SessionMarkerKey = "Toolbox.SqlProfiler.Active";

    private readonly ISqlProfilerService _profiler;
    private readonly ISqlConnectionStringFactory _connectionStrings;
    private readonly IExcelReportService _excel;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly ILogger<SqlProfilerController> _logger;

    public SqlProfilerController(
        ISqlProfilerService profiler,
        ISqlConnectionStringFactory connectionStrings,
        IExcelReportService excel,
        IStringLocalizer<SharedResource> localizer,
        ILogger<SqlProfilerController> logger)
    {
        _profiler = profiler;
        _connectionStrings = connectionStrings;
        _excel = excel;
        _localizer = localizer;
        _logger = logger;
    }

    [HttpGet("/" + ToolboxRouteSlugs.Pages.SqlProfiler)]
    [HttpGet("/" + ToolboxRouteSlugs.HtmlPages.SqlProfiler, Name = ToolboxRouteSlugs.RouteNames.SqlProfilerHtml)]
    public IActionResult Index()
    {
        return View(new ProfilerPageViewModel
        {
            Options = new ProfilerCaptureOptions(),
            HostAllowlistEnabled = _connectionStrings.HasHostAllowlist
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestConnection([FromBody] SqlConnectionInputModel input, CancellationToken cancellationToken)
    {
        if (input is null)
        {
            return Fail("SqlProfiler.Error.InvalidRequest");
        }

        try
        {
            var permissions = await _profiler.CheckAsync(input, cancellationToken);
            var databases = await _profiler.ListDatabasesAsync(input, cancellationToken);

            return Json(SchemaCompareApiResponse<object>.Ok(new
            {
                serverVersion = permissions.ServerVersion,
                canCapture = permissions.CanCapture,
                canUseRecent = permissions.CanUseRecentProcedures,
                isAzure = permissions.IsAzureSqlDatabase,
                databases
            }));
        }
        catch (Exception ex)
        {
            return Json(SchemaCompareApiResponse<object>.Fail(Describe(ex, "SqlProfiler.Error.ConnectionFailed", input.Server)));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartCapture([FromBody] StartCaptureRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Fail("SqlProfiler.Error.InvalidRequest");
        }

        try
        {
            var captureId = await _profiler.StartCaptureAsync(request, BrowserSessionId(), cancellationToken);
            return Json(SchemaCompareApiResponse<object>.Ok(new { captureId }));
        }
        catch (Exception ex)
        {
            return Json(SchemaCompareApiResponse<object>.Fail(Describe(ex, "SqlProfiler.Error.StartFailed", request.Connection.Server)));
        }
    }

    [HttpGet]
    public async Task<IActionResult> Events(string captureId, bool mask, CancellationToken cancellationToken)
    {
        var state = await _profiler.ReadAsync(captureId ?? string.Empty, BrowserSessionId(), mask, cancellationToken);
        return state is null
            ? Fail("SqlProfiler.Error.CaptureNotFound")
            : Json(SchemaCompareApiResponse<CaptureStateViewModel>.Ok(state));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StopCapture(string captureId, bool mask, CancellationToken cancellationToken)
    {
        var state = await _profiler.StopAsync(captureId ?? string.Empty, BrowserSessionId(), mask, cancellationToken);
        return state is null
            ? Fail("SqlProfiler.Error.CaptureNotFound")
            : Json(SchemaCompareApiResponse<CaptureStateViewModel>.Ok(state));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecentProcedures(
        [FromBody] SqlConnectionInputModel input,
        int minutes,
        CancellationToken cancellationToken)
    {
        if (input is null)
        {
            return Fail("SqlProfiler.Error.InvalidRequest");
        }

        try
        {
            var result = await _profiler.ReadRecentAsync(input, minutes, cancellationToken);
            return Json(SchemaCompareApiResponse<RecentProceduresViewModel>.Ok(result));
        }
        catch (Exception ex)
        {
            return Json(SchemaCompareApiResponse<object>.Fail(Describe(ex, "SqlProfiler.Error.RecentFailed", input.Server)));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Definition(
        [FromBody] SqlConnectionInputModel input,
        string objectName,
        CancellationToken cancellationToken)
    {
        if (input is null || string.IsNullOrWhiteSpace(objectName))
        {
            return Fail("SqlProfiler.Error.InvalidRequest");
        }

        try
        {
            var detail = await _profiler.ReadDefinitionAsync(input, objectName, cancellationToken);
            return Json(SchemaCompareApiResponse<ProfilerDetailViewModel>.Ok(detail));
        }
        catch (Exception ex)
        {
            return Json(SchemaCompareApiResponse<object>.Fail(Describe(ex, "SqlProfiler.Error.DefinitionFailed", input.Server)));
        }
    }

    /// <summary>
    /// Best-effort stop from <c>navigator.sendBeacon</c> when the tab closes, which cannot
    /// carry an antiforgery header. It only ever stops a capture owned by the caller's own
    /// session, so the worst a forged request can do is end that user's own recording.
    /// </summary>
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Abandon(string captureId, CancellationToken cancellationToken)
    {
        await _profiler.StopAsync(captureId ?? string.Empty, BrowserSessionId(), false, cancellationToken);
        return NoContent();
    }

    [HttpGet]
    public async Task<IActionResult> Export(string captureId, bool mask, CancellationToken cancellationToken)
    {
        var state = await _profiler.ReadAsync(captureId ?? string.Empty, BrowserSessionId(), mask, cancellationToken);
        if (state is null)
        {
            return NotFound(_localizer["SqlProfiler.Error.CaptureNotFound"].Value);
        }

        // Flattened so the sheet has one simple column per field.
        var rows = state.Events.Select(x => new
        {
            x.Time,
            x.Kind,
            Object = x.ObjectName,
            x.Database,
            User = x.UserName,
            Host = x.ClientHost,
            Application = x.AppName,
            Spid = x.SessionId,
            DurationMs = x.DurationMs,
            CpuMs = x.CpuMs,
            Reads = x.LogicalReads,
            Rows = x.RowCount,
            Error = x.ErrorMessage,
            x.Statement
        }).ToArray();

        var bytes = await _excel.GenerateAsync("SqlProfiler", rows);
        var fileName = $"sql-profiler-{DateTime.Now:yyyyMMdd-HHmmss}.csv";
        return File(bytes, "text/csv", fileName);
    }

    private IActionResult Fail(string key) =>
        Json(SchemaCompareApiResponse<object>.Fail(_localizer[key].Value));

    private string Describe(Exception exception, string fallbackKey, string? server)
    {
        _logger.LogWarning(
            exception,
            "SQL profiler failed for server {Server}: {Reason}",
            SqlSchemaErrorHelper.Scrub(server),
            SqlSchemaErrorHelper.Scrub(exception.Message));

        return SqlSchemaErrorHelper.ToUserMessage(
            exception,
            _localizer[fallbackKey].Value,
            _localizer["SchemaCompare.Error.LoginFailed"].Value,
            _localizer["SchemaCompare.Error.ServerNotFound"].Value,
            _localizer["SchemaCompare.Error.DatabaseNotFound"].Value,
            _localizer["SqlProfiler.Error.PermissionDenied"].Value,
            _localizer["SchemaCompare.Error.Timeout"].Value);
    }

    private string BrowserSessionId()
    {
        HttpContext.Session.SetString(SessionMarkerKey, "1");
        return HttpContext.Session.Id;
    }
}
