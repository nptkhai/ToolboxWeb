using System.Diagnostics;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Microsoft.SqlServer.Dac;
using Microsoft.SqlServer.Dac.Compare;
using Microsoft.SqlServer.Dac.Model;
using ToolboxWeb.Web.Infrastructure.SqlSchema;
using ToolboxWeb.Web.ViewModels.SchemaCompare;

// The phase-1 engine has its own SchemaDifference; alias DacFx's to keep both usable.
using DacDifference = Microsoft.SqlServer.Dac.Compare.SchemaDifference;

namespace ToolboxWeb.Web.Infrastructure.DacFx;

/// <summary>
/// Everything the comparison needs, resolved while the request is still alive. Connection
/// strings are built and validated up front so the background task never touches a scoped
/// service, and the credentials stop travelling any further than this object.
/// </summary>
public sealed record DacFxComparePlan(
    string ComparisonId,
    string SourceConnectionString,
    string SourceDatabase,
    string SourceLabel,
    string TargetConnectionString,
    string TargetDatabase,
    string TargetLabel,
    CompareScopeOptions Scope,
    DacFxCompareOptions Options,
    IReadOnlyList<NameMappingPair> NameMappings,
    string SourceCode,
    string TargetCode);

public interface IDacFxCompareEngine
{
    /// <summary>
    /// Extracts both databases into packages (reporting progress as it goes) and compares
    /// them. The caller owns the returned <see cref="DacFxComparison"/> and must dispose it.
    /// </summary>
    Task<DacFxComparison> CompareAsync(
        DacFxComparePlan plan,
        DacFxProgressCollector progress,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// A live comparison: the DacFx result plus the index the UI needs to address individual
/// differences by id.
/// </summary>
public sealed class DacFxComparison : IDisposable
{
    private readonly string _workingDirectory;

    internal DacFxComparison(
        SchemaComparison comparison,
        SchemaComparisonResult result,
        IReadOnlyDictionary<string, DacDifference> index,
        string sourceLabel,
        string targetLabel,
        string targetDatabase,
        string workingDirectory,
        SqlNameMapper nameMapper,
        IReadOnlySet<string> mappedSameIds,
        IReadOnlySet<string> mappedSameObjectNames,
        HashSet<string> includedDifferenceIds)
    {
        Comparison = comparison;
        Result = result;
        Index = index;
        SourceLabel = sourceLabel;
        TargetLabel = targetLabel;
        TargetDatabase = targetDatabase;
        _workingDirectory = workingDirectory;
        NameMapper = nameMapper;
        MappedSameIds = mappedSameIds;
        MappedSameObjectNames = mappedSameObjectNames;
        IncludedDifferenceIds = includedDifferenceIds;
    }

    public SchemaComparison Comparison { get; }
    public SchemaComparisonResult Result { get; }

    /// <summary>Stable id per difference node, assigned in tree order.</summary>
    public IReadOnlyDictionary<string, DacDifference> Index { get; }

    public string SourceLabel { get; }
    public string TargetLabel { get; }
    public string TargetDatabase { get; }

    /// <summary>Applies the user's database-name substitutions; used on detail views and the script.</summary>
    public SqlNameMapper NameMapper { get; }

    /// <summary>Ids of differences suppressed because mapping made them equal to the target.</summary>
    public IReadOnlySet<string> MappedSameIds { get; }

    /// <summary>Schema-qualified module names removed from the generated script.</summary>
    public IReadOnlySet<string> MappedSameObjectNames { get; }

    /// <summary>
    /// UI selection is intentionally kept outside DacFx. Calling Include/Exclude repeatedly
    /// rebuilds its complete dependency graph and can take tens of minutes per large table.
    /// Only top-level ids are stored here; script post-processing applies the selection once.
    /// </summary>
    public HashSet<string> IncludedDifferenceIds { get; }

    public void Dispose()
    {
        // The extracted packages are only useful for this comparison.
        try
        {
            if (Directory.Exists(_workingDirectory))
            {
                Directory.Delete(_workingDirectory, recursive: true);
            }
        }
        catch (IOException)
        {
            // A stale temp folder is not worth failing a request over.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

public sealed class DacFxCompareEngine : IDacFxCompareEngine
{
    private readonly SqlSchemaOptions _options;
    private readonly ILogger<DacFxCompareEngine> _logger;

    public DacFxCompareEngine(
        IOptions<SqlSchemaOptions> options,
        ILogger<DacFxCompareEngine> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<DacFxComparison> CompareAsync(
        DacFxComparePlan plan,
        DacFxProgressCollector progress,
        CancellationToken cancellationToken = default)
    {
        var totalWatch = Stopwatch.StartNew();
        var workingDirectory = Path.Combine(
            Path.GetTempPath(),
            "ToolboxWeb.SchemaCompare",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingDirectory);

        try
        {
            var commandTimeout = Math.Clamp(_options.CommandTimeoutSeconds, 10, 600);
            var longRunningTimeout = Math.Clamp(_options.ExtractTimeoutSeconds, 60, 7200);
            var lockTimeout = Math.Clamp(_options.DatabaseLockTimeoutSeconds, 10, 600);
            var settings = $"SQL timeout: command {commandTimeout}s, long-running {longRunningTimeout}s, lock {lockTimeout}s";

            progress.Add(CompareSide.Source, settings, ProgressState.Running);
            progress.Add(CompareSide.Target, settings, ProgressState.Running);
            _logger.LogInformation(
                "Schema compare {ComparisonId} started for {SourceDatabase} -> {TargetDatabase}. {Settings}",
                plan.ComparisonId,
                plan.SourceDatabase,
                plan.TargetDatabase,
                settings);

            // Real databases produce very large in-memory DacFx models. Extract sequentially
            // so two model builders cannot push the web machine into paging; source goes first
            // so a source-side failure does not waste time extracting the target.
            var sourcePath = await ExtractAsync(
                plan.SourceConnectionString, plan.SourceDatabase, CompareSide.Source,
                workingDirectory, BuildExtractRequest(plan), plan.ComparisonId, progress, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            var targetPath = await ExtractAsync(
                plan.TargetConnectionString, plan.TargetDatabase, CompareSide.Target,
                workingDirectory, BuildExtractRequest(plan), plan.ComparisonId, progress, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            var comparison = new SchemaComparison(
                new SchemaCompareDacpacEndpoint(sourcePath),
                new SchemaCompareDacpacEndpoint(targetPath));

            ApplyOptions(comparison, plan);

            var result = await RunBlockingStageAsync(
                plan.ComparisonId,
                "DacFx object comparison",
                progress,
                () => comparison.Compare(cancellationToken),
                cancellationToken);

            var indexWatch = Stopwatch.StartNew();
            var index = BuildIndex(result, cancellationToken);
            _logger.LogInformation(
                "Schema compare {ComparisonId} indexed {NodeCount} difference nodes in {ElapsedMs} ms",
                plan.ComparisonId,
                index.Count,
                indexWatch.ElapsedMilliseconds);

            var nameMapper = SqlNameMapper.From(
                plan.NameMappings, plan.SourceCode, plan.TargetCode, plan.TargetDatabase);
            IReadOnlySet<string> mappedSameIds;
            IReadOnlySet<string> mappedSameObjectNames;
            if (nameMapper.HasMappings)
            {
                var reconciliation = await RunAsyncStageAsync(
                    plan.ComparisonId,
                    "Database-name reconciliation",
                    progress,
                    () => ReconcileMappingsAsync(
                        result, index, nameMapper, plan, cancellationToken),
                    cancellationToken);
                mappedSameIds = reconciliation.MappedSameIds;
                mappedSameObjectNames = reconciliation.MappedSameObjectNames;

                _logger.LogInformation(
                    "Schema compare {ComparisonId}: catalog reconciliation inspected {ChangedCount} changed top-level objects; {CatalogCandidateCount} had module definitions, {MappingCandidateCount} contained mapped names, {EquivalentCount} were equivalent and suppressed; stage limit reached: {TimedOut}",
                    plan.ComparisonId,
                    reconciliation.ChangedCount,
                    reconciliation.CatalogCandidateCount,
                    reconciliation.MappingCandidateCount,
                    reconciliation.EquivalentCount,
                    reconciliation.TimedOut);

                if (reconciliation.TimedOut)
                {
                    var partialMessage = "Database-name reconciliation reached its own time limit; the comparison result is available, but some mapping-only rows may remain visible.";
                    progress.Add(CompareSide.Source, partialMessage, ProgressState.Completed);
                    progress.Add(CompareSide.Target, partialMessage, ProgressState.Completed);
                }
            }
            else
            {
                mappedSameIds = new HashSet<string>(StringComparer.Ordinal);
                mappedSameObjectNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            var includedDifferenceIds = BuildDefaultIncludedIds(result, mappedSameIds);
            _logger.LogInformation(
                "Schema compare {ComparisonId}: initialized virtual selection with {IncludedCount} included and {ExcludedCount} excluded top-level differences; no DacFx dependency-graph mutations were performed",
                plan.ComparisonId,
                includedDifferenceIds.Count,
                result.Differences.Count() - includedDifferenceIds.Count);

            var completedMessage = $"Compare completed in {FormatElapsed(totalWatch.Elapsed)}";
            progress.Add(CompareSide.Source, completedMessage, ProgressState.Completed);
            progress.Add(CompareSide.Target, completedMessage, ProgressState.Completed);
            _logger.LogInformation(
                "Schema compare {ComparisonId} completed in {ElapsedMs} ms with {TopLevelDifferenceCount} top-level differences and {MappedSameCount} mapped equivalents",
                plan.ComparisonId,
                totalWatch.ElapsedMilliseconds,
                result.Differences.Count(),
                mappedSameIds.Count);

            return new DacFxComparison(
                comparison,
                result,
                index,
                plan.SourceLabel,
                plan.TargetLabel,
                plan.TargetDatabase,
                workingDirectory,
                nameMapper,
                mappedSameIds,
                mappedSameObjectNames,
                includedDifferenceIds);
        }
        catch
        {
            _logger.LogInformation(
                "Schema compare {ComparisonId} left the engine after {ElapsedMs} ms",
                plan.ComparisonId,
                totalWatch.ElapsedMilliseconds);
            TryDelete(workingDirectory);
            throw;
        }
    }

    private async Task<string> ExtractAsync(
        string connectionString,
        string databaseName,
        CompareSide side,
        string workingDirectory,
        DacFxExtractWorkerRequest request,
        string comparisonId,
        DacFxProgressCollector progress,
        CancellationToken cancellationToken)
    {
        var watch = Stopwatch.StartNew();
        var path = Path.Combine(workingDirectory, $"{side}.dacpac");
        request.ConnectionString = connectionString;
        request.DatabaseName = databaseName;
        request.OutputPath = path;
        progress.Add(side, $"Extract started for {databaseName}", ProgressState.Running);
        _logger.LogInformation(
            "Schema compare {ComparisonId}: {Side} extract started for {Database}",
            comparisonId,
            side,
            databaseName);

        // DacFx goes quiet for the whole model-building phase, which on a large database is
        // most of the run. Without a heartbeat there is no way to tell a slow extract from a
        // dead one.
        using var heartbeat = new CancellationTokenSource();
        var ticker = ReportHeartbeatAsync(
            comparisonId,
            $"Extracting {databaseName}",
            side,
            progress,
            watch,
            heartbeat.Token);

        try
        {
            await RunExtractWorkerAsync(request, side, comparisonId, progress, cancellationToken);
        }
        finally
        {
            await heartbeat.CancelAsync();
            await ticker;
        }

        var completedMessage = $"Extract completed for {databaseName} in {FormatElapsed(watch.Elapsed)}";
        progress.Add(side, completedMessage, ProgressState.Completed);
        _logger.LogInformation(
            "Schema compare {ComparisonId}: {Side} extract completed for {Database} in {ElapsedMs} ms; package {PackageBytes} bytes",
            comparisonId,
            side,
            databaseName,
            watch.ElapsedMilliseconds,
            new FileInfo(path).Length);
        return path;
    }

    private async Task RunExtractWorkerAsync(
        DacFxExtractWorkerRequest request,
        CompareSide side,
        string comparisonId,
        DacFxProgressCollector progress,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = ResolveDotNetHost(),
            WorkingDirectory = AppContext.BaseDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add(typeof(DacFxCompareEngine).Assembly.Location);
        startInfo.ArgumentList.Add(DacFxExtractWorker.CommandSwitch);
        startInfo.Environment["DOTNET_NOLOGO"] = "1";
        startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException("Could not start the DacFx extract worker.");
        }

        _logger.LogInformation(
            "Schema compare {ComparisonId}: {Side} extract worker {ProcessId} started",
            comparisonId,
            side,
            process.Id);

        var output = new WorkerOutputState();
        var outputTask = PumpWorkerOutputAsync(process.StandardOutput, output, side, progress);
        var errorTask = ReadBoundedAsync(process.StandardError, 4096);

        try
        {
            await process.StandardInput.WriteAsync(DacFxExtractWorker.SerializeRequest(request));
            process.StandardInput.Close();
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
            }

            _logger.LogWarning(
                "Schema compare {ComparisonId}: {Side} extract worker was terminated after cancellation",
                comparisonId,
                side);
            throw;
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
            }
        }

        await outputTask;
        var standardError = await errorTask;

        if (process.ExitCode != 0)
        {
            if (!string.IsNullOrWhiteSpace(standardError))
            {
                _logger.LogWarning(
                    "Schema compare {ComparisonId}: {Side} extract worker stderr: {WorkerError}",
                    comparisonId,
                    side,
                    SqlSchemaErrorHelper.Scrub(standardError));
            }

            throw new InvalidOperationException(
                output.Error ?? $"DacFx extract worker exited with code {process.ExitCode}.");
        }

        if (!File.Exists(request.OutputPath) || new FileInfo(request.OutputPath).Length == 0)
        {
            throw new InvalidOperationException("DacFx extract worker completed without producing a package.");
        }
    }

    private static async Task PumpWorkerOutputAsync(
        StreamReader reader,
        WorkerOutputState output,
        CompareSide side,
        DacFxProgressCollector progress)
    {
        while (await reader.ReadLineAsync() is { } line)
        {
            if (!DacFxExtractWorker.TryParseMessage(line, out var message) || message is null)
            {
                continue;
            }

            if (string.Equals(message.Type, "error", StringComparison.Ordinal))
            {
                output.Error = string.IsNullOrWhiteSpace(message.Detail)
                    ? message.Error
                    : $"{message.Error} — {message.Detail}";
                continue;
            }

            if (!string.Equals(message.Type, "progress", StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(message.Operation))
            {
                continue;
            }

            var state = Enum.TryParse<ProgressState>(message.State, out var parsed)
                ? parsed
                : ProgressState.Running;
            progress.Add(side, message.Operation, state);
        }
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader, int maxCharacters)
    {
        var result = new StringBuilder();
        while (await reader.ReadLineAsync() is { } line)
        {
            var remaining = maxCharacters - result.Length;
            if (remaining <= 0)
            {
                continue;
            }

            if (result.Length > 0)
            {
                result.AppendLine();
            }

            result.Append(line.AsSpan(0, Math.Min(line.Length, remaining)));
        }

        return result.ToString();
    }

    private static string ResolveDotNetHost()
    {
        var configured = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
        {
            return configured;
        }

        var installed = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "dotnet",
            "dotnet.exe");
        return File.Exists(installed) ? installed : "dotnet";
    }

    private sealed class WorkerOutputState
    {
        public string? Error { get; set; }
    }

    private async Task<T> RunBlockingStageAsync<T>(
        string comparisonId,
        string stage,
        DacFxProgressCollector progress,
        Func<T> action,
        CancellationToken cancellationToken)
    {
        var watch = Stopwatch.StartNew();
        progress.Add(CompareSide.Source, $"{stage} started", ProgressState.Running);
        progress.Add(CompareSide.Target, $"{stage} started", ProgressState.Running);
        _logger.LogInformation(
            "Schema compare {ComparisonId}: {Stage} started",
            comparisonId,
            stage);

        using var heartbeat = new CancellationTokenSource();
        var ticker = ReportHeartbeatAsync(
            comparisonId,
            stage,
            side: null,
            progress,
            watch,
            heartbeat.Token);

        try
        {
            var result = await Task.Run(action, cancellationToken);
            var completedMessage = $"{stage} completed in {FormatElapsed(watch.Elapsed)}";
            progress.Add(CompareSide.Source, completedMessage, ProgressState.Completed);
            progress.Add(CompareSide.Target, completedMessage, ProgressState.Completed);
            _logger.LogInformation(
                "Schema compare {ComparisonId}: {Stage} completed in {ElapsedMs} ms",
                comparisonId,
                stage,
                watch.ElapsedMilliseconds);
            return result;
        }
        finally
        {
            await heartbeat.CancelAsync();
            await ticker;
        }
    }

    private async Task<T> RunAsyncStageAsync<T>(
        string comparisonId,
        string stage,
        DacFxProgressCollector progress,
        Func<Task<T>> action,
        CancellationToken cancellationToken)
    {
        var watch = Stopwatch.StartNew();
        progress.Add(CompareSide.Source, $"{stage} started", ProgressState.Running);
        progress.Add(CompareSide.Target, $"{stage} started", ProgressState.Running);
        _logger.LogInformation(
            "Schema compare {ComparisonId}: {Stage} started",
            comparisonId,
            stage);

        using var heartbeat = new CancellationTokenSource();
        var ticker = ReportHeartbeatAsync(
            comparisonId,
            stage,
            side: null,
            progress,
            watch,
            heartbeat.Token);

        try
        {
            var result = await action();
            cancellationToken.ThrowIfCancellationRequested();
            var completedMessage = $"{stage} completed in {FormatElapsed(watch.Elapsed)}";
            progress.Add(CompareSide.Source, completedMessage, ProgressState.Completed);
            progress.Add(CompareSide.Target, completedMessage, ProgressState.Completed);
            _logger.LogInformation(
                "Schema compare {ComparisonId}: {Stage} completed in {ElapsedMs} ms",
                comparisonId,
                stage,
                watch.ElapsedMilliseconds);
            return result;
        }
        finally
        {
            await heartbeat.CancelAsync();
            await ticker;
        }
    }

    /// <summary>
    /// Writes regular progress while DacFx is silent. Memory is included because two large
    /// in-memory models can push the web process into paging, which otherwise looks like a
    /// database or network stall.
    /// </summary>
    private async Task ReportHeartbeatAsync(
        string comparisonId,
        string stage,
        CompareSide? side,
        DacFxProgressCollector progress,
        Stopwatch watch,
        CancellationToken cancellationToken)
    {
        try
        {
            var interval = TimeSpan.FromSeconds(Math.Clamp(_options.ProgressHeartbeatSeconds, 10, 300));
            using var timer = new PeriodicTimer(interval);
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                var memoryMb = Environment.WorkingSet / 1024d / 1024d;
                var message = $"{stage} still running ({FormatElapsed(watch.Elapsed)}; app memory {memoryMb:N0} MB)";

                if (side is null)
                {
                    progress.Add(CompareSide.Source, message, ProgressState.Running);
                    progress.Add(CompareSide.Target, message, ProgressState.Running);
                }
                else
                {
                    progress.Add(side.Value, message, ProgressState.Running);
                }

                _logger.LogInformation(
                    "Schema compare {ComparisonId}: {Stage} still running after {ElapsedMs} ms; app working set {WorkingSetMb:F0} MB",
                    comparisonId,
                    stage,
                    watch.ElapsedMilliseconds,
                    memoryMb);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal: the operation finished and switched the heartbeat off.
        }
    }

    /// <summary>
    /// Trims the extract down to what a schema comparison actually needs and bounds every
    /// timeout it is able to wait on.
    /// <para>
    /// Extraction ignores the connection string's <c>Command Timeout</c> and uses
    /// <see cref="DacOptionsBase.CommandTimeout"/> instead, which defaults to 60 seconds.
    /// Passing that, DacFx reports "Could not extract package from specified database" with
    /// "Unable to reconnect to database" underneath, which reads like a network fault.
    /// </para>
    /// <para>
    /// <see cref="DacOptionsBase.LongRunningCommandTimeout"/> is the opposite trap: it
    /// defaults to 0, meaning "wait forever", so the model-building queries on a large
    /// database have no stopping point at all. Both get the same real limit here.
    /// </para>
    /// </summary>
    private DacFxExtractWorkerRequest BuildExtractRequest(DacFxComparePlan plan)
    {
        var wantsSecurity = plan.Scope.SecurityObjects;
        var commandTimeout = Math.Clamp(_options.CommandTimeoutSeconds, 10, 600);
        var longRunningTimeout = Math.Clamp(_options.ExtractTimeoutSeconds, 60, 7200);
        var lockTimeout = Math.Clamp(_options.DatabaseLockTimeoutSeconds, 10, 600);

        return new DacFxExtractWorkerRequest
        {
            VerifyExtraction = _options.VerifyExtraction,
            ExtractApplicationScopedObjectsOnly = !wantsSecurity,
            IgnorePermissions = !wantsSecurity,
            IgnoreUserLoginMappings = !wantsSecurity,

            // Defaults to true, which sends DacFx off to read logins and server roles on a
            // server that may host hundreds of databases. Nothing outside the security scope
            // compares them, so skip the trip.
            ExtractReferencedServerScopedElements = wantsSecurity,

            // Same reasoning: do not pay to read what the comparison will ignore anyway.
            IgnoreExtendedProperties = !plan.Scope.ExtendedProperties || plan.Options.IgnoreExtendedProperties,

            // Ordinary catalog queries and lock waits should fail promptly. Only commands
            // DacFx explicitly classifies as long-running receive the larger extract limit.
            // Setting all three to 900 seconds made a blocked catalog read wait 15x longer
            // than the former working configuration.
            CommandTimeoutSeconds = commandTimeout,
            LongRunningCommandTimeoutSeconds = longRunningTimeout,
            DatabaseLockTimeoutSeconds = lockTimeout
        };
    }

    private static void ApplyOptions(SchemaComparison comparison, DacFxComparePlan plan)
    {
        var options = comparison.Options;
        var requested = plan.Options;

        options.IgnoreWhitespace = requested.IgnoreWhitespace;
        options.IgnoreComments = requested.IgnoreComments;
        options.IgnoreKeywordCasing = requested.IgnoreKeywordCasing;
        options.IgnoreColumnOrder = requested.IgnoreColumnOrder;
        options.IgnoreIndexOptions = requested.IgnoreIndexOptions;
        options.IgnoreFillFactor = requested.IgnoreFillFactor;
        options.IgnoreFilegroupPlacement = requested.IgnoreFilegroupPlacement;
        options.IgnoreIdentitySeed = requested.IgnoreIdentitySeed;
        options.IgnoreExtendedProperties = requested.IgnoreExtendedProperties;

        // Keep the raw result complete. DROP remains opt-in through the comparison's virtual
        // selection and one linear script-filtering pass. Mutating DacFx selection here or
        // after Compare forces dependency-graph rebuilds on production-sized schemas.
        options.DropObjectsNotInSource = true;
        options.DropConstraintsNotInSource = true;
        options.DropDmlTriggersNotInSource = true;
        options.DropExtendedPropertiesNotInSource = true;
        options.DropIndexesNotInSource = true;
        options.DropPermissionsNotInSource = true;
        options.DropRoleMembersNotInSource = true;
        options.DropStatisticsNotInSource = true;
        options.BlockOnPossibleDataLoss = true;

        // A schema sync must not silently alter database-wide runtime behaviour. The reviewed
        // production-sized script changed scoped configuration and disabled full-text without
        // re-enabling it. Object-level differences remain available in the grid.
        options.ScriptDatabaseOptions = false;

        options.ExcludeObjectTypes = BuildExcludedTypes(plan.Scope).ToArray();
    }

    private static HashSet<string> BuildDefaultIncludedIds(
        SchemaComparisonResult result,
        IReadOnlySet<string> mappedSameIds)
    {
        var included = new HashSet<string>(StringComparer.Ordinal);
        var position = 0;
        foreach (var difference in result.Differences)
        {
            var id = position.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (!mappedSameIds.Contains(id)
                && !DacFxDifferenceMapper.ContainsDelete(difference))
            {
                included.Add(id);
            }
            position++;
        }
        return included;
    }

    private static IEnumerable<ObjectType> BuildExcludedTypes(CompareScopeOptions scope)
    {
        if (!scope.Tables) yield return ObjectType.Tables;

        if (!scope.Views) yield return ObjectType.Views;

        if (!scope.StoredProcedures) yield return ObjectType.StoredProcedures;

        if (!scope.Functions)
        {
            yield return ObjectType.ScalarValuedFunctions;
            yield return ObjectType.TableValuedFunctions;
            yield return ObjectType.Aggregates;
        }

        if (!scope.Triggers) yield return ObjectType.DatabaseTriggers;

        if (!scope.TableTypes) yield return ObjectType.UserDefinedTableTypes;

        if (!scope.Sequences) yield return ObjectType.Sequences;

        if (!scope.Synonyms) yield return ObjectType.Synonyms;

        if (!scope.ExtendedProperties) yield return ObjectType.ExtendedProperties;

        if (!scope.FullText) yield return ObjectType.FullTextCatalogs;

        if (!scope.SecurityObjects)
        {
            yield return ObjectType.Users;
            yield return ObjectType.Logins;
            yield return ObjectType.Permissions;
            yield return ObjectType.RoleMembership;
        }
    }

    /// <summary>
    /// Walks the difference tree once and gives every node an id the browser can send back.
    /// Ids are positional ("3", "3.1") and only valid for this comparison.
    /// </summary>
    private static Dictionary<string, DacDifference> BuildIndex(
        SchemaComparisonResult result,
        CancellationToken cancellationToken)
    {
        var index = new Dictionary<string, DacDifference>(StringComparer.Ordinal);
        var position = 0;

        foreach (var difference in result.Differences)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Walk(difference, position.ToString(), index, cancellationToken);
            position++;
        }

        return index;

        static void Walk(
            DacDifference node,
            string id,
            Dictionary<string, DacDifference> into,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            into[id] = node;
            var childPosition = 0;
            foreach (var child in node.Children)
            {
                Walk(child, $"{id}.{childPosition}", into, cancellationToken);
                childPosition++;
            }
        }
    }

    /// <summary>
    /// Finds each top-level "changed" module whose source, after name mapping and cosmetic
    /// normalization, equals the target — the false positives the user sees when the origin
    /// database references <c>EDU_ORG_DATA</c> and the target already references
    /// <c>EDU_FBU_DATA</c>. Calling DacFx <c>Exclude</c> once per object rebuilds its dependency
    /// graph and took roughly 15 seconds per object on the production-sized pair. Instead the
    /// ids hide the rows immediately and the schema-qualified names let script post-processing
    /// remove those already-equivalent module batches in one linear pass.
    /// </summary>
    private async Task<ReconciliationSummary> ReconcileMappingsAsync(
        SchemaComparisonResult result,
        IReadOnlyDictionary<string, DacDifference> index,
        SqlNameMapper mapper,
        DacFxComparePlan plan,
        CancellationToken cancellationToken)
    {
        // DacFx scripting is intentionally not used here. On the production-sized pair it
        // walked a 123k-node model for every changed object and spent more than 19 minutes in
        // this stage alone. sys.sql_modules returns every relevant definition in one query.
        var stageLimit = TimeSpan.FromSeconds(
            Math.Clamp(_options.MappingReconciliationTimeoutSeconds, 10, 600));
        using var stageCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        stageCancellation.CancelAfter(stageLimit);
        var stageToken = stageCancellation.Token;
        var stageWatch = Stopwatch.StartNew();

        var sourceTask = ReadModuleCatalogAsync(plan.SourceConnectionString, stageToken);
        var targetTask = ReadModuleCatalogAsync(plan.TargetConnectionString, stageToken);
        try
        {
            await Task.WhenAll(sourceTask, targetTask);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Schema compare {ComparisonId}: catalog reconciliation stopped after its {StageLimitSeconds}s limit while reading module definitions; returning the DacFx result without mapping cleanup",
                plan.ComparisonId,
                (int)stageLimit.TotalSeconds);
            return new ReconciliationSummary(
                new HashSet<string>(StringComparer.Ordinal),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                0, 0, 0, 0, TimedOut: true);
        }

        var sourceCatalog = await sourceTask;
        var targetCatalog = await targetTask;
        _logger.LogInformation(
            "Schema compare {ComparisonId}: loaded {SourceModuleCount} source and {TargetModuleCount} target module definitions in {ElapsedMs} ms",
            plan.ComparisonId,
            sourceCatalog.Count,
            targetCatalog.Count,
            stageWatch.ElapsedMilliseconds);
        var mappedSame = new HashSet<string>(StringComparer.Ordinal);
        var mappedSameObjectNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var topLevel = index.Where(pair => !pair.Key.Contains('.', StringComparison.Ordinal));
        var changedCount = 0;
        var catalogCandidateCount = 0;
        var mappingCandidateCount = 0;
        var timedOut = false;

        foreach (var (id, difference) in topLevel)
        {
            if (stageToken.IsCancellationRequested)
            {
                cancellationToken.ThrowIfCancellationRequested();
                timedOut = true;
                break;
            }

            if (difference.UpdateAction != SchemaUpdateAction.Change)
            {
                continue;
            }

            changedCount++;

            var identifier = difference.SourceObject?.Name ?? difference.TargetObject?.Name;
            var key = GetObjectKey(identifier);
            if (key is null
                || !sourceCatalog.TryGetValue(key, out var source)
                || !targetCatalog.TryGetValue(key, out var target))
            {
                continue;
            }

            catalogCandidateCount++;

            if (!mapper.CouldAffectSource(source.Definition))
            {
                continue;
            }

            mappingCandidateCount++;

            if (!source.HasSameSettings(target)
                || !mapper.AreEquivalent(source.Definition, target.Definition))
            {
                continue;
            }

            mappedSame.Add(id);
            mappedSameObjectNames.Add(key);
        }

        _logger.LogInformation(
            "Schema compare {ComparisonId}: matched and suppressed {EquivalentCount} mapping-only modules from {ChangedCount} changed objects in {ElapsedMs} ms",
            plan.ComparisonId,
            mappedSame.Count,
            changedCount,
            stageWatch.ElapsedMilliseconds);

        return new ReconciliationSummary(
            mappedSame,
            mappedSameObjectNames,
            changedCount,
            catalogCandidateCount,
            mappingCandidateCount,
            mappedSame.Count,
            timedOut);
    }

    private async Task<Dictionary<string, ModuleSnapshot>> ReadModuleCatalogAsync(
        string connectionString,
        CancellationToken cancellationToken)
    {
        var catalog = new Dictionary<string, ModuleSnapshot>(StringComparer.OrdinalIgnoreCase);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(SqlSchemaQueries.ModuleDefinitionsForMapping, connection)
        {
            CommandTimeout = Math.Clamp(_options.CommandTimeoutSeconds, 10, 600)
        };
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var definitionOrdinal = reader.GetOrdinal("ModuleDefinition");
            if (reader.IsDBNull(definitionOrdinal))
            {
                continue;
            }

            var key = $"{reader.GetString(reader.GetOrdinal("SchemaName"))}.{reader.GetString(reader.GetOrdinal("ObjectName"))}";
            var executeAsOrdinal = reader.GetOrdinal("ExecuteAsPrincipal");
            catalog[key] = new ModuleSnapshot(
                reader.GetString(definitionOrdinal),
                reader.GetBoolean(reader.GetOrdinal("UsesAnsiNulls")),
                reader.GetBoolean(reader.GetOrdinal("UsesQuotedIdentifier")),
                reader.GetBoolean(reader.GetOrdinal("IsSchemaBound")),
                reader.GetBoolean(reader.GetOrdinal("UsesDatabaseCollation")),
                reader.GetBoolean(reader.GetOrdinal("IsRecompiled")),
                reader.GetBoolean(reader.GetOrdinal("NullOnNullInput")),
                reader.GetBoolean(reader.GetOrdinal("UsesNativeCompilation")),
                reader.IsDBNull(executeAsOrdinal) ? null : reader.GetString(executeAsOrdinal));
        }

        return catalog;
    }

    private static string? GetObjectKey(ObjectIdentifier? identifier)
    {
        if (identifier is null || identifier.Parts.Count == 0)
        {
            return null;
        }

        var parts = identifier.Parts;
        return parts.Count == 1 ? parts[0] : $"{parts[^2]}.{parts[^1]}";
    }

    private sealed record ModuleSnapshot(
        string Definition,
        bool UsesAnsiNulls,
        bool UsesQuotedIdentifier,
        bool IsSchemaBound,
        bool UsesDatabaseCollation,
        bool IsRecompiled,
        bool NullOnNullInput,
        bool UsesNativeCompilation,
        string? ExecuteAsPrincipal)
    {
        public bool HasSameSettings(ModuleSnapshot other) =>
            UsesAnsiNulls == other.UsesAnsiNulls
            && UsesQuotedIdentifier == other.UsesQuotedIdentifier
            && IsSchemaBound == other.IsSchemaBound
            && UsesDatabaseCollation == other.UsesDatabaseCollation
            && IsRecompiled == other.IsRecompiled
            && NullOnNullInput == other.NullOnNullInput
            && UsesNativeCompilation == other.UsesNativeCompilation
            && string.Equals(ExecuteAsPrincipal, other.ExecuteAsPrincipal, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record ReconciliationSummary(
        IReadOnlySet<string> MappedSameIds,
        IReadOnlySet<string> MappedSameObjectNames,
        int ChangedCount,
        int CatalogCandidateCount,
        int MappingCandidateCount,
        int EquivalentCount,
        bool TimedOut);

    private static string FormatElapsed(TimeSpan elapsed)
    {
        return elapsed.TotalHours >= 1
            ? elapsed.ToString(@"hh\:mm\:ss")
            : elapsed.ToString(@"mm\:ss");
    }

    private static void TryDelete(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
