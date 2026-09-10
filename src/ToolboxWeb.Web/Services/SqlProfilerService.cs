using Microsoft.Data.SqlClient;
using ToolboxWeb.Web.Enums;
using ToolboxWeb.Web.Infrastructure.SqlProfiler;
using ToolboxWeb.Web.Infrastructure.SqlSchema;
using ToolboxWeb.Web.ViewModels.SchemaCompare;
using ToolboxWeb.Web.ViewModels.SqlProfiler;

namespace ToolboxWeb.Web.Services;

public interface ISqlProfilerService
{
    Task<ProfilerPermissionResult> CheckAsync(SqlConnectionInputModel connection, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> ListDatabasesAsync(SqlConnectionInputModel connection, CancellationToken cancellationToken = default);

    Task<string> StartCaptureAsync(StartCaptureRequest request, string browserSessionId, CancellationToken cancellationToken = default);

    Task<CaptureStateViewModel?> ReadAsync(string captureId, string browserSessionId, bool maskParameters, CancellationToken cancellationToken = default);

    Task<CaptureStateViewModel?> StopAsync(string captureId, string browserSessionId, bool maskParameters, CancellationToken cancellationToken = default);

    Task<RecentProceduresViewModel> ReadRecentAsync(SqlConnectionInputModel connection, int minutes, CancellationToken cancellationToken = default);

    Task<ProfilerDetailViewModel> ReadDefinitionAsync(SqlConnectionInputModel connection, string objectName, CancellationToken cancellationToken = default);
}

public sealed class SqlProfilerService : ISqlProfilerService
{
    private readonly IXEventSessionManager _sessions;
    private readonly IProcedureStatsReader _stats;
    private readonly ISqlProfilerSessionStore _store;
    private readonly ISqlConnectionStringFactory _connectionStrings;
    private readonly ICurrentUserService _currentUser;
    private readonly IActivityLogService _activityLog;
    private readonly ILogger<SqlProfilerService> _logger;

    public SqlProfilerService(
        IXEventSessionManager sessions,
        IProcedureStatsReader stats,
        ISqlProfilerSessionStore store,
        ISqlConnectionStringFactory connectionStrings,
        ICurrentUserService currentUser,
        IActivityLogService activityLog,
        ILogger<SqlProfilerService> logger)
    {
        _sessions = sessions;
        _stats = stats;
        _store = store;
        _connectionStrings = connectionStrings;
        _currentUser = currentUser;
        _activityLog = activityLog;
        _logger = logger;
    }

    public async Task<ProfilerPermissionResult> CheckAsync(
        SqlConnectionInputModel connection,
        CancellationToken cancellationToken = default)
    {
        return await _sessions.CheckPermissionsAsync(BuildConnectionString(connection, requireDatabase: false), cancellationToken);
    }

    public async Task<IReadOnlyList<string>> ListDatabasesAsync(
        SqlConnectionInputModel connection,
        CancellationToken cancellationToken = default)
    {
        return await _sessions.ListDatabasesAsync(BuildConnectionString(connection, requireDatabase: false), cancellationToken);
    }

    public async Task<string> StartCaptureAsync(
        StartCaptureRequest request,
        string browserSessionId,
        CancellationToken cancellationToken = default)
    {
        var connectionString = BuildConnectionString(request.Connection, requireDatabase: false);

        var permissions = await _sessions.CheckPermissionsAsync(connectionString, cancellationToken);
        if (permissions.IsAzureSqlDatabase)
        {
            throw new SqlSchemaValidationException("Azure SQL Database is not supported: it uses database-scoped event sessions.");
        }

        if (!permissions.CanCapture)
        {
            throw new SqlSchemaValidationException(
                "This account needs ALTER ANY EVENT SESSION and VIEW SERVER STATE on the server to capture events.");
        }

        // Close whatever this user had running, then sweep anything left behind by a crash
        // or a closed browser tab. Event sessions outlive the web app otherwise.
        await StopActiveAsync(browserSessionId, cancellationToken);
        await SweepAsync(connectionString, cancellationToken);

        var options = request.Options;
        var sessionName = ProfilerScriptBuilder.BuildSessionName();
        await _sessions.CreateAndStartAsync(connectionString, sessionName, options, cancellationToken);

        var entry = new ProfilerCaptureEntry
        {
            CaptureId = Guid.NewGuid().ToString("N"),
            SessionId = browserSessionId,
            SessionName = sessionName,
            ConnectionString = connectionString,
            Database = options.Database,
            ServerLabel = request.Connection.Server.Trim(),
            AutoStopAt = DateTime.Now.AddMinutes(Math.Clamp(options.AutoStopMinutes, 1, 120))
        };

        _store.Start(browserSessionId, entry);
        await LogAsync($"Started SQL capture on {entry.ServerLabel}");
        return entry.CaptureId;
    }

    public async Task<CaptureStateViewModel?> ReadAsync(
        string captureId,
        string browserSessionId,
        bool maskParameters,
        CancellationToken cancellationToken = default)
    {
        var entry = _store.Get(captureId, browserSessionId);
        if (entry is null)
        {
            return null;
        }

        if (entry.Running && entry.IsExpired)
        {
            await StopEntryAsync(entry, cancellationToken);
        }

        if (entry.Running)
        {
            await PullEventsAsync(entry, cancellationToken);
        }

        return await BuildStateAsync(entry, maskParameters);
    }

    public async Task<CaptureStateViewModel?> StopAsync(
        string captureId,
        string browserSessionId,
        bool maskParameters,
        CancellationToken cancellationToken = default)
    {
        var entry = _store.Get(captureId, browserSessionId);
        if (entry is null)
        {
            return null;
        }

        if (entry.Running)
        {
            await PullEventsAsync(entry, cancellationToken);
            await StopEntryAsync(entry, cancellationToken);
        }

        return await BuildStateAsync(entry, maskParameters);
    }

    public async Task<RecentProceduresViewModel> ReadRecentAsync(
        SqlConnectionInputModel connection,
        int minutes,
        CancellationToken cancellationToken = default)
    {
        var connectionString = BuildConnectionString(connection, requireDatabase: true);
        var database = connection.Database.Trim();
        var window = minutes <= 0 ? 5 : minutes;

        var rows = await _stats.ReadRecentAsync(connectionString, database, window, cancellationToken);

        return new RecentProceduresViewModel
        {
            Minutes = window,
            Database = database,
            Rows = rows
        };
    }

    public async Task<ProfilerDetailViewModel> ReadDefinitionAsync(
        SqlConnectionInputModel connection,
        string objectName,
        CancellationToken cancellationToken = default)
    {
        var connectionString = BuildConnectionString(connection, requireDatabase: true);

        try
        {
            var definition = await _stats.ReadDefinitionAsync(
                connectionString, connection.Database.Trim(), objectName, cancellationToken);

            return new ProfilerDetailViewModel
            {
                ObjectName = objectName,
                Definition = definition,
                DefinitionError = string.IsNullOrEmpty(definition)
                    ? "The definition could not be read; the object may not exist in this database or may be encrypted."
                    : null
            };
        }
        catch (SqlException ex)
        {
            return new ProfilerDetailViewModel
            {
                ObjectName = objectName,
                DefinitionError = SqlSchemaErrorHelper.Scrub(ex.Message)
            };
        }
    }

    // ------------------------------------------------------------------ internals

    private async Task PullEventsAsync(ProfilerCaptureEntry entry, CancellationToken cancellationToken)
    {
        try
        {
            var xml = await _sessions.ReadRingBufferAsync(entry.ConnectionString, entry.SessionName, cancellationToken);
            var snapshot = XEventRingBufferReader.Parse(xml);

            await entry.LockedAsync(events =>
            {
                foreach (var raw in snapshot.Events)
                {
                    // The ring buffer replays everything it still holds on every read, so the
                    // key check is what stops the list growing duplicates.
                    events.TryAdd(raw.Key, raw);
                }

                return true;
            });

            if (snapshot.DroppedCount > entry.DroppedCount)
            {
                entry.DroppedCount = snapshot.DroppedCount;
            }
        }
        catch (SqlException ex)
        {
            entry.Error = SqlSchemaErrorHelper.Scrub(ex.Message);
            _logger.LogWarning(ex, "Could not read profiler ring buffer for {SessionName}", entry.SessionName);
        }
    }

    private async Task<CaptureStateViewModel> BuildStateAsync(ProfilerCaptureEntry entry, bool maskParameters)
    {
        var raw = await entry.LockedAsync(events => events.Values.ToArray());
        var rows = ProfilerEventMerger.Merge(raw, maskParameters);

        return new CaptureStateViewModel
        {
            CaptureId = entry.CaptureId,
            Running = entry.Running,
            Error = entry.Error,
            ElapsedMs = (long)(DateTime.Now - entry.StartedAt).TotalMilliseconds,
            EventsDropped = entry.DroppedCount,
            AutoStopAt = entry.Running ? entry.AutoStopAt : null,
            Summary = ProfilerEventMerger.Summarise(rows),
            Events = rows
        };
    }

    private async Task StopEntryAsync(ProfilerCaptureEntry entry, CancellationToken cancellationToken)
    {
        entry.Running = false;

        try
        {
            await _sessions.DropAsync(entry.ConnectionString, entry.SessionName, cancellationToken);
        }
        catch (SqlException ex)
        {
            entry.Error = SqlSchemaErrorHelper.Scrub(ex.Message);
            _logger.LogWarning(ex, "Could not drop profiler session {SessionName}", entry.SessionName);
        }
    }

    private async Task StopActiveAsync(string browserSessionId, CancellationToken cancellationToken)
    {
        var active = _store.GetActive(browserSessionId);
        if (active is null)
        {
            return;
        }

        await StopEntryAsync(active, cancellationToken);
        _store.Remove(active);
    }

    /// <summary>
    /// Drops any session this tool left on the server that no live capture still owns.
    /// Without this a closed browser tab would leave an event session running for good.
    /// </summary>
    private async Task SweepAsync(string connectionString, CancellationToken cancellationToken)
    {
        foreach (var expired in _store.ExpiredEntries())
        {
            await StopEntryAsync(expired, cancellationToken);
            _store.Remove(expired);
        }

        try
        {
            var dropped = await _sessions.DropOrphansAsync(connectionString, _store.ActiveSessionNames(), cancellationToken);
            if (dropped > 0)
            {
                _logger.LogInformation("Swept {Count} orphaned profiler session(s)", dropped);
            }
        }
        catch (SqlException ex)
        {
            _logger.LogWarning(ex, "Could not sweep orphaned profiler sessions");
        }
    }

    private string BuildConnectionString(SqlConnectionInputModel connection, bool requireDatabase)
    {
        var raw = _connectionStrings.Create(connection, requireDatabase);

        // Tagging the connection lets the event session filter its own traffic back out.
        var builder = new SqlConnectionStringBuilder(raw)
        {
            ApplicationName = ProfilerScriptBuilder.ApplicationName
        };

        return builder.ConnectionString;
    }

    private async Task LogAsync(string summary)
    {
        var userId = _currentUser.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        try
        {
            await _activityLog.LogAsync(userId, ActivityActionType.Use, ActivityEntityType.SqlProfiler, null, summary);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not write profiler activity log");
        }
    }
}
