using ToolboxWeb.Web.Infrastructure.Hosting;
using ToolboxWeb.Web.Services;

namespace ToolboxWeb.Web.ViewModels;

/// <summary>The desktop update page (/update.html).</summary>
public sealed class DesktopUpdatePageViewModel
{
    public required string CurrentVersion { get; init; }

    /// <summary>The newest release when it is newer than this copy; null when up to date.</summary>
    public DeviceReleaseInfo? Release { get; init; }

    /// <summary>A required release: the app cannot be used until it is installed.</summary>
    public bool Required { get; init; }

    /// <summary>Installed by Setup.exe, so it can update itself; otherwise only the download link applies.</summary>
    public bool Installed { get; init; }

    public required DesktopUpdateProgress Progress { get; init; }
}
