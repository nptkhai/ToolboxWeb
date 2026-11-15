using Microsoft.Extensions.Options;
using ToolboxWeb.Web.Infrastructure.Hosting;
using Velopack;
using Velopack.Sources;

namespace ToolboxWeb.Web.Services;

public enum DesktopUpdateStage
{
    Idle,
    Checking,
    Downloading,
    Ready,
    Applying,

    /// <summary>The server has no newer package than this copy.</summary>
    NoUpdate,
    Failed
}

/// <summary>What the update page shows while an update runs (GET /desktop/update/status).</summary>
public sealed record DesktopUpdateProgress(DesktopUpdateStage Stage, int Percent, string? Version, string? ErrorKey);

/// <summary>
/// Updates an installed desktop copy (Velopack, see docs/RELEASE.md) from the packages on the
/// license server ({License:ServerUrl}/updates). Download first, then apply: the app quits, the
/// updater swaps the files and starts the new version, whose start-up backs up the database before
/// any migration (DatabaseBackup). A copy that was not installed by Setup.exe cannot update itself;
/// it gets the download link instead.
/// </summary>
public interface IDesktopUpdateService
{
    /// <summary>Installed by the Toolkit installer, so it can update itself.</summary>
    bool IsInstalled { get; }

    DesktopUpdateProgress Progress { get; }

    /// <summary>Checks the server and downloads the newest package in the background.</summary>
    void Start();

    /// <summary>Quits, applies the downloaded package and starts the new version (no new window: the open page reloads).</summary>
    void ApplyAndRestart();
}

public sealed class DesktopUpdateService : IDesktopUpdateService
{
    private readonly ILogger<DesktopUpdateService> _logger;
    private readonly UpdateManager? _manager;
    private readonly object _gate = new();
    private DesktopUpdateProgress _progress = new(DesktopUpdateStage.Idle, 0, null, null);
    private UpdateInfo? _ready;

    public DesktopUpdateService(IOptions<LicenseOptions> options, AppModeInfo mode, ILogger<DesktopUpdateService> logger)
    {
        _logger = logger;
        var server = options.Value.ServerUrl?.Trim().TrimEnd('/');
        if (!mode.IsDesktop || string.IsNullOrEmpty(server))
        {
            return;
        }

        try
        {
            var manager = new UpdateManager(new SimpleWebSource(server + "/updates"));
            _manager = manager.IsInstalled ? manager : null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "The updater could not be prepared.");
        }
    }

    public bool IsInstalled => _manager is not null;

    public DesktopUpdateProgress Progress
    {
        get
        {
            lock (_gate)
            {
                return _progress;
            }
        }
    }

    public void Start()
    {
        lock (_gate)
        {
            if (_manager is null || _progress.Stage is DesktopUpdateStage.Checking or DesktopUpdateStage.Downloading or DesktopUpdateStage.Applying)
            {
                return;
            }

            if (_progress.Stage == DesktopUpdateStage.Ready)
            {
                return;
            }

            _progress = new DesktopUpdateProgress(DesktopUpdateStage.Checking, 0, null, null);
        }

        _ = Task.Run(RunAsync);
    }

    public void ApplyAndRestart()
    {
        UpdateInfo? ready;
        lock (_gate)
        {
            ready = _ready;
            if (_manager is null || ready is null)
            {
                return;
            }

            _progress = _progress with { Stage = DesktopUpdateStage.Applying };
        }

        _logger.LogWarning("Applying Toolkit {Version} and restarting.", ready.TargetFullRelease.Version);
        _manager.ApplyUpdatesAndRestart(ready.TargetFullRelease, [DesktopHost.NoWindowSwitch]);
    }

    private async Task RunAsync()
    {
        try
        {
            var update = await _manager!.CheckForUpdatesAsync();
            if (update is null)
            {
                Set(new DesktopUpdateProgress(DesktopUpdateStage.NoUpdate, 0, null, "Update.Error.NoPackage"));
                return;
            }

            var version = update.TargetFullRelease.Version.ToString();
            Set(new DesktopUpdateProgress(DesktopUpdateStage.Downloading, 0, version, null));
            await _manager.DownloadUpdatesAsync(update, percent => Set(new DesktopUpdateProgress(DesktopUpdateStage.Downloading, percent, version, null)));
            lock (_gate)
            {
                _ready = update;
                _progress = new DesktopUpdateProgress(DesktopUpdateStage.Ready, 100, version, null);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The update could not be downloaded.");
            Set(new DesktopUpdateProgress(DesktopUpdateStage.Failed, 0, null, ex is HttpRequestException or TaskCanceledException ? "Update.Error.Unreachable" : "Update.Error.Failed"));
        }
    }

    private void Set(DesktopUpdateProgress progress)
    {
        lock (_gate)
        {
            _progress = progress;
        }
    }
}
