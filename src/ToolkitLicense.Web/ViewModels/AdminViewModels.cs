using ToolkitLicense.Web.Domain;
using ToolkitLicense.Web.Services;

namespace ToolkitLicense.Web.ViewModels;

public sealed class OverviewViewModel
{
    public int Pending { get; set; }
    public int Devices { get; set; }
    public int ActiveLicenses { get; set; }
    public int SeenThisWeek { get; set; }
    public Release? Latest { get; set; }
    public int DevicesBehind { get; set; }
    public List<ActivationRequest> PendingRequests { get; set; } = [];
    public List<AuditEntry> RecentAudit { get; set; } = [];
    public bool SigningKeyBackedUpHint { get; set; }
}

public sealed class ActivationsViewModel
{
    public List<ActivationRequest> Pending { get; set; } = [];
    public List<ActivationRequest> History { get; set; } = [];
    public string? Token { get; set; }
    public string? TokenError { get; set; }
}

public sealed class ActivationDetailsViewModel
{
    public ActivationRequest Request { get; set; } = default!;
    public License? License { get; set; }
    public List<Customer> Customers { get; set; } = [];
    public string KeyThumbprint { get; set; } = string.Empty;

    // Approve form
    public int? CustomerId { get; set; }
    public string? NewCustomerName { get; set; }
    public LicenseTerm Term { get; set; } = LicenseTerm.None;
    public DateTime? Date { get; set; }
    public string? Note { get; set; }
}

public sealed class DevicesViewModel
{
    public string? Search { get; set; }
    public List<Device> Devices { get; set; } = [];
    public string? LatestVersion { get; set; }
}

public sealed class DeviceDetailsViewModel
{
    public Device Device { get; set; } = default!;
    public License? Active { get; set; }
    public List<License> Licenses { get; set; } = [];
    public List<ActivationRequest> Requests { get; set; } = [];
    public List<Customer> Customers { get; set; } = [];
    public string KeyThumbprint { get; set; } = string.Empty;
    public string? LatestVersion { get; set; }
}

public sealed class CustomersViewModel
{
    public List<Customer> Customers { get; set; } = [];
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Note { get; set; }
}

public sealed class CustomerEditViewModel
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Note { get; set; }
    public List<Device> Devices { get; set; } = [];
}

public sealed class ReleasesViewModel
{
    public List<Release> Releases { get; set; } = [];

    /// <summary>The installer and update packages on the server (Infrastructure/UpdateFiles).</summary>
    public List<ToolkitLicense.Web.Infrastructure.UpdateFileInfo> Files { get; set; } = [];

    /// <summary>Newest version in releases.win.json; offered for the release form when it has no record yet.</summary>
    public string? FeedVersion { get; set; }
    public string? UploadError { get; set; }
    public List<VersionUse> InUse { get; set; } = [];
    public string? Version { get; set; }
    public string? Notes { get; set; }
    public bool Mandatory { get; set; }
    public string? DownloadUrl { get; set; }
}

public sealed class SigningKeyViewModel
{
    public string PublicKeyPem { get; set; } = string.Empty;
    public string PublicKeyBase64 { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
    public string KeyFile { get; set; } = string.Empty;
    public string DataRoot { get; set; } = string.Empty;
}

public sealed class AuditViewModel
{
    public List<AuditEntry> Entries { get; set; } = [];
    public int Page { get; set; }
    public bool HasMore { get; set; }
}

public sealed class DownloadViewModel
{
    public Release? Latest { get; set; }
    public ToolkitLicense.Web.Infrastructure.UpdateFileInfo? Setup { get; set; }
}
