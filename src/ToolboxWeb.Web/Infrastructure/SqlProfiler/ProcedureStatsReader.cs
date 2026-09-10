using System.Globalization;
using Microsoft.Data.SqlClient;
using ToolboxWeb.Web.ViewModels.SqlProfiler;

namespace ToolboxWeb.Web.Infrastructure.SqlProfiler;

public interface IProcedureStatsReader
{
    /// <summary>Procedures that ran in the last N minutes, from the plan cache.</summary>
    Task<IReadOnlyList<ProcedureStatViewModel>> ReadRecentAsync(
        string connectionString,
        string database,
        int minutes,
        CancellationToken cancellationToken = default);

    Task<string> ReadDefinitionAsync(
        string connectionString,
        string database,
        string objectName,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The quick-look mode: no event session, no elevated rights beyond VIEW SERVER STATE.
/// <para>
/// It answers "which procedures just ran and which are slow", but the plan cache aggregates
/// across all callers, so it can never say <em>who</em> ran them. That is what the capture
/// mode is for.
/// </para>
/// </summary>
public sealed class ProcedureStatsReader : IProcedureStatsReader
{
    public async Task<IReadOnlyList<ProcedureStatViewModel>> ReadRecentAsync(
        string connectionString,
        string database,
        int minutes,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT  QUOTENAME(s.name) + N'.' + QUOTENAME(o.name)     AS ObjectName,
                    ps.execution_count                                AS ExecutionCount,
                    ps.last_execution_time                            AS LastExecutionTime,
                    ps.total_elapsed_time                             AS TotalElapsedUs,
                    ps.last_elapsed_time                              AS LastElapsedUs,
                    ps.total_worker_time                              AS TotalWorkerUs,
                    ps.total_logical_reads                            AS TotalLogicalReads
            FROM sys.dm_exec_procedure_stats ps
            JOIN sys.objects o ON o.object_id = ps.object_id
            JOIN sys.schemas s ON s.schema_id = o.schema_id
            WHERE ps.database_id = DB_ID(@database)
              AND ps.last_execution_time >= DATEADD(minute, -@minutes, GETDATE())
            ORDER BY ps.last_elapsed_time DESC;
            """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 60 };
        command.Parameters.AddWithValue("@database", database);
        command.Parameters.AddWithValue("@minutes", Math.Clamp(minutes, 1, 1440));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<ProcedureStatViewModel>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var executions = reader.GetInt64(1);
            var totalUs = reader.GetInt64(3);
            var lastExecution = reader.GetDateTime(2);

            rows.Add(new ProcedureStatViewModel
            {
                ObjectName = reader.GetString(0),
                ExecutionCount = executions,
                LastExecutionTime = lastExecution,
                LastExecutionText = lastExecution.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                TotalMs = Math.Round(totalUs / 1000d, 1),
                AverageMs = executions == 0 ? 0 : Math.Round(totalUs / 1000d / executions, 1),
                LastMs = Math.Round(reader.GetInt64(4) / 1000d, 1),
                TotalCpuMs = Math.Round(reader.GetInt64(5) / 1000d, 1),
                TotalLogicalReads = reader.GetInt64(6)
            });
        }

        return rows;
    }

    public async Task<string> ReadDefinitionAsync(
        string connectionString,
        string database,
        string objectName,
        CancellationToken cancellationToken = default)
    {
        // The connection is already opened against the right database by the caller, so the
        // name resolves without any string building.
        const string sql = "SELECT OBJECT_DEFINITION(OBJECT_ID(@objectName));";

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(database))
        {
            await connection.ChangeDatabaseAsync(database, cancellationToken);
        }

        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 30 };
        command.Parameters.AddWithValue("@objectName", objectName);

        var definition = await command.ExecuteScalarAsync(cancellationToken);
        return definition as string ?? string.Empty;
    }
}
