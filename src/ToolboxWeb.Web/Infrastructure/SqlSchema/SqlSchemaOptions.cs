namespace ToolboxWeb.Web.Infrastructure.SqlSchema;

/// <summary>
/// Bound from the <c>SchemaCompare</c> section of configuration.
/// </summary>
public sealed class SqlSchemaOptions
{
    /// <summary>
    /// Hosts the schema reader is allowed to connect to. Empty means no restriction.
    /// Set this when the app is reachable by users you do not fully trust, because the
    /// feature otherwise lets any signed-in user point the web server at any host.
    /// </summary>
    public string[] AllowedHosts { get; set; } = [];

    public int ConnectTimeoutSeconds { get; set; } = 10;

    /// <summary>Timeout for ordinary SQL commands, including DacFx catalog queries.</summary>
    public int CommandTimeoutSeconds { get; set; } = 180;

    /// <summary>
    /// How long DacFx waits to acquire a database lock. Keep this separate from the longer
    /// model-building timeout so lock contention is reported instead of looking like a hang.
    /// </summary>
    public int DatabaseLockTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Timeout for commands DacFx classifies as long-running while it builds a model.
    /// This is deliberately separate from ordinary commands and lock waits; giving every
    /// catalog query this larger value made blocked extraction look like an endless run.
    /// </summary>
    public int ExtractTimeoutSeconds { get; set; } = 900;

    /// <summary>
    /// DacFx re-validates the whole model after extracting it, which costs time a schema
    /// comparison does not need, so it is off by default.
    /// </summary>
    public bool VerifyExtraction { get; set; }

    /// <summary>
    /// Deadline after which cancellation is requested for the whole comparison. DacFx
    /// cancellation is cooperative, so progress logging records both this request and the
    /// later time at which the active command actually returns.
    /// </summary>
    public int TotalTimeoutMinutes { get; set; } = 30;

    /// <summary>
    /// Maximum time spent removing false differences caused only by database-name mapping.
    /// This optional cleanup must never consume the deadline of an otherwise completed DacFx
    /// comparison; if it reaches this limit, the usable result is returned with any remaining
    /// mapping-only rows left visible.
    /// </summary>
    public int MappingReconciliationTimeoutSeconds { get; set; } = 120;

    /// <summary>How often a silent DacFx stage reports elapsed time and process memory.</summary>
    public int ProgressHeartbeatSeconds { get; set; } = 30;

    /// <summary>How long a generated script stays downloadable.</summary>
    public int ResultTtlMinutes { get; set; } = 30;
}
