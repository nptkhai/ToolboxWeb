using ToolkitLicense.Web.Domain;

namespace ToolkitLicense.Web.Infrastructure;

/// <summary>Status pills: the tbx-pill tone and the localization key of the label.</summary>
public static class Pills
{
    public static (string Css, string Key) For(ActivationStatus status) => status switch
    {
        ActivationStatus.Approved => ("is-success", "Status.Approved"),
        ActivationStatus.Rejected => ("is-danger", "Status.Rejected"),
        _ => ("is-accent", "Status.Pending")
    };

    public static (string Css, string Key) For(License? license)
    {
        var now = DateTime.UtcNow;
        return license switch
        {
            null => ("is-neutral", "License.State.None"),
            { RevokedAtUtc: not null } => ("is-danger", "License.State.Revoked"),
            { ReplacedByLicenseId: not null } => ("is-neutral", "License.State.Replaced"),
            { ExpiresAtUtc: { } end } when end <= now => ("is-warning", "License.State.Expired"),
            { ExpiresAtUtc: { } end } when end <= now.AddDays(30) => ("is-warning", "License.State.ExpiresSoon"),
            _ => ("is-success", "License.State.Active")
        };
    }

    /// <summary>The license a device is judged by: its active one, else the latest it had.</summary>
    public static License? Current(Device device)
    {
        var now = DateTime.UtcNow;
        return device.Licenses.Where(x => x.IsActive(now)).OrderByDescending(x => x.IssuedAtUtc).FirstOrDefault()
            ?? device.Licenses.Where(x => x.ReplacedByLicenseId is null).OrderByDescending(x => x.IssuedAtUtc).FirstOrDefault();
    }
}
