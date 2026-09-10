using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using ToolboxWeb.Web.Constants;
using ToolboxWeb.Web.Infrastructure.SqlSchema;
using ToolboxWeb.Web.Services;
using ToolboxWeb.Web.ViewModels.SchemaCompare;

namespace ToolboxWeb.Web.Controllers;

[Authorize]
public class SchemaCompareController : Controller
{
    private const string SessionMarkerKey = "Toolbox.SchemaCompare.Active";

    private readonly ISchemaCompareService _schemaCompare;
    private readonly IDacFxSchemaCompareService _dacFx;
    private readonly ISqlConnectionStringFactory _connectionStrings;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly ILogger<SchemaCompareController> _logger;

    public SchemaCompareController(
        ISchemaCompareService schemaCompare,
        IDacFxSchemaCompareService dacFx,
        ISqlConnectionStringFactory connectionStrings,
        IStringLocalizer<SharedResource> localizer,
        ILogger<SchemaCompareController> logger)
    {
        _schemaCompare = schemaCompare;
        _dacFx = dacFx;
        _connectionStrings = connectionStrings;
        _localizer = localizer;
        _logger = logger;
    }

    [HttpGet("/" + ToolboxRouteSlugs.Pages.SchemaCompare)]
    [HttpGet("/" + ToolboxRouteSlugs.HtmlPages.SchemaCompare, Name = ToolboxRouteSlugs.RouteNames.SchemaCompareHtml)]
    public IActionResult Index()
    {
        return View(new SchemaComparePageViewModel
        {
            Options = new SchemaCompareOptions(),
            HostAllowlistEnabled = _connectionStrings.HasHostAllowlist
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestConnection([FromBody] SqlConnectionInputModel input, CancellationToken cancellationToken)
    {
        if (input is null)
        {
            return Json(SchemaCompareApiResponse<object>.Fail(_localizer["SchemaCompare.Error.InvalidRequest"].Value));
        }

        try
        {
            var probe = await _schemaCompare.ProbeAsync(input, cancellationToken);
            return Json(SchemaCompareApiResponse<SqlServerProbeViewModel>.Ok(probe));
        }
        catch (Exception ex)
        {
            return Json(SchemaCompareApiResponse<object>.Fail(Describe(ex, "SchemaCompare.Error.ConnectionFailed", input.Server)));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Compare([FromBody] SchemaCompareRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Json(SchemaCompareApiResponse<object>.Fail(_localizer["SchemaCompare.Error.InvalidRequest"].Value));
        }

        try
        {
            var result = await _schemaCompare.CompareAsync(request, BrowserSessionId(), cancellationToken);
            return Json(SchemaCompareApiResponse<SchemaCompareResultViewModel>.Ok(result));
        }
        catch (Exception ex)
        {
            return Json(SchemaCompareApiResponse<object>.Fail(Describe(ex, "SchemaCompare.Error.CompareFailed", request.Source.Server)));
        }
    }

    [HttpGet]
    public IActionResult Script(string resultId)
    {
        var stored = _schemaCompare.GetStoredResult(resultId ?? string.Empty, BrowserSessionId());
        if (stored is null)
        {
            return NotFound(_localizer["SchemaCompare.Error.ResultNotFound"].Value);
        }

        var fileName = string.Create(
            CultureInfo.InvariantCulture,
            $"schema-sync-{stored.CreatedAt:yyyyMMdd-HHmmss}.sql");

        // UTF-8 with BOM so SSMS opens Unicode object names correctly.
        return File(new UTF8Encoding(true).GetBytes(stored.Script), "application/sql", fileName);
    }

    // ------------------------------------------------------------ DacFx engine

    /// <summary>
    /// Kicks off the comparison and returns straight away. Extracting both databases takes
    /// seconds, so the browser polls <see cref="Progress"/> to show the running log.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult StartCompare([FromBody] DacFxCompareRequest request)
    {
        if (request is null)
        {
            return Json(SchemaCompareApiResponse<object>.Fail(_localizer["SchemaCompare.Error.InvalidRequest"].Value));
        }

        try
        {
            var comparisonId = _dacFx.Start(request, BrowserSessionId());
            return Json(SchemaCompareApiResponse<object>.Ok(new { comparisonId }));
        }
        catch (Exception ex)
        {
            return Json(SchemaCompareApiResponse<object>.Fail(Describe(ex, "SchemaCompare.Error.CompareFailed", request.Source.Server)));
        }
    }

    /// <summary>
    /// Stops a comparison that is still running. Extracting a large database can take many
    /// minutes, and there is otherwise no way out but to wait for the deadline.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult CancelCompare([FromBody] CancelCompareRequest request)
    {
        var stopped = request is not null
            && _dacFx.Cancel(request.ComparisonId ?? string.Empty, BrowserSessionId());

        return stopped
            ? Json(SchemaCompareApiResponse<object>.Ok(new { stopped = true }))
            : Json(SchemaCompareApiResponse<object>.Fail(_localizer["SchemaCompare.Error.ResultNotFound"].Value));
    }

    [HttpGet]
    public IActionResult Progress(string comparisonId)
    {
        var progress = _dacFx.GetProgress(comparisonId ?? string.Empty, BrowserSessionId());
        return progress is null
            ? Json(SchemaCompareApiResponse<object>.Fail(_localizer["SchemaCompare.Error.ResultNotFound"].Value))
            : Json(SchemaCompareApiResponse<CompareProgressViewModel>.Ok(progress));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleInclude([FromBody] ToggleIncludeRequest request)
    {
        if (request is null)
        {
            return Json(SchemaCompareApiResponse<object>.Fail(_localizer["SchemaCompare.Error.InvalidRequest"].Value));
        }

        var result = await _dacFx.ToggleAsync(request, BrowserSessionId());
        return result is null
            ? Json(SchemaCompareApiResponse<object>.Fail(_localizer["SchemaCompare.Error.ResultNotFound"].Value))
            : Json(SchemaCompareApiResponse<ToggleIncludeResultViewModel>.Ok(result));
    }

    [HttpGet]
    public async Task<IActionResult> DiffDetail(string comparisonId, string differenceId)
    {
        var detail = await _dacFx.GetDetailAsync(
            comparisonId ?? string.Empty,
            differenceId ?? string.Empty,
            BrowserSessionId());

        return detail is null
            ? Json(SchemaCompareApiResponse<object>.Fail(_localizer["SchemaCompare.Error.ResultNotFound"].Value))
            : Json(SchemaCompareApiResponse<DiffDetailViewModel>.Ok(detail));
    }

    [HttpGet]
    public async Task<IActionResult> GenerateScript(string comparisonId)
    {
        try
        {
            var script = await _dacFx.GetScriptAsync(comparisonId ?? string.Empty, BrowserSessionId());
            return script is null
                ? Json(SchemaCompareApiResponse<object>.Fail(_localizer["SchemaCompare.Error.ResultNotFound"].Value))
                : Json(SchemaCompareApiResponse<ScriptViewModel>.Ok(script));
        }
        catch (Exception ex)
        {
            return Json(SchemaCompareApiResponse<object>.Fail(Describe(ex, "SchemaCompare.Error.ScriptFailed", null)));
        }
    }

    [HttpGet]
    public async Task<IActionResult> DownloadScript(string comparisonId)
    {
        var script = await _dacFx.GetScriptAsync(comparisonId ?? string.Empty, BrowserSessionId());
        if (script is null)
        {
            return NotFound(_localizer["SchemaCompare.Error.ResultNotFound"].Value);
        }

        var fileName = string.Create(
            CultureInfo.InvariantCulture,
            $"schema-sync-{DateTime.Now:yyyyMMdd-HHmmss}.sql");

        // UTF-8 with BOM so SSMS opens Unicode object names correctly.
        return File(new UTF8Encoding(true).GetBytes(script.Script), "application/sql", fileName);
    }

    private string Describe(Exception exception, string fallbackKey, string? server)
    {
        _logger.LogWarning(
            exception,
            "Schema compare failed for server {Server}: {Reason}",
            SqlSchemaErrorHelper.Scrub(server),
            SqlSchemaErrorHelper.Scrub(exception.Message));

        return SqlSchemaErrorHelper.ToUserMessage(
            exception,
            _localizer[fallbackKey].Value,
            _localizer["SchemaCompare.Error.LoginFailed"].Value,
            _localizer["SchemaCompare.Error.ServerNotFound"].Value,
            _localizer["SchemaCompare.Error.DatabaseNotFound"].Value,
            _localizer["SchemaCompare.Error.PermissionDenied"].Value,
            _localizer["SchemaCompare.Error.Timeout"].Value);
    }

    /// <summary>
    /// Session ids are only stable once something has been written, so set a marker first.
    /// The generated script is tied to this id and cannot be downloaded from another session.
    /// </summary>
    private string BrowserSessionId()
    {
        HttpContext.Session.SetString(SessionMarkerKey, "1");
        return HttpContext.Session.Id;
    }
}
