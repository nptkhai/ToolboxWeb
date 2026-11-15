using Microsoft.AspNetCore.Identity;

namespace ToolkitLicense.Web.Domain;

/// <summary>The administrator who signs in to this app (only the owner).</summary>
public sealed class AdminUser : IdentityUser
{
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>A person who uses Toolkit on one or more devices. Holds no work data.</summary>
public sealed class Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<Device> Devices { get; set; } = [];
}

/// <summary>
/// One Toolkit desktop install, identified by the id and key pair it generated itself. The public
/// key checks the device's signed requests; the private key never leaves the device.
/// </summary>
public sealed class Device
{
    /// <summary>The device's own id (a GUID generated on first start).</summary>
    public string Id { get; set; } = string.Empty;
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public string MachineName { get; set; } = string.Empty;
    public string WindowsUser { get; set; } = string.Empty;

    /// <summary>ECDSA P-256 public key, SubjectPublicKeyInfo, base64.</summary>
    public string PublicKey { get; set; } = string.Empty;
    public string? AppVersion { get; set; }
    public DateTime FirstSeenAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAtUtc { get; set; } = DateTime.UtcNow;
    public string? Note { get; set; }
    public List<License> Licenses { get; set; } = [];
}

public enum ActivationStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}

public enum ActivationSource
{
    /// <summary>The device sent its token to the API and waits for the answer.</summary>
    Online = 0,

    /// <summary>The owner pasted the token (sent by chat or mail) and hands the license back by hand.</summary>
    Manual = 1
}

/// <summary>A device asking for a license; the owner approves or rejects it.</summary>
public sealed class ActivationRequest
{
    /// <summary>Random id; the device polls with it.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string DeviceId { get; set; } = string.Empty;
    public Device? Device { get; set; }
    public ActivationSource Source { get; set; }
    public ActivationStatus Status { get; set; }
    public string? RequestIp { get; set; }
    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? DecidedAtUtc { get; set; }
    public string? LicenseId { get; set; }
    public string? DecisionNote { get; set; }
}

/// <summary>A signed license for one device. Revoking or replacing it is recorded, never deleted.</summary>
public sealed class License
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string DeviceId { get; set; } = string.Empty;
    public Device? Device { get; set; }
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public DateTime IssuedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Null: never expires (revocation still applies).</summary>
    public DateTime? ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? RevokeReason { get; set; }

    /// <summary>Set when a later license for the same device takes its place (renewal, re-issue).</summary>
    public string? ReplacedByLicenseId { get; set; }

    /// <summary>The license as handed to the device: payload and signature (see LicenseSigner).</summary>
    public string Text { get; set; } = string.Empty;

    public bool IsActive(DateTime nowUtc) =>
        RevokedAtUtc is null && ReplacedByLicenseId is null && (ExpiresAtUtc is null || ExpiresAtUtc > nowUtc);
}

/// <summary>A published Toolkit version that devices compare themselves with.</summary>
public sealed class Release
{
    public int Id { get; set; }
    public string Version { get; set; } = string.Empty;
    public string? Notes { get; set; }

    /// <summary>Devices below a mandatory release are asked to update before going on (M5).</summary>
    public bool Mandatory { get; set; }
    public string? DownloadUrl { get; set; }
    public DateTime PublishedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>What the administrator did, for the audit page.</summary>
public sealed class AuditEntry
{
    public int Id { get; set; }
    public DateTime AtUtc { get; set; } = DateTime.UtcNow;
    public string Admin { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
}
