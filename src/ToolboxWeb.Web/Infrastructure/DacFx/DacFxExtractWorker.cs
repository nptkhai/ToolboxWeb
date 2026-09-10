using System.Text.Json;
using Microsoft.SqlServer.Dac;
using ToolboxWeb.Web.Infrastructure.SqlSchema;

namespace ToolboxWeb.Web.Infrastructure.DacFx;

/// <summary>
/// Runs DacFx extraction in a child process. DacFx cancellation is cooperative and can stay
/// inside a catalog query for hours; isolating it lets the web process enforce a real deadline
/// by terminating only the worker, without taking the website down with it.
/// </summary>
public static class DacFxExtractWorker
{
    public const string CommandSwitch = "--toolbox-schema-extract-worker";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private static readonly object OutputLock = new();

    public static async Task<bool> TryRunAsync(string[] args)
    {
        if (!args.Contains(CommandSwitch, StringComparer.Ordinal))
        {
            return false;
        }

        Environment.ExitCode = await RunAsync();
        return true;
    }

    internal static string SerializeRequest(DacFxExtractWorkerRequest request) =>
        JsonSerializer.Serialize(request, JsonOptions);

    internal static bool TryParseMessage(string value, out DacFxExtractWorkerMessage? message)
    {
        try
        {
            message = JsonSerializer.Deserialize<DacFxExtractWorkerMessage>(value, JsonOptions);
            return message is not null;
        }
        catch (JsonException)
        {
            message = null;
            return false;
        }
    }

    private static async Task<int> RunAsync()
    {
        try
        {
            var input = await Console.In.ReadToEndAsync();
            var request = JsonSerializer.Deserialize<DacFxExtractWorkerRequest>(input, JsonOptions)
                ?? throw new InvalidOperationException("The extract worker request is empty.");

            Validate(request);

            var services = new DacServices(request.ConnectionString);
            services.ProgressChanged += (_, e) => WriteMessage(new DacFxExtractWorkerMessage
            {
                Type = "progress",
                Operation = e.Message,
                State = Map(e.Status).ToString()
            });
            services.Message += (_, e) => WriteMessage(new DacFxExtractWorkerMessage
            {
                Type = "progress",
                Operation = e.Message.Message,
                State = e.Message.MessageType == DacMessageType.Message
                    ? ProgressState.Completed.ToString()
                    : ProgressState.Failed.ToString()
            });

            services.Extract(
                request.OutputPath,
                request.DatabaseName,
                "ToolboxWeb",
                new Version(1, 0),
                extractOptions: BuildOptions(request),
                cancellationToken: CancellationToken.None);

            WriteMessage(new DacFxExtractWorkerMessage { Type = "completed" });
            return 0;
        }
        catch (Exception exception)
        {
            var deepest = exception;
            while (deepest.InnerException is not null)
            {
                deepest = deepest.InnerException;
            }

            WriteMessage(new DacFxExtractWorkerMessage
            {
                Type = "error",
                Error = SqlSchemaErrorHelper.Scrub(exception.Message),
                Detail = ReferenceEquals(deepest, exception)
                    ? null
                    : SqlSchemaErrorHelper.Scrub(deepest.Message)
            });
            return 2;
        }
    }

    private static DacExtractOptions BuildOptions(DacFxExtractWorkerRequest request) => new()
    {
        ExtractAllTableData = false,
        VerifyExtraction = request.VerifyExtraction,
        ExtractApplicationScopedObjectsOnly = request.ExtractApplicationScopedObjectsOnly,
        IgnorePermissions = request.IgnorePermissions,
        IgnoreUserLoginMappings = request.IgnoreUserLoginMappings,
        ExtractReferencedServerScopedElements = request.ExtractReferencedServerScopedElements,
        IgnoreExtendedProperties = request.IgnoreExtendedProperties,
        CommandTimeout = request.CommandTimeoutSeconds,
        LongRunningCommandTimeout = request.LongRunningCommandTimeoutSeconds,
        DatabaseLockTimeout = request.DatabaseLockTimeoutSeconds
    };

    private static void Validate(DacFxExtractWorkerRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ConnectionString)
            || string.IsNullOrWhiteSpace(request.DatabaseName)
            || string.IsNullOrWhiteSpace(request.OutputPath))
        {
            throw new InvalidOperationException("The extract worker request is incomplete.");
        }

        var fullOutputPath = Path.GetFullPath(request.OutputPath);
        var tempRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ToolboxWeb.SchemaCompare"));
        if (!fullOutputPath.StartsWith(tempRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The extract worker output path is outside the schema-compare workspace.");
        }
    }

    private static void WriteMessage(DacFxExtractWorkerMessage message)
    {
        message.Operation = SqlSchemaErrorHelper.Scrub(message.Operation);
        lock (OutputLock)
        {
            Console.Out.WriteLine(JsonSerializer.Serialize(message, JsonOptions));
            Console.Out.Flush();
        }
    }

    private static ProgressState Map(DacOperationStatus status) => status switch
    {
        DacOperationStatus.Completed => ProgressState.Completed,
        DacOperationStatus.Faulted or DacOperationStatus.Cancelled => ProgressState.Failed,
        _ => ProgressState.Running
    };
}

internal sealed class DacFxExtractWorkerRequest
{
    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public int CommandTimeoutSeconds { get; set; }
    public int LongRunningCommandTimeoutSeconds { get; set; }
    public int DatabaseLockTimeoutSeconds { get; set; }
    public bool VerifyExtraction { get; set; }
    public bool ExtractApplicationScopedObjectsOnly { get; set; }
    public bool IgnorePermissions { get; set; }
    public bool IgnoreUserLoginMappings { get; set; }
    public bool ExtractReferencedServerScopedElements { get; set; }
    public bool IgnoreExtendedProperties { get; set; }
}

internal sealed class DacFxExtractWorkerMessage
{
    public string Type { get; set; } = string.Empty;
    public string? Operation { get; set; }
    public string? State { get; set; }
    public string? Error { get; set; }
    public string? Detail { get; set; }
}
