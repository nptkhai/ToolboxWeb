using ToolboxWeb.Web.Infrastructure.SqlProfiler;

namespace ToolboxWeb.Web.Services;

/// <summary>
/// Stops recordings that ran past their deadline.
/// <para>
/// An Extended Events session lives on the SQL Server, not in this app, so closing a browser
/// tab does not end it. The page sends a best-effort stop on unload and a new recording
/// sweeps leftovers, but neither fires if the user simply walks away and nobody records
/// again. This timer is what guarantees a forgotten session cannot run forever on a
/// customer's server.
/// </para>
/// </summary>
public sealed class SqlProfilerJanitor : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private readonly ISqlProfilerSessionStore _store;
    private readonly IXEventSessionManager _sessions;
    private readonly ILogger<SqlProfilerJanitor> _logger;

    public SqlProfilerJanitor(
        ISqlProfilerSessionStore store,
        IXEventSessionManager sessions,
        ILogger<SqlProfilerJanitor> logger)
    {
        _store = store;
        _sessions = sessions;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            foreach (var entry in _store.ExpiredEntries())
            {
                try
                {
                    await _sessions.DropAsync(entry.ConnectionString, entry.SessionName, stoppingToken);
                    _logger.LogInformation(
                        "Auto-stopped profiler session {SessionName} after its deadline", entry.SessionName);
                }
                catch (Exception ex)
                {
                    // The server may be unreachable now; try again on the next tick rather
                    // than dropping the entry and losing track of the session name.
                    _logger.LogWarning(ex, "Could not auto-stop profiler session {SessionName}", entry.SessionName);
                    continue;
                }

                entry.Running = false;
                _store.Remove(entry);
            }
        }
    }
}
