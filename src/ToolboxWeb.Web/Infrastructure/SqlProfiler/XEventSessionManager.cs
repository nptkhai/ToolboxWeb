using Microsoft.Data.SqlClient;
using ToolboxWeb.Web.ViewModels.SqlProfiler;

namespace ToolboxWeb.Web.Infrastructure.SqlProfiler;

public interface IXEventSessionManager
{
    /// <summary>Checks the two server permissions the capture needs before anything is created.</summary>
    Task<ProfilerPermissionResult> CheckPermissionsAsync(string connectionString, CancellationToken cancellationToken = default);

    Task CreateAndStartAsync(string connectionString, string sessionName, ProfilerCaptureOptions options, CancellationToken cancellationToken = default);

    Task<string?> ReadRingBufferAsync(string connectionString, string sessionName, CancellationToken cancellationToken = default);

    Task DropAsync(string connectionString, string sessionName, CancellationToken cancellationToken = default);

    /// <summary>Drops every session this tool created except the ones still in use.</summary>
    Task<int> DropOrphansAsync(string connectionString, IReadOnlyCollection<string> keepSessionNames, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> ListDatabasesAsync(string connectionString, CancellationToken cancellationToken = default);
}

public sealed record ProfilerPermissionResult(
    bool CanAlterEventSession,
    bool CanViewServerState,
    bool IsAzureSqlDatabase,
    string ServerVersion)
{
    public bool CanCapture => CanAlterEventSession && CanViewServerState && !IsAzureSqlDatabase;
    public bool CanUseRecentProcedures => CanViewServerState;
}

public sealed class XEventSessionManager : IXEventSessionManager
{
    private readonly ILogger<XEventSessionManager> _logger;

    public XEventSessionManager(ILogger<XEventSessionManager> logger)
    {
        _logger = logger;
    }

    public async Task<ProfilerPermissionResult> CheckPermissionsAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                CONVERT(int, CASE WHEN EXISTS (
                    SELECT 1 FROM sys.fn_my_permissions(NULL, 'SERVER')
                    WHERE permission_name IN ('ALTER ANY EVENT SESSION', 'CONTROL SERVER')) THEN 1 ELSE 0 END) AS CanAlter,
                CONVERT(int, CASE WHEN EXISTS (
                    SELECT 1 FROM sys.fn_my_permissions(NULL, 'SERVER')
                    WHERE permission_name IN ('VIEW SERVER STATE', 'CONTROL SERVER')) THEN 1 ELSE 0 END) AS CanView,
                CONVERT(int, SERVERPROPERTY('EngineEdition')) AS EngineEdition,
                CONVERT(nvarchar(100), SERVERPROPERTY('ProductVersion')) AS ProductVersion;
            """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return new ProfilerPermissionResult(false, false, false, string.Empty);
        }

        // EngineEdition 5 is Azure SQL Database, where event sessions are database-scoped
        // and use a different syntax; this tool only targets a normal SQL Server.
        var engineEdition = reader.GetInt32(2);

        return new ProfilerPermissionResult(
            reader.GetInt32(0) == 1,
            reader.GetInt32(1) == 1,
            engineEdition == 5,
            reader.GetString(3));
    }

    public async Task CreateAndStartAsync(
        string connectionString,
        string sessionName,
        ProfilerCaptureOptions options,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await ExecuteAsync(connection, ProfilerScriptBuilder.BuildDrop(sessionName), cancellationToken);
        await ExecuteAsync(connection, ProfilerScriptBuilder.BuildCreate(sessionName, options), cancellationToken);
        await ExecuteAsync(connection, ProfilerScriptBuilder.BuildStart(sessionName), cancellationToken);

        _logger.LogInformation("Started profiler session {SessionName}", sessionName);
    }

    public async Task<string?> ReadRingBufferAsync(
        string connectionString,
        string sessionName,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(ProfilerScriptBuilder.BuildReadRingBuffer(), connection);
        command.Parameters.AddWithValue("@sessionName", sessionName);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result as string;
    }

    public async Task DropAsync(
        string connectionString,
        string sessionName,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await ExecuteAsync(connection, ProfilerScriptBuilder.BuildDrop(sessionName), cancellationToken);

        _logger.LogInformation("Dropped profiler session {SessionName}", sessionName);
    }

    public async Task<int> DropOrphansAsync(
        string connectionString,
        IReadOnlyCollection<string> keepSessionNames,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var names = new List<string>();
        await using (var command = new SqlCommand(ProfilerScriptBuilder.ListOwnSessions, connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                names.Add(reader.GetString(0));
            }
        }

        var dropped = 0;
        foreach (var name in names.Where(x => !keepSessionNames.Contains(x)))
        {
            await ExecuteAsync(connection, ProfilerScriptBuilder.BuildDrop(name), cancellationToken);
            dropped++;
            _logger.LogInformation("Dropped orphaned profiler session {SessionName}", name);
        }

        return dropped;
    }

    public async Task<IReadOnlyList<string>> ListDatabasesAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT name FROM sys.databases
            WHERE state = 0 AND HAS_DBACCESS(name) = 1 AND name <> N'tempdb'
            ORDER BY name;
            """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var databases = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
        {
            databases.Add(reader.GetString(0));
        }

        return databases;
    }

    private static async Task ExecuteAsync(SqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 60 };
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
