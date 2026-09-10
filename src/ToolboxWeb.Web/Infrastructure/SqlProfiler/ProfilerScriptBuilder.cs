using System.Globalization;
using System.Text;
using ToolboxWeb.Web.ViewModels.SqlProfiler;

namespace ToolboxWeb.Web.Infrastructure.SqlProfiler;

/// <summary>
/// Builds the <c>CREATE EVENT SESSION</c> statement for a capture.
/// <para>
/// Three details here came out of running this against a real server rather than from the
/// documentation: <c>duration</c> is in microseconds, <c>error_reported</c> without a
/// severity filter drowns the grid in "Changed database context to ..." notices, and the
/// tool's own connection shows up unless it is excluded by application name.
/// </para>
/// </summary>
public static class ProfilerScriptBuilder
{
    /// <summary>Prefix every session this tool creates carries, so orphans can be found and dropped.</summary>
    public const string SessionPrefix = "ToolboxWeb_Capture_";

    /// <summary>Set on the profiler's own connections so they can be filtered back out.</summary>
    public const string ApplicationName = "ToolboxWeb.Profiler";

    private const string CommonActions =
        "sqlserver.username, sqlserver.client_hostname, sqlserver.client_app_name, "
        + "sqlserver.session_id, sqlserver.database_name";

    public static string BuildSessionName() => SessionPrefix + Guid.NewGuid().ToString("N");

    public static string BuildCreate(string sessionName, ProfilerCaptureOptions options)
    {
        var events = new List<string>();

        if (options.CaptureProcedures)
        {
            events.Add($"""
                ADD EVENT sqlserver.rpc_completed (
                    ACTION ({CommonActions}, sqlserver.sql_text)
                    WHERE ({BuildPredicate(options, includeDuration: true)})
                )
                """);
        }

        if (options.CaptureModules)
        {
            // module_end also fires for procedures, which duplicates rpc_completed; the two
            // are merged after reading rather than filtered here, because object_type is a
            // mapped value and awkward to compare in a predicate.
            events.Add($"""
                ADD EVENT sqlserver.module_end (
                    ACTION ({CommonActions})
                    WHERE ({BuildPredicate(options, includeDuration: true)})
                )
                """);
        }

        if (options.CaptureErrors)
        {
            // severity <= 10 is informational: "Changed database context", "Changed language
            // setting", startup notices. Real errors start at 11.
            events.Add($"""
                ADD EVENT sqlserver.error_reported (
                    ACTION ({CommonActions}, sqlserver.sql_text)
                    WHERE (severity > 10 AND {BuildPredicate(options, includeDuration: false)})
                )
                """);
        }

        if (events.Count == 0)
        {
            throw new InvalidOperationException("At least one event type must be selected.");
        }

        var maxEvents = Math.Clamp(options.MaxEvents, 100, 50000);

        var script = new StringBuilder();
        script.Append(CultureInfo.InvariantCulture, $"CREATE EVENT SESSION [{Escape(sessionName)}] ON SERVER").AppendLine();
        script.AppendLine(string.Join("," + Environment.NewLine, events));
        script.Append(CultureInfo.InvariantCulture,
            $"ADD TARGET package0.ring_buffer (SET max_memory = 4096, max_events_limit = {maxEvents})").AppendLine();
        script.AppendLine("WITH (MAX_MEMORY = 4096 KB, EVENT_RETENTION_MODE = ALLOW_SINGLE_EVENT_LOSS,");
        script.AppendLine("      MAX_DISPATCH_LATENCY = 2 SECONDS, STARTUP_STATE = OFF);");
        return script.ToString();
    }

    public static string BuildStart(string sessionName) =>
        $"ALTER EVENT SESSION [{Escape(sessionName)}] ON SERVER STATE = START;";

    public static string BuildStop(string sessionName) =>
        $"ALTER EVENT SESSION [{Escape(sessionName)}] ON SERVER STATE = STOP;";

    public static string BuildDrop(string sessionName) => $"""
        IF EXISTS (SELECT 1 FROM sys.server_event_sessions WHERE name = N'{EscapeLiteral(sessionName)}')
        BEGIN
            IF EXISTS (SELECT 1 FROM sys.dm_xe_sessions WHERE name = N'{EscapeLiteral(sessionName)}')
                ALTER EVENT SESSION [{Escape(sessionName)}] ON SERVER STATE = STOP;
            DROP EVENT SESSION [{Escape(sessionName)}] ON SERVER;
        END
        """;

    /// <summary>Lists sessions this tool created, so forgotten ones can be dropped.</summary>
    public const string ListOwnSessions = $"""
        SELECT name FROM sys.server_event_sessions WHERE name LIKE '{SessionPrefix}%';
        """;

    /// <summary>Reads the ring buffer of one session.</summary>
    public static string BuildReadRingBuffer() => """
        SELECT CONVERT(nvarchar(max), t.target_data)
        FROM sys.dm_xe_session_targets t
        JOIN sys.dm_xe_sessions s ON s.address = t.event_session_address
        WHERE s.name = @sessionName AND t.target_name = 'ring_buffer';
        """;

    private static string BuildPredicate(ProfilerCaptureOptions options, bool includeDuration)
    {
        var parts = new List<string>
        {
            // Without this the profiler records its own polling queries.
            $"sqlserver.client_app_name <> N'{EscapeLiteral(ApplicationName)}'"
        };

        if (includeDuration && options.MinDurationMs > 0)
        {
            // The predicate is in microseconds, same as the reported value.
            var micro = (long)options.MinDurationMs * 1000;
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"duration >= {micro}"));
        }

        if (!string.IsNullOrWhiteSpace(options.Database))
        {
            parts.Add($"sqlserver.database_name = N'{EscapeLiteral(options.Database.Trim())}'");
        }

        if (!string.IsNullOrWhiteSpace(options.FilterUser))
        {
            parts.Add($"sqlserver.username = N'{EscapeLiteral(options.FilterUser.Trim())}'");
        }

        if (!string.IsNullOrWhiteSpace(options.FilterHost))
        {
            parts.Add($"sqlserver.client_hostname = N'{EscapeLiteral(options.FilterHost.Trim())}'");
        }

        if (!string.IsNullOrWhiteSpace(options.FilterApp))
        {
            parts.Add($"sqlserver.client_app_name = N'{EscapeLiteral(options.FilterApp.Trim())}'");
        }

        return string.Join(" AND ", parts);
    }

    private static string Escape(string identifier) =>
        identifier.Replace("]", "]]", StringComparison.Ordinal);

    private static string EscapeLiteral(string literal) =>
        literal.Replace("'", "''", StringComparison.Ordinal);
}
