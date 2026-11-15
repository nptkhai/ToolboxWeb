using ToolboxWeb.Web.Enums;
using ToolboxWeb.Web.Infrastructure.Hosting;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace ToolboxWeb.Web.Services;

/// <summary>
/// Desktop only: talks to the license server in the background. Shortly after start and then every
/// License:CheckHours it sends the heartbeat (version, renewals, revocation, newest release); while
/// an activation request waits for approval it asks about it every two minutes. Without a network
/// it stays quiet and tries again next time: the license itself is checked without the server.
/// </summary>
public sealed class LicenseHeartbeatService(
    IDeviceLicenseService licenses,
    DeviceFileStore device,
    IServiceScopeFactory scopes,
    IOptions<LicenseOptions> options,
    ILogger<LicenseHeartbeatService> logger) : BackgroundService
{
    private static readonly TimeSpan FirstCheck = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan PendingPoll = TimeSpan.FromMinutes(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var every = TimeSpan.FromHours(Math.Max(0.25, options.Value.CheckHours));
        var nextCheck = DateTime.UtcNow + FirstCheck;
        while (!stoppingToken.IsCancellationRequested)
        {
            var status = licenses.GetStatus();
            if (status.State is DeviceLicenseState.Off or DeviceLicenseState.NotConfigured || !status.ServerConfigured)
            {
                return;
            }

            try
            {
                if (!string.IsNullOrEmpty(device.Read().License?.ActivationRequestId) && status.State != DeviceLicenseState.Active)
                {
                    var (_, change) = await licenses.PollAsync(stoppingToken);
                    await LogAsync(change);
                    await Task.Delay(PendingPoll, stoppingToken);
                    continue;
                }

                if (DateTime.UtcNow >= nextCheck)
                {
                    var (_, change) = await licenses.CheckNowAsync(stoppingToken);
                    await LogAsync(change);
                    nextCheck = DateTime.UtcNow + every;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "License check failed.");
                nextCheck = DateTime.UtcNow + every;
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    /// <summary>What the background check changed goes to the owner's activity log.</summary>
    private async Task LogAsync(LicenseChange change)
    {
        var ownerId = device.Read().OwnerId;
        if (change == LicenseChange.None || ownerId is null)
        {
            return;
        }

        // Written in the app's default language: a background check has no request culture.
        var culture = System.Globalization.CultureInfo.CurrentUICulture;
        System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo("vi-VN");
        using var scope = scopes.CreateScope();
        var log = scope.ServiceProvider.GetRequiredService<IActivityLogService>();
        var text = scope.ServiceProvider.GetRequiredService<IStringLocalizer<SharedResource>>();
        await log.LogAsync(ownerId, ActivityActionType.Update, ActivityEntityType.License, licenses.GetStatus().LicenseId, text[$"License.Log.{change}"].Value);
        System.Globalization.CultureInfo.CurrentUICulture = culture;
    }
}
