using System.Text.Json;

namespace ToolboxWeb.Web.Infrastructure.Hosting;

/// <summary>
/// <c>device.json</c> in the data folder: what belongs to this computer rather than to an account
/// (the desktop owner, the Jira connection, the device id and key, the license).
/// </summary>
public sealed class DeviceFileStore
{
    private const string FileName = "device.json";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly string _path;
    private readonly string _folder;
    private readonly ILogger<DeviceFileStore> _logger;
    private readonly object _gate = new();
    // Only this process writes the file, so it is read once and kept (it is read on every page).
    private DeviceFile? _cached;

    public DeviceFileStore(AppModeInfo mode, ILogger<DeviceFileStore> logger)
    {
        _folder = mode.DataRoot;
        _path = Path.Combine(mode.DataRoot, FileName);
        _logger = logger;
    }

    public DeviceFile Read()
    {
        lock (_gate)
        {
            if (_cached is not null)
            {
                return _cached;
            }

            try
            {
                _cached = File.Exists(_path)
                    ? JsonSerializer.Deserialize<DeviceFile>(File.ReadAllText(_path), Json) ?? new DeviceFile()
                    : new DeviceFile();
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "{Path} could not be read; it is treated as empty.", _path);
                _cached = new DeviceFile();
            }

            return _cached;
        }
    }

    /// <summary>Reads, changes and writes the file in one step (written to a temporary file, then moved).</summary>
    public DeviceFile Update(Func<DeviceFile, DeviceFile> change)
    {
        lock (_gate)
        {
            var updated = change(Read());
            Directory.CreateDirectory(_folder);
            var temporary = _path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(updated, Json));
            File.Move(temporary, _path, overwrite: true);
            _cached = updated;
            return updated;
        }
    }
}

public sealed record DeviceFile(
    string? OwnerId = null,
    DeviceJiraSettings? Jira = null,
    DeviceIdentity? Identity = null,
    DeviceLicenseFile? License = null);

/// <summary>
/// This computer's own id and key pair, made on first need (DeviceIdentityService). The private
/// key is encrypted with Data Protection, so a copy of the data folder on another computer cannot
/// use it; the machine name it was made on is kept for the same reason.
/// </summary>
public sealed record DeviceIdentity(string DeviceId, string MachineName, string PublicKey, string ProtectedPrivateKey, DateTime CreatedAtUtc);

/// <summary>
/// The license of this computer and what the license server last said (DeviceLicenseService).
/// <paramref name="Text"/> is the signed TKL1 license; it is checked again on every start.
/// </summary>
public sealed record DeviceLicenseFile(
    string? Text = null,
    string? ActivationRequestId = null,
    string? ActivationStatus = null,
    string? RevokedLicenseId = null,
    DateTime? LastContactAtUtc = null,
    DeviceReleaseInfo? LatestRelease = null,
    bool UpdateAvailable = false);

/// <summary>The newest Toolkit release, as the license server reported it.</summary>
public sealed record DeviceReleaseInfo(string Version, string? Notes, bool Mandatory, string? DownloadUrl, DateTime? PublishedAtUtc);

/// <summary>The Jira connection saved on this computer. The password or token is encrypted (Data Protection).</summary>
public sealed record DeviceJiraSettings(
    string BaseUrl,
    string Kind,
    string? Username,
    string ProtectedSecret,
    string? DisplayName,
    DateTime? VerifiedAtUtc);
