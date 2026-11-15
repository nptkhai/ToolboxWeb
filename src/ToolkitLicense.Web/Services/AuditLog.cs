using ToolkitLicense.Web.Data;
using ToolkitLicense.Web.Domain;

namespace ToolkitLicense.Web.Services;

/// <summary>
/// Records what the administrator did. Entries are added to the same unit of work as the change,
/// so they are saved together with it (or not at all).
/// </summary>
public interface IAuditLog
{
    void Add(string action, string summary);
}

public sealed class AuditLog(LicenseDbContext db, IHttpContextAccessor http) : IAuditLog
{
    public void Add(string action, string summary)
    {
        var admin = http.HttpContext?.User.Identity?.Name;
        db.AuditEntries.Add(new AuditEntry
        {
            Admin = string.IsNullOrWhiteSpace(admin) ? "system" : admin,
            Action = action,
            Summary = summary.Length > 1000 ? summary[..1000] : summary
        });
    }
}

/// <summary>Audit action names; the audit page shows them through the "Audit.Action.*" keys.</summary>
public static class AuditActions
{
    public const string SetupAdmin = "SetupAdmin";
    public const string TwoFactorEnabled = "TwoFactorEnabled";
    public const string PasswordChanged = "PasswordChanged";
    public const string RecoveryCodesRenewed = "RecoveryCodesRenewed";
    public const string ManualRequest = "ManualRequest";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Reissued = "Reissued";
    public const string Revoked = "Revoked";
    public const string DeviceAssigned = "DeviceAssigned";
    public const string DeviceNote = "DeviceNote";
    public const string CustomerCreated = "CustomerCreated";
    public const string CustomerUpdated = "CustomerUpdated";
    public const string CustomerDeleted = "CustomerDeleted";
    public const string ReleaseCreated = "ReleaseCreated";
    public const string ReleaseDeleted = "ReleaseDeleted";

    public static readonly string[] All =
    [
        SetupAdmin, TwoFactorEnabled, PasswordChanged, RecoveryCodesRenewed, ManualRequest, Approved, Rejected,
        Reissued, Revoked, DeviceAssigned, DeviceNote, CustomerCreated, CustomerUpdated, CustomerDeleted,
        ReleaseCreated, ReleaseDeleted
    ];
}
