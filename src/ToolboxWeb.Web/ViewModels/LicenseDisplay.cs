using ToolboxWeb.Web.Services;

namespace ToolboxWeb.Web.ViewModels;

/// <summary>How a desktop license state is shown: the tbx-pill tone and the label key.</summary>
public static class LicenseDisplay
{
    public static (string Tone, string Key) Pill(DeviceLicenseStatus status) => status.State switch
    {
        DeviceLicenseState.Active when status.ExpiresSoon => ("is-warning", "License.State.ExpiresSoon"),
        DeviceLicenseState.Active => ("is-success", "License.State.Active"),
        DeviceLicenseState.Expired => ("is-warning", "License.State.Expired"),
        DeviceLicenseState.Revoked => ("is-danger", "License.State.Revoked"),
        DeviceLicenseState.NotConfigured => ("is-danger", "License.State.NotConfigured"),
        DeviceLicenseState.Off => ("is-neutral", "License.State.Off"),
        _ when status.ActivationStatus == "pending" => ("is-accent", "License.State.Pending"),
        _ when status.ActivationStatus == "rejected" => ("is-danger", "License.State.Rejected"),
        _ => ("is-neutral", "License.State.NotActivated")
    };
}
