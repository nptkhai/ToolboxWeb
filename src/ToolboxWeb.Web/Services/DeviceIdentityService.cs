using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using ToolboxWeb.Web.Infrastructure.Hosting;
using ToolkitLicense.Web.Infrastructure;

namespace ToolboxWeb.Web.Services;

/// <summary>
/// The desktop's own identity for the license server: a device id and an ECDSA P-256 key pair,
/// made on first need and kept in device.json (private key encrypted with Data Protection). It
/// signs the activation token (TKT1) and the heartbeat (TKH1); see docs/LICENSE_SERVER.md.
/// </summary>
public interface IDeviceIdentityService
{
    /// <summary>The identity of this computer, made now when there is none or the saved one belongs to another computer.</summary>
    DeviceIdentity Get();

    /// <summary>The activation token to send to the license server or to copy by hand.</summary>
    string CreateToken();

    /// <summary>A signed heartbeat telling the server the running version and the license held.</summary>
    string CreateHeartbeat(string? licenseId);

    /// <summary>base64url(SHA-256(public key)): what a license names as <c>deviceKey</c>.</summary>
    string KeyThumbprint { get; }
}

public sealed class DeviceIdentityService : IDeviceIdentityService
{
    private const string KeyPurpose = "Toolkit.Desktop.DeviceKey.v1";

    private readonly DeviceFileStore _device;
    private readonly IDataProtector _protector;
    private readonly ILogger<DeviceIdentityService> _logger;
    private readonly object _gate = new();
    private ECDsa? _key;
    private DeviceIdentity? _identity;

    public DeviceIdentityService(DeviceFileStore device, IDataProtectionProvider protection, ILogger<DeviceIdentityService> logger)
    {
        _device = device;
        _protector = protection.CreateProtector(KeyPurpose);
        _logger = logger;
    }

    public string KeyThumbprint => WireFormat.KeyThumbprint(Get().PublicKey);

    public DeviceIdentity Get()
    {
        lock (_gate)
        {
            if (_identity is not null && _key is not null)
            {
                return _identity;
            }

            var saved = _device.Read().Identity;
            if (saved is not null && TryLoad(saved, out var key))
            {
                _key = key;
                _identity = saved;
                return saved;
            }

            return Create();
        }
    }

    public string CreateToken()
    {
        var identity = Get();
        return Sign(WireFormat.TokenPrefix, new DeviceToken(
            1,
            identity.DeviceId,
            identity.MachineName,
            Environment.UserName,
            AppVersion.Version,
            identity.PublicKey,
            DateTimeOffset.UtcNow));
    }

    public string CreateHeartbeat(string? licenseId)
    {
        var identity = Get();
        return Sign(WireFormat.HeartbeatPrefix, new HeartbeatPayload(1, identity.DeviceId, AppVersion.Version, licenseId, DateTimeOffset.UtcNow));
    }

    private string Sign(string prefix, object payload)
    {
        Get();
        lock (_gate)
        {
            var key = _key!;
            return WireFormat.Encode(prefix, payload, data => key.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
        }
    }

    // A saved identity is used only on the computer that made it, with keys that still open it.
    private bool TryLoad(DeviceIdentity saved, out ECDsa? key)
    {
        key = null;
        if (!string.Equals(saved.MachineName, Environment.MachineName, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("device.json was made on {Saved}, not on {Current}; this computer gets a new device id.", saved.MachineName, Environment.MachineName);
            return false;
        }

        try
        {
            var loaded = ECDsa.Create();
            loaded.ImportPkcs8PrivateKey(Convert.FromBase64String(_protector.Unprotect(saved.ProtectedPrivateKey)), out _);
            if (Convert.ToBase64String(loaded.ExportSubjectPublicKeyInfo()) != saved.PublicKey)
            {
                loaded.Dispose();
                return false;
            }

            key = loaded;
            return true;
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            _logger.LogWarning(ex, "The device key in device.json cannot be opened (data folder copied from another computer?); this computer gets a new device id.");
            return false;
        }
    }

    private DeviceIdentity Create()
    {
        var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var identity = new DeviceIdentity(
            Guid.NewGuid().ToString("D"),
            Environment.MachineName,
            Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),
            _protector.Protect(Convert.ToBase64String(key.ExportPkcs8PrivateKey())),
            DateTime.UtcNow);

        // A new identity cannot use a license made for the old one.
        _device.Update(file => file with { Identity = identity, License = null });
        _key?.Dispose();
        _key = key;
        _identity = identity;
        _logger.LogInformation("Created device id {DeviceId}.", identity.DeviceId);
        return identity;
    }
}
