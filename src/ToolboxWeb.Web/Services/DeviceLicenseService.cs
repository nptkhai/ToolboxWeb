using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ToolboxWeb.Web.Infrastructure.Hosting;
using ToolkitLicense.Web.Infrastructure;

namespace ToolboxWeb.Web.Services;

/// <summary>License settings of a desktop copy (appsettings.Desktop.json, section "License").</summary>
public sealed class LicenseOptions
{
    public const string HttpClientName = "toolkit-license";

    /// <summary>Address of the license server (Toolkit License), e.g. https://license.example.com.</summary>
    public string? ServerUrl { get; set; }

    /// <summary>The server's public signing key: the PEM or the one-line base64 from its "Signing key" page.</summary>
    public string? PublicKey { get; set; }

    /// <summary>Hours between two checks with the server (heartbeat).</summary>
    public double CheckHours { get; set; } = 6;
}

public enum DeviceLicenseState
{
    /// <summary>Not a desktop, or a development desktop without a public key: nothing is checked.</summary>
    Off,

    /// <summary>A desktop outside development without a usable public key: it cannot check any license.</summary>
    NotConfigured,

    /// <summary>No license, or one that does not check out: every page leads to the activation screen.</summary>
    NotActivated,
    Active,

    /// <summary>The license ran out: read-only.</summary>
    Expired,

    /// <summary>The server said the license was revoked: read-only.</summary>
    Revoked
}

/// <summary>The license of this computer as the pages show it.</summary>
public sealed record DeviceLicenseStatus(
    DeviceLicenseState State,
    string? LicenseId,
    string? Customer,
    DateTime? IssuedAtUtc,
    DateTime? ExpiresAtUtc,
    string? ActivationStatus,
    DateTime? LastContactAtUtc,
    DeviceReleaseInfo? LatestRelease,
    bool UpdateAvailable,
    string? ServerKeyFingerprint,
    bool ServerConfigured)
{
    /// <summary>Expired or revoked: pages can be read, nothing can be changed.</summary>
    public bool IsReadOnly => State is DeviceLicenseState.Expired or DeviceLicenseState.Revoked;

    /// <summary>No usable license at all: only the activation screen opens.</summary>
    public bool IsBlocked => State is DeviceLicenseState.NotActivated or DeviceLicenseState.NotConfigured;

    public bool ExpiresSoon => State == DeviceLicenseState.Active && ExpiresAtUtc is { } end && end - DateTime.UtcNow <= TimeSpan.FromDays(14);
}

/// <summary>Result of an action: null <paramref name="ErrorKey"/> on success, else a localization key.</summary>
public sealed record LicenseActionResult(string? ErrorKey, DeviceLicenseStatus Status, string? ErrorArgument = null)
{
    public bool Succeeded => ErrorKey is null;
}

/// <summary>What a license check with the server changed, so the caller can log it.</summary>
public enum LicenseChange
{
    None,
    Activated,
    Renewed,
    Revoked
}

/// <summary>
/// The license of a desktop copy (docs/LICENSE_SERVER.md). Checking it needs no network: the
/// signature (server public key from settings), the device id and key, and the end date. The
/// server is asked only to request activation, to poll a request, and for the heartbeat, which
/// also brings renewals, revocations and the newest release.
/// </summary>
public interface IDeviceLicenseService
{
    DeviceLicenseStatus GetStatus();

    /// <summary>Checks a license text (pasted, or from the server) and saves it when it belongs to this computer.</summary>
    LicenseActionResult ApplyLicenseText(string? text);

    /// <summary>Sends the activation token to the server; an already approved device gets its license back at once.</summary>
    Task<(LicenseActionResult Result, LicenseChange Change)> RequestActivationAsync(CancellationToken cancellationToken = default);

    /// <summary>Asks the server about the pending request; saves the license once it is approved.</summary>
    Task<(LicenseActionResult Result, LicenseChange Change)> PollAsync(CancellationToken cancellationToken = default);

    /// <summary>Heartbeat: reports the running version and picks up renewals, revocations and the newest release.</summary>
    Task<(LicenseActionResult Result, LicenseChange Change)> CheckNowAsync(CancellationToken cancellationToken = default);
}

public sealed class DeviceLicenseService : IDeviceLicenseService
{
    private readonly DeviceFileStore _device;
    private readonly IDeviceIdentityService _identity;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<DeviceLicenseService> _logger;
    private readonly bool _enabled;
    private readonly string? _serverUrl;
    private readonly string? _publicKey;
    private readonly string? _fingerprint;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _cacheGate = new();

    // The last checked license text and what it said, so pages do not verify the signature each time.
    private string? _checkedText;
    private LicensePayload? _checkedPayload;

    public DeviceLicenseService(
        DeviceFileStore device,
        IDeviceIdentityService identity,
        IHttpClientFactory http,
        IOptions<LicenseOptions> options,
        AppModeInfo mode,
        IHostEnvironment environment,
        ILogger<DeviceLicenseService> logger)
    {
        _device = device;
        _identity = identity;
        _http = http;
        _logger = logger;
        _serverUrl = string.IsNullOrWhiteSpace(options.Value.ServerUrl) ? null : options.Value.ServerUrl.Trim().TrimEnd('/');
        (_publicKey, _fingerprint) = ReadPublicKey(options.Value.PublicKey);
        // A development desktop without a key keeps working as before M4.
        _enabled = mode.IsDesktop && (_publicKey is not null || !environment.IsDevelopment() || !string.IsNullOrWhiteSpace(options.Value.PublicKey));
        if (mode.IsDesktop && _publicKey is null && !string.IsNullOrWhiteSpace(options.Value.PublicKey))
        {
            logger.LogError("License:PublicKey is not a valid P-256 public key.");
        }
    }

    public DeviceLicenseStatus GetStatus()
    {
        var file = _device.Read().License ?? new DeviceLicenseFile();
        if (!_enabled)
        {
            return Build(DeviceLicenseState.Off, null, file);
        }

        if (_publicKey is null)
        {
            return Build(DeviceLicenseState.NotConfigured, null, file);
        }

        var payload = Check(file.Text);
        if (payload is null)
        {
            return Build(DeviceLicenseState.NotActivated, null, file);
        }

        var state = payload.LicenseId == file.RevokedLicenseId
            ? DeviceLicenseState.Revoked
            : payload.ExpiresAt is { } end && end <= DateTimeOffset.UtcNow
                ? DeviceLicenseState.Expired
                : DeviceLicenseState.Active;
        return Build(state, payload, file);
    }

    public LicenseActionResult ApplyLicenseText(string? text)
    {
        var (payload, error) = Verify(text);
        if (payload is null)
        {
            return new LicenseActionResult(error, GetStatus());
        }

        if (payload.ExpiresAt is { } end && end <= DateTimeOffset.UtcNow)
        {
            return new LicenseActionResult("License.Error.Expired", GetStatus());
        }

        Save(text!.Trim(), payload);
        return new LicenseActionResult(null, GetStatus());
    }

    public async Task<(LicenseActionResult Result, LicenseChange Change)> RequestActivationAsync(CancellationToken cancellationToken = default)
    {
        if (_serverUrl is null || _publicKey is null)
        {
            return (new LicenseActionResult("License.Error.NoServer", GetStatus()), LicenseChange.None);
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var before = GetStatus();
            var (reply, error, argument) = await SendAsync(HttpMethod.Post, "/api/activations", new { token = _identity.CreateToken() }, cancellationToken);
            if (reply is null)
            {
                return (new LicenseActionResult(error, GetStatus(), argument), LicenseChange.None);
            }

            return HandleActivationReply(reply.Value, before);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<(LicenseActionResult Result, LicenseChange Change)> PollAsync(CancellationToken cancellationToken = default)
    {
        var requestId = _device.Read().License?.ActivationRequestId;
        if (_serverUrl is null || _publicKey is null || string.IsNullOrEmpty(requestId))
        {
            return (new LicenseActionResult(null, GetStatus()), LicenseChange.None);
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var before = GetStatus();
            var (reply, error, argument) = await SendAsync(HttpMethod.Get, "/api/activations/" + Uri.EscapeDataString(requestId), null, cancellationToken);
            if (reply is null)
            {
                if (error == "License.Error.Server.notFound")
                {
                    // The server no longer knows the request: start again with a new one.
                    UpdateFile(file => file with { ActivationRequestId = null, ActivationStatus = null });
                }

                return (new LicenseActionResult(error, GetStatus(), argument), LicenseChange.None);
            }

            return HandleActivationReply(reply.Value, before);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<(LicenseActionResult Result, LicenseChange Change)> CheckNowAsync(CancellationToken cancellationToken = default)
    {
        if (_serverUrl is null || _publicKey is null)
        {
            return (new LicenseActionResult("License.Error.NoServer", GetStatus()), LicenseChange.None);
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var before = GetStatus();
            var held = Check(_device.Read().License?.Text);
            var (reply, error, argument) = await SendAsync(HttpMethod.Post, "/api/devices/heartbeat", new { heartbeat = _identity.CreateHeartbeat(held?.LicenseId) }, cancellationToken);
            if (reply is null)
            {
                // Unknown device: it never asked for activation; nothing to report.
                return error == "License.Error.Server.unknownDevice"
                    ? (new LicenseActionResult(null, GetStatus()), LicenseChange.None)
                    : (new LicenseActionResult(error, GetStatus(), argument), LicenseChange.None);
            }

            var root = reply.Value;
            var license = root.GetProperty("license");
            var status = license.GetProperty("status").GetString();
            var change = LicenseChange.None;
            if (license.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
            {
                var applied = ApplyFromServer(text.GetString());
                if (applied is not null)
                {
                    change = before.State == DeviceLicenseState.Active ? LicenseChange.Renewed : LicenseChange.Activated;
                }
            }
            else if (status == "revoked" && held is not null)
            {
                UpdateFile(file => file with { RevokedLicenseId = held.LicenseId });
                change = before.State == DeviceLicenseState.Revoked ? LicenseChange.None : LicenseChange.Revoked;
            }
            else if (status == "active" && held is not null && held.LicenseId == _device.Read().License?.RevokedLicenseId)
            {
                // Revoked earlier by mistake and active again on the server.
                UpdateFile(file => file with { RevokedLicenseId = null });
                change = LicenseChange.Renewed;
            }

            var release = ReadRelease(root);
            var updateAvailable = root.TryGetProperty("updateAvailable", out var update) && update.ValueKind == JsonValueKind.True;
            UpdateFile(file => file with { LastContactAtUtc = DateTime.UtcNow, LatestRelease = release, UpdateAvailable = updateAvailable });
            return (new LicenseActionResult(null, GetStatus()), change);
        }
        finally
        {
            _gate.Release();
        }
    }

    // ------------------------------------------------------------------ helpers

    private (LicenseActionResult, LicenseChange) HandleActivationReply(JsonElement root, DeviceLicenseStatus before)
    {
        var requestId = root.TryGetProperty("requestId", out var id) ? id.GetString() : null;
        var status = root.TryGetProperty("status", out var s) ? s.GetString() : null;
        UpdateFile(file => file with { ActivationRequestId = requestId, ActivationStatus = status, LastContactAtUtc = DateTime.UtcNow });
        if (status == "approved" && root.TryGetProperty("license", out var license) && license.ValueKind == JsonValueKind.String)
        {
            var error = ApplyFromServer(license.GetString()) is null ? "License.Error.FromServer" : null;
            if (error is null)
            {
                UpdateFile(file => file with { ActivationRequestId = null });
                return (new LicenseActionResult(null, GetStatus()), before.State == DeviceLicenseState.Active ? LicenseChange.Renewed : LicenseChange.Activated);
            }

            return (new LicenseActionResult(error, GetStatus()), LicenseChange.None);
        }

        return (new LicenseActionResult(null, GetStatus()), LicenseChange.None);
    }

    private LicensePayload? ApplyFromServer(string? text)
    {
        var (payload, error) = Verify(text);
        if (payload is null)
        {
            _logger.LogWarning("The license server sent a license that does not check out ({Error}).", error);
            return null;
        }

        Save(text!.Trim(), payload);
        return payload;
    }

    private void Save(string text, LicensePayload payload)
    {
        UpdateFile(file => file with
        {
            Text = text,
            RevokedLicenseId = file.RevokedLicenseId == payload.LicenseId ? file.RevokedLicenseId : null,
            ActivationStatus = "approved"
        });
        _logger.LogInformation("License {LicenseId} saved (ends {End}).", payload.LicenseId, payload.ExpiresAt?.ToString("u") ?? "never");
    }

    private void UpdateFile(Func<DeviceLicenseFile, DeviceLicenseFile> change) =>
        _device.Update(file => file with { License = change(file.License ?? new DeviceLicenseFile()) });

    private LicensePayload? Check(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || _publicKey is null)
        {
            return null;
        }

        lock (_cacheGate)
        {
            if (text == _checkedText)
            {
                return _checkedPayload;
            }
        }

        var (payload, _) = Verify(text);
        lock (_cacheGate)
        {
            _checkedText = text;
            _checkedPayload = payload;
        }

        return payload;
    }

    /// <summary>Signature with the server key, then the device id and key of this computer.</summary>
    private (LicensePayload? Payload, string? ErrorKey) Verify(string? text)
    {
        if (_publicKey is null)
        {
            return (null, "License.Error.NoServer");
        }

        if (!WireFormat.TryDecode(text, WireFormat.LicensePrefix, out var bytes, out var signature))
        {
            return (null, "License.Error.Malformed");
        }

        if (!WireFormat.Verify(_publicKey, bytes, signature))
        {
            return (null, "License.Error.Signature");
        }

        var payload = WireFormat.Read<LicensePayload>(bytes);
        if (payload is null || payload.V != 1)
        {
            return (null, "License.Error.Malformed");
        }

        var identity = _identity.Get();
        if (payload.DeviceId != identity.DeviceId || payload.DeviceKey != _identity.KeyThumbprint)
        {
            return (null, "License.Error.OtherDevice");
        }

        return (payload, null);
    }

    private async Task<(JsonElement? Reply, string? ErrorKey, string? Argument)> SendAsync(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        try
        {
            var client = _http.CreateClient(LicenseOptions.HttpClientName);
            using var request = new HttpRequestMessage(method, _serverUrl + path);
            if (body is not null)
            {
                request.Content = JsonContent.Create(body);
            }

            using var response = await client.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            JsonElement? json = null;
            if (!string.IsNullOrWhiteSpace(content))
            {
                try
                {
                    json = JsonDocument.Parse(content).RootElement.Clone();
                }
                catch (JsonException)
                {
                    return (null, "License.Error.NotLicenseServer", null);
                }
            }

            if (response.IsSuccessStatusCode && json is not null)
            {
                return (json, null, null);
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                return (null, "License.Error.TooMany", null);
            }

            var code = json is { } error && error.ValueKind == JsonValueKind.Object && error.TryGetProperty("error", out var e) ? e.GetString() : null;
            return code is null
                ? (null, "License.Error.ServerStatus", ((int)response.StatusCode).ToString(System.Globalization.CultureInfo.InvariantCulture))
                : (null, "License.Error.Server." + code, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("License server {Url} not reachable: {Message}", _serverUrl, ex.Message);
            return (null, "License.Error.Unreachable", null);
        }
    }

    private static DeviceReleaseInfo? ReadRelease(JsonElement root)
    {
        if (!root.TryGetProperty("latestRelease", out var release) || release.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        string? Text(string name) => release.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        return new DeviceReleaseInfo(
            Text("version") ?? string.Empty,
            Text("notes"),
            release.TryGetProperty("mandatory", out var mandatory) && mandatory.ValueKind == JsonValueKind.True,
            Text("downloadUrl"),
            release.TryGetProperty("publishedAt", out var published) && published.TryGetDateTimeOffset(out var at) ? at.UtcDateTime : null);
    }

    private DeviceLicenseStatus Build(DeviceLicenseState state, LicensePayload? payload, DeviceLicenseFile file) => new(
        state,
        payload?.LicenseId,
        string.IsNullOrWhiteSpace(payload?.Customer) ? null : payload.Customer,
        payload?.IssuedAt.UtcDateTime,
        payload?.ExpiresAt?.UtcDateTime,
        file.ActivationStatus,
        file.LastContactAtUtc,
        file.LatestRelease,
        file.UpdateAvailable,
        _fingerprint,
        _serverUrl is not null);

    /// <summary>Accepts the PEM or the one-line base64 (SubjectPublicKeyInfo) of a P-256 key.</summary>
    private static (string? Base64, string? Fingerprint) ReadPublicKey(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            return (null, null);
        }

        var base64 = string.Concat(configured
            .Replace("-----BEGIN PUBLIC KEY-----", string.Empty)
            .Replace("-----END PUBLIC KEY-----", string.Empty)
            .Where(c => !char.IsWhiteSpace(c)));
        try
        {
            var bytes = Convert.FromBase64String(base64);
            using var key = ECDsa.Create();
            key.ImportSubjectPublicKeyInfo(bytes, out _);
            return key.KeySize == 256
                ? (base64, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant())
                : (null, null);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            return (null, null);
        }
    }
}
