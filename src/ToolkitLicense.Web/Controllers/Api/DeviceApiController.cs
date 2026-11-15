using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ToolkitLicense.Web.Domain;
using ToolkitLicense.Web.Services;

namespace ToolkitLicense.Web.Controllers.Api;

public sealed record TokenBody(string? Token);

public sealed record HeartbeatBody(string? Heartbeat);

/// <summary>
/// What the Toolkit desktop app calls (M4). No sign-in: requests are signed with the device's own
/// key, and the answers carry nothing secret (a license only works with the device's private key).
/// Errors are <c>{ "error": "malformed" | "badSignature" | "badDevice" | "keyMismatch" | "unknownDevice" | "stale" | "notFound" }</c>.
/// </summary>
[ApiController]
[Route("api")]
[AllowAnonymous]
[IgnoreAntiforgeryToken]
[EnableRateLimiting(RateLimits.Api)]
public sealed class DeviceApiController(LicenseService licenses, ReleaseService releases) : ControllerBase
{
    /// <summary>Sends an activation token; answers the request id to poll and its status.</summary>
    [HttpPost("activations")]
    public async Task<IActionResult> Submit([FromBody] TokenBody body)
    {
        var result = await licenses.SubmitAsync(body.Token, ActivationSource.Online, HttpContext.Connection.RemoteIpAddress?.ToString());
        if (result.Request is null)
        {
            return result.Error == TokenError.KeyMismatch ? Conflict(Error(result.Error)) : BadRequest(Error(result.Error));
        }

        return await PollAsync(result.Request.Id, StatusCodes.Status202Accepted);
    }

    /// <summary>The status of a request; carries the license once approved.</summary>
    [HttpGet("activations/{id}")]
    public Task<IActionResult> Poll(string id) => PollAsync(id, StatusCodes.Status200OK);

    [HttpPost("devices/heartbeat")]
    public async Task<IActionResult> Heartbeat([FromBody] HeartbeatBody body)
    {
        var (answer, error) = await licenses.HeartbeatAsync(body.Heartbeat);
        if (answer is null)
        {
            return error == TokenError.UnknownDevice ? NotFound(Error(error)) : BadRequest(Error(error));
        }

        return Ok(new
        {
            license = new { status = answer.LicenseStatus, licenseId = answer.LicenseId, expiresAt = Utc(answer.ExpiresAtUtc), text = answer.LicenseText },
            latestRelease = ReleaseJson(answer.Latest),
            updateAvailable = answer.UpdateAvailable,
            updateRequired = answer.UpdateRequired,
            serverTime = DateTimeOffset.UtcNow
        });
    }

    [HttpGet("releases/latest")]
    public async Task<IActionResult> Latest()
    {
        var latest = await releases.LatestAsync();
        return latest is null ? NoContent() : Ok(ReleaseJson(latest));
    }

    private async Task<IActionResult> PollAsync(string id, int statusCode)
    {
        var (request, license) = await licenses.PollAsync(id);
        if (request is null)
        {
            return NotFound(new { error = "notFound" });
        }

        var status = request.Status switch
        {
            ActivationStatus.Approved when license is null => "revoked",
            ActivationStatus.Approved => "approved",
            ActivationStatus.Rejected => "rejected",
            _ => "pending"
        };
        return StatusCode(statusCode, new
        {
            requestId = request.Id,
            status,
            license = status == "approved" ? license!.Text : null,
            expiresAt = status == "approved" ? Utc(license!.ExpiresAtUtc) : null
        });
    }

    private static object Error(TokenError error) => new { error = JsonNamingPolicyCamel(error.ToString()) };

    private static string JsonNamingPolicyCamel(string name) => char.ToLowerInvariant(name[0]) + name[1..];

    private static DateTimeOffset? Utc(DateTime? value) => value is null ? null : new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc));

    private static object? ReleaseJson(Release? release) => release is null ? null : new
    {
        version = release.Version,
        notes = release.Notes,
        mandatory = release.Mandatory,
        downloadUrl = release.DownloadUrl,
        publishedAt = Utc(release.PublishedAtUtc)
    };
}
