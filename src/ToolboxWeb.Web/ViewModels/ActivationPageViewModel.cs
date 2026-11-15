using ToolboxWeb.Web.Services;

namespace ToolboxWeb.Web.ViewModels;

/// <summary>The activation screen of a desktop copy (/activate.html).</summary>
public sealed class ActivationPageViewModel
{
    public required DeviceLicenseStatus Status { get; init; }
    public required string DeviceId { get; init; }
    public required string MachineName { get; init; }

    /// <summary>The activation token for the manual way (copied and sent to the owner).</summary>
    public required string Token { get; init; }

    /// <summary>A license pasted by hand; kept in the box when it was refused.</summary>
    public string? LicenseText { get; set; }
    public string? LicenseError { get; set; }

    /// <summary>A request is waiting for approval on the license server.</summary>
    public bool Pending => Status.ActivationStatus == "pending" && Status.State != DeviceLicenseState.Active;
    public bool Rejected => Status.ActivationStatus == "rejected" && Status.State != DeviceLicenseState.Active;
}
