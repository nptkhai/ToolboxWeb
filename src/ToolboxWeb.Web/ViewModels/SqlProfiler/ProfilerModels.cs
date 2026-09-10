using ToolboxWeb.Web.ViewModels.SchemaCompare;

namespace ToolboxWeb.Web.ViewModels.SqlProfiler;

/// <summary>What the user wants captured. Everything here becomes an Extended Events predicate.</summary>
public sealed class ProfilerCaptureOptions
{
    /// <summary>Empty means every database on the server.</summary>
    public string Database { get; set; } = string.Empty;

    /// <summary>Skip anything faster than this. Raise it when the grid floods.</summary>
    public int MinDurationMs { get; set; }

    public bool CaptureProcedures { get; set; } = true;

    /// <summary>Triggers and functions, via module_end.</summary>
    public bool CaptureModules { get; set; } = true;

    public bool CaptureErrors { get; set; } = true;

    public string FilterUser { get; set; } = string.Empty;
    public string FilterHost { get; set; } = string.Empty;
    public string FilterApp { get; set; } = string.Empty;

    /// <summary>Ring buffer cap. Older events fall out once this is reached.</summary>
    public int MaxEvents { get; set; } = 5000;

    /// <summary>The session stops itself after this long even if the user walks away.</summary>
    public int AutoStopMinutes { get; set; } = 15;
}

public sealed class StartCaptureRequest
{
    public SqlConnectionInputModel Connection { get; set; } = new();
    public ProfilerCaptureOptions Options { get; set; } = new();
}

/// <summary>One row of the capture grid, after rpc_completed and module_end have been merged.</summary>
public sealed class ProfilerEventViewModel
{
    public string Id { get; init; } = string.Empty;
    public DateTime At { get; init; }
    public string Time { get; init; } = string.Empty;

    /// <summary>Procedure, Trigger, Function or Error.</summary>
    public string Kind { get; init; } = string.Empty;

    public string ObjectName { get; init; } = string.Empty;
    public string Database { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public string ClientHost { get; init; } = string.Empty;
    public string AppName { get; init; } = string.Empty;
    public int SessionId { get; init; }

    public double DurationMs { get; init; }
    public double CpuMs { get; init; }
    public long LogicalReads { get; init; }
    public long RowCount { get; init; }

    public bool HasError { get; init; }
    public string ErrorMessage { get; init; } = string.Empty;

    /// <summary>Full "exec dbo.X @a=1" text; empty for module-only events.</summary>
    public string Statement { get; init; } = string.Empty;

    public IReadOnlyList<ProfilerParameterViewModel> Parameters { get; init; } = [];
}

public sealed class ProfilerParameterViewModel
{
    public string Name { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;

    /// <summary>Guessed from how the value is written, since XE does not report the type.</summary>
    public string Kind { get; init; } = string.Empty;
}

public sealed class ProfilerSummaryViewModel
{
    public int TotalCalls { get; init; }
    public double TotalDurationMs { get; init; }
    public double SlowestMs { get; init; }
    public string SlowestObject { get; init; } = string.Empty;
    public int ErrorCount { get; init; }
    public int DistinctObjects { get; init; }
}

public sealed class CaptureStateViewModel
{
    public string CaptureId { get; init; } = string.Empty;
    public bool Running { get; init; }
    public string? Error { get; init; }
    public long ElapsedMs { get; init; }
    public int EventsDropped { get; init; }
    public DateTime? AutoStopAt { get; init; }
    public ProfilerSummaryViewModel Summary { get; init; } = new();
    public IReadOnlyList<ProfilerEventViewModel> Events { get; init; } = [];
}

public sealed class ProcedureStatViewModel
{
    public string ObjectName { get; init; } = string.Empty;
    public long ExecutionCount { get; init; }
    public DateTime LastExecutionTime { get; init; }
    public string LastExecutionText { get; init; } = string.Empty;
    public double AverageMs { get; init; }
    public double TotalMs { get; init; }
    public double LastMs { get; init; }
    public double TotalCpuMs { get; init; }
    public long TotalLogicalReads { get; init; }
}

public sealed class RecentProceduresViewModel
{
    public int Minutes { get; init; }
    public string Database { get; init; } = string.Empty;
    public IReadOnlyList<ProcedureStatViewModel> Rows { get; init; } = [];
}

public sealed class ProfilerDetailViewModel
{
    public string ObjectName { get; init; } = string.Empty;
    public string Statement { get; init; } = string.Empty;
    public IReadOnlyList<ProfilerParameterViewModel> Parameters { get; init; } = [];
    public string Definition { get; init; } = string.Empty;
    public string? DefinitionError { get; init; }
}

public sealed class ProfilerPageViewModel
{
    public ProfilerCaptureOptions Options { get; init; } = new();
    public bool HostAllowlistEnabled { get; init; }
}
