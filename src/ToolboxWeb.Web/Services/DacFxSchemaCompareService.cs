using System.Diagnostics;
using System.Runtime;
using System.Text;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using ToolboxWeb.Web.Enums;
using ToolboxWeb.Web.Infrastructure.DacFx;
using ToolboxWeb.Web.Infrastructure.SqlSchema;
using ToolboxWeb.Web.ViewModels.SchemaCompare;

namespace ToolboxWeb.Web.Services;

public interface IDacFxSchemaCompareService
{
    /// <summary>Starts the comparison in the background and returns its id immediately.</summary>
    string Start(DacFxCompareRequest request, string sessionId);

    CompareProgressViewModel? GetProgress(string comparisonId, string sessionId);

    Task<ToggleIncludeResultViewModel?> ToggleAsync(ToggleIncludeRequest request, string sessionId);

    Task<DiffDetailViewModel?> GetDetailAsync(string comparisonId, string differenceId, string sessionId);

    Task<ScriptViewModel?> GetScriptAsync(string comparisonId, string sessionId);

    /// <summary>Stops a running comparison. Returns false when there is nothing to stop.</summary>
    bool Cancel(string comparisonId, string sessionId);
}

public sealed class DacFxSchemaCompareService : IDacFxSchemaCompareService
{
    private readonly IDacFxCompareEngine _engine;
    private readonly ISchemaCompareSessionStore _sessions;
    private readonly ISqlConnectionStringFactory _connectionStrings;
    private readonly ICurrentUserService _currentUser;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly ILogger<DacFxSchemaCompareService> _logger;
    private readonly TimeSpan _totalTimeout;

    public DacFxSchemaCompareService(
        IDacFxCompareEngine engine,
        ISchemaCompareSessionStore sessions,
        ISqlConnectionStringFactory connectionStrings,
        ICurrentUserService currentUser,
        IServiceScopeFactory scopeFactory,
        IOptions<SqlSchemaOptions> options,
        IStringLocalizer<SharedResource> localizer,
        ILogger<DacFxSchemaCompareService> logger)
    {
        _engine = engine;
        _sessions = sessions;
        _connectionStrings = connectionStrings;
        _currentUser = currentUser;
        _scopeFactory = scopeFactory;
        _totalTimeout = TimeSpan.FromMinutes(Math.Clamp(options.Value.TotalTimeoutMinutes, 1, 480));
        _localizer = localizer;
        _logger = logger;
    }

    public string Start(DacFxCompareRequest request, string sessionId)
    {
        // Built here, while the scoped factory is still alive, and so the credentials never
        // reach the background task in raw form.
        var sourceConnectionString = _connectionStrings.Create(request.Source, requireDatabase: true);
        var targetConnectionString = _connectionStrings.Create(request.Target, requireDatabase: true);
        var entry = _sessions.Start(sessionId);

        var plan = new DacFxComparePlan(
            entry.ComparisonId,
            sourceConnectionString,
            request.Source.Database.Trim(),
            request.Source.Label,
            targetConnectionString,
            request.Target.Database.Trim(),
            request.Target.Label,
            request.Scope,
            request.Options,
            request.NameMappings ?? [],
            request.SourceCode ?? string.Empty,
            request.TargetCode ?? string.Empty);

        var userId = _currentUser.UserId;

        // The deadline is what makes a comparison finite. Without it a slow extract just
        // keeps running, and the only way out is to restart the application.
        entry.Cancellation.CancelAfter(_totalTimeout);

        _logger.LogInformation(
            "Queued schema compare {ComparisonId} for {SourceDatabase} -> {TargetDatabase}; total deadline {TotalTimeoutMinutes} minutes",
            entry.ComparisonId,
            plan.SourceDatabase,
            plan.TargetDatabase,
            (int)_totalTimeout.TotalMinutes);

        _ = Task.Run(() => RunAsync(entry, plan, userId));
        return entry.ComparisonId;
    }

    public bool Cancel(string comparisonId, string sessionId)
    {
        var entry = _sessions.Get(comparisonId, sessionId);
        if (entry is null || entry.Completed)
        {
            return false;
        }

        entry.CancelledByUser = true;
        try
        {
            entry.Cancellation.Cancel();
            _logger.LogInformation(
                "User requested cancellation of schema compare {ComparisonId} after {ElapsedMs} ms",
                comparisonId,
                (long)(DateTime.Now - entry.CreatedAt).TotalMilliseconds);
        }
        catch (ObjectDisposedException)
        {
            return false;
        }

        return true;
    }

    private async Task RunAsync(SchemaCompareSessionEntry entry, DacFxComparePlan plan, string userId)
    {
        var watch = Stopwatch.StartNew();
        using var cancellationDiagnostic = entry.Cancellation.Token.Register(() =>
        {
            var reason = entry.CancelledByUser ? "user request" : "total deadline";
            var message = $"Stop requested after {FormatElapsed(watch.Elapsed)} ({reason}); stopping the active DacFx stage";
            entry.Progress.Add(CompareSide.Source, message, ProgressState.Running);
            entry.Progress.Add(CompareSide.Target, message, ProgressState.Running);
            _logger.LogWarning(
                "Schema compare {ComparisonId}: cancellation requested after {ElapsedMs} ms due to {Reason}; stopping the active DacFx stage",
                entry.ComparisonId,
                watch.ElapsedMilliseconds,
                reason);
        });

        try
        {
            entry.Comparison = await _engine.CompareAsync(
                plan, entry.Progress, entry.Cancellation.Token);
            entry.ElapsedMs = watch.ElapsedMilliseconds;
            entry.Completed = true;

            LogTimeline(entry, "completed");
            await LogActivityAsync(entry, plan, userId);
        }
        catch (OperationCanceledException)
        {
            watch.Stop();
            entry.ElapsedMs = watch.ElapsedMilliseconds;
            entry.Error = entry.CancelledByUser
                ? _localizer["SchemaCompare.Error.Cancelled"].Value
                : string.Format(
                    _localizer["SchemaCompare.Error.TotalTimeout"].Value,
                    (int)_totalTimeout.TotalMinutes);
            entry.Completed = true;
            entry.Progress.Add(CompareSide.Source, entry.Error, ProgressState.Failed);
            entry.Progress.Add(CompareSide.Target, entry.Error, ProgressState.Failed);
            _logger.LogWarning(
                "Schema compare {ComparisonId} cancelled after {ElapsedMs} ms; cancelled by user: {CancelledByUser}",
                entry.ComparisonId,
                entry.ElapsedMs,
                entry.CancelledByUser);
            LogTimeline(entry, "cancelled");
            ReleaseFailedComparisonMemory(entry.ComparisonId);
        }
        catch (Exception ex)
        {
            watch.Stop();
            entry.ElapsedMs = watch.ElapsedMilliseconds;
            entry.Error = DescribeFailure(ex);
            entry.Completed = true;
            entry.Progress.Add(CompareSide.Source, entry.Error, ProgressState.Failed);

            _logger.LogWarning(
                ex,
                "Schema compare failed for {Source} -> {Target}: {Reason}",
                SqlSchemaErrorHelper.Scrub(plan.SourceLabel),
                SqlSchemaErrorHelper.Scrub(plan.TargetLabel),
                SqlSchemaErrorHelper.Scrub(ex.Message));
            LogTimeline(entry, "failed");
            ReleaseFailedComparisonMemory(entry.ComparisonId);
        }
    }

    private void ReleaseFailedComparisonMemory(string comparisonId)
    {
        // A failed DacFx result is not IDisposable. Its two expanded schema models can remain
        // in generation 2 / the LOH for a long time (3+ GB was observed after cancellation),
        // making the next comparison page heavily. At this point the engine frame has unwound
        // and no usable result exists, so a one-off compacting collection is appropriate.
        var beforeMb = Environment.WorkingSet / 1024d / 1024d;
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        var afterMb = Environment.WorkingSet / 1024d / 1024d;

        _logger.LogInformation(
            "Schema compare {ComparisonId}: released failed DacFx models; app working set {BeforeMb:F0} MB -> {AfterMb:F0} MB",
            comparisonId,
            beforeMb,
            afterMb);
    }

    /// <summary>
    /// Writes the whole per-step timeline into the log once the run is over.
    /// <para>
    /// The extract worker is a separate process; its detailed steps only ever reach the
    /// progress collector on their way to the browser, so without this they are lost the
    /// moment the page is closed — which is exactly when a half-hour run needs explaining.
    /// </para>
    /// </summary>
    private void LogTimeline(SchemaCompareSessionEntry entry, string outcome)
    {
        var lines = entry.Progress.Snapshot();
        var text = new StringBuilder();
        text.Append("Schema compare ").Append(entry.ComparisonId)
            .Append(" timeline (").Append(outcome).Append(", ")
            .Append(lines.Count).AppendLine(" steps):");

        DateTime? previous = null;
        foreach (var line in lines)
        {
            var gap = previous is null ? TimeSpan.Zero : line.At - previous.Value;
            previous = line.At;
            text.Append("    ").Append(line.At.ToString("HH:mm:ss.fff"))
                .Append(" (+").Append(((int)gap.TotalSeconds).ToString()).Append("s) ")
                .Append(line.Side).Append(' ')
                .Append(line.State).Append(' ')
                .AppendLine(line.Operation);
        }

        _logger.LogInformation("{Timeline}", text.ToString());
    }

    public CompareProgressViewModel? GetProgress(string comparisonId, string sessionId)
    {
        var entry = _sessions.Get(comparisonId, sessionId);
        if (entry is null)
        {
            return null;
        }

        _sessions.Touch(entry);
        var snapshot = entry.Progress.Snapshot();

        DacFxCompareResultViewModel? result = null;
        if (entry.Completed && entry.Comparison is not null)
        {
            var rows = DacFxDifferenceMapper.BuildRows(entry.Comparison);
            result = new DacFxCompareResultViewModel
            {
                ComparisonId = entry.ComparisonId,
                SourceLabel = entry.Comparison.SourceLabel,
                TargetLabel = entry.Comparison.TargetLabel,
                GeneratedAt = entry.CreatedAt,
                ElapsedMs = entry.ElapsedMs,
                IsEqual = entry.Comparison.Result.IsEqual,
                Stats = DacFxDifferenceMapper.BuildStats(rows),
                Rows = rows,
                Warnings = entry.Comparison.Result.GetErrors().Select(x => x.Message).ToArray()
            };
        }

        return new CompareProgressViewModel
        {
            ComparisonId = entry.ComparisonId,
            Running = !entry.Completed,
            Completed = entry.Completed,
            Error = entry.Error,
            ElapsedMs = entry.Completed ? entry.ElapsedMs : (long)(DateTime.Now - entry.CreatedAt).TotalMilliseconds,
            Source = MapProgress(snapshot, CompareSide.Source),
            Target = MapProgress(snapshot, CompareSide.Target),
            Result = result
        };
    }

    public async Task<ToggleIncludeResultViewModel?> ToggleAsync(ToggleIncludeRequest request, string sessionId)
    {
        var entry = _sessions.Get(request.ComparisonId, sessionId);
        if (entry?.Comparison is null)
        {
            return null;
        }

        _sessions.Touch(entry);

        return await entry.LockedAsync(comparison =>
        {
            foreach (var id in request.Ids)
            {
                // The grid only addresses top-level objects. Keep its selection in memory;
                // SchemaComparisonResult.Include/Exclude rebuilds DacFx's dependency graph
                // and took over 23 minutes for one production table.
                if (id.Contains('.', StringComparison.Ordinal)
                    || !comparison.Index.ContainsKey(id)
                    || comparison.MappedSameIds.Contains(id))
                {
                    continue;
                }

                if (request.Include)
                {
                    comparison.IncludedDifferenceIds.Add(id);
                }
                else
                {
                    comparison.IncludedDifferenceIds.Remove(id);
                }
            }

            var rows = DacFxDifferenceMapper.BuildRows(comparison);
            return new ToggleIncludeResultViewModel
            {
                Rows = rows,
                Blocked = [],
                Stats = DacFxDifferenceMapper.BuildStats(rows)
            };
        });
    }

    public async Task<DiffDetailViewModel?> GetDetailAsync(string comparisonId, string differenceId, string sessionId)
    {
        var entry = _sessions.Get(comparisonId, sessionId);
        if (entry?.Comparison is null)
        {
            return null;
        }

        _sessions.Touch(entry);

        return await entry.LockedAsync(comparison =>
            comparison.Index.TryGetValue(differenceId, out var difference)
                ? DacFxDifferenceMapper.BuildDetail(differenceId, difference, comparison.Result, comparison.NameMapper)
                : null);
    }

    public async Task<ScriptViewModel?> GetScriptAsync(string comparisonId, string sessionId)
    {
        var entry = _sessions.Get(comparisonId, sessionId);
        if (entry?.Comparison is null)
        {
            return null;
        }

        _sessions.Touch(entry);

        return await entry.LockedAsync(comparison =>
        {
            var excludedObjectNames = GetExcludedObjectNames(comparison);
            var raw = comparison.Result.GenerateScript(comparison.TargetDatabase).Script ?? string.Empty;
            var processed = DacFxScriptPostProcessor.Process(
                raw,
                comparison.TargetDatabase,
                comparison.SourceLabel,
                comparison.TargetLabel,
                comparison.MappedSameObjectNames,
                excludedObjectNames);

            // The emitted module bodies still carry the source database's names, so map them
            // to the target's before the script is downloaded and run.
            var mappedScript = comparison.NameMapper.Apply(processed.Script);

            var rows = DacFxDifferenceMapper.BuildRows(comparison);
            var includedCount = rows.Count(x => x.Included);

            _logger.LogInformation(
                "Schema compare {ComparisonId}: generated script with {IncludedCount} included differences; suppressed {SuppressedSelectionBatchCount} batch(es) for unchecked objects and {SuppressedMappedModuleCount} mapping-only module batches",
                comparisonId,
                includedCount,
                processed.SuppressedSelectionBatchCount,
                processed.SuppressedMappedModuleCount);

            return new ScriptViewModel
            {
                Script = mappedScript,
                IncludedCount = includedCount,
                ExcludedCount = rows.Count - includedCount,
                Warnings = processed.Warnings
            };
        });
    }

    private async Task LogActivityAsync(SchemaCompareSessionEntry entry, DacFxComparePlan plan, string userId)
    {
        if (string.IsNullOrEmpty(userId) || entry.Comparison is null)
        {
            return;
        }

        // The background task outlives the request scope, so the activity log needs its own.
        using var scope = _scopeFactory.CreateScope();
        var activityLog = scope.ServiceProvider.GetRequiredService<IActivityLogService>();
        var changeCount = entry.Comparison.Result.Differences.Count();

        try
        {
            await activityLog.LogAsync(
                userId,
                ActivityActionType.Use,
                ActivityEntityType.SchemaCompare,
                null,
                $"Compared {plan.SourceLabel} with {plan.TargetLabel}: {changeCount} difference(s).");
        }
        catch (Exception ex)
        {
            // Never fail a finished comparison because history could not be written.
            _logger.LogWarning(ex, "Could not write schema compare activity log");
        }
    }

    private static IReadOnlySet<string> GetExcludedObjectNames(DacFxComparison comparison)
    {
        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var position = 0;
        foreach (var difference in comparison.Result.Differences)
        {
            var id = position.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (!comparison.IncludedDifferenceIds.Contains(id))
            {
                var identifier = difference.SourceObject?.Name ?? difference.TargetObject?.Name;
                if (identifier is not null && identifier.Parts.Count > 0)
                {
                    var parts = identifier.Parts;
                    excluded.Add(parts.Count == 1
                        ? parts[0]
                        : $"{parts[^2]}.{parts[^1]}");
                }
            }
            position++;
        }
        return excluded;
    }

    /// <summary>
    /// DacFx reports "Could not extract package from specified database" and buries the real
    /// cause in the inner exceptions, so the deepest one is appended. A timeout also gets a
    /// hint, because raising the limit in configuration is the actual fix.
    /// </summary>
    private string DescribeFailure(Exception exception)
    {
        var deepest = exception;
        while (deepest.InnerException is not null)
        {
            deepest = deepest.InnerException;
        }

        var message = SqlSchemaErrorHelper.Scrub(exception.Message);
        if (!ReferenceEquals(deepest, exception))
        {
            message += " — " + SqlSchemaErrorHelper.Scrub(deepest.Message);
        }

        if (LooksLikeTimeout(exception))
        {
            message += " " + _localizer["SchemaCompare.Error.ExtractTimeoutHint"].Value;
        }

        return message.Length > 800 ? message[..800] : message;
    }

    private static bool LooksLikeTimeout(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is System.ComponentModel.Win32Exception { NativeErrorCode: 258 }
                || current.Message.Contains("timed out", StringComparison.OrdinalIgnoreCase)
                || current.Message.Contains("Timeout Expired", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<ProgressLineViewModel> MapProgress(
        IReadOnlyList<ProgressEntry> entries,
        CompareSide side)
    {
        return entries
            .Where(x => x.Side == side)
            .Select(x => new ProgressLineViewModel
            {
                Time = x.At.ToString("HH:mm:ss"),
                Operation = x.Operation,
                State = x.State.ToString()
            })
            .ToArray();
    }

    private static string FormatElapsed(TimeSpan elapsed)
    {
        return elapsed.TotalHours >= 1
            ? elapsed.ToString(@"hh\:mm\:ss")
            : elapsed.ToString(@"mm\:ss");
    }
}
