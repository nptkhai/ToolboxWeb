using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using ToolboxWeb.Web.Infrastructure.DacFx;
using ToolboxWeb.Web.Infrastructure.SqlSchema;

namespace ToolboxWeb.Web.Services;

/// <summary>
/// One live comparison belonging to one browser session.
/// <para>
/// The DacFx result has to stay in memory between requests: ticking a row calls
/// <c>Include</c>/<c>Exclude</c> on it, the detail panel reads scripts out of it, and the
/// script is generated from it. Re-comparing on every click would cost seconds each time.
/// </para>
/// </summary>
public sealed class SchemaCompareSessionEntry : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public required string ComparisonId { get; init; }
    public required string SessionId { get; init; }
    public required DacFxProgressCollector Progress { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.Now;
    public DateTime ExpiresAt { get; set; }

    /// <summary>Null until the background compare finishes.</summary>
    public DacFxComparison? Comparison { get; set; }

    public bool Completed { get; set; }
    public string? Error { get; set; }
    public long ElapsedMs { get; set; }

    /// <summary>
    /// Stops the background compare, either because the user pressed stop or because the
    /// overall deadline expired. Extraction runs in a child process so this token can terminate
    /// the worker when DacFx does not observe cooperative cancellation.
    /// </summary>
    public CancellationTokenSource Cancellation { get; } = new();

    /// <summary>True when a person stopped it, as opposed to the deadline expiring.</summary>
    public bool CancelledByUser { get; set; }

    /// <summary>DacFx result objects are not thread-safe; every mutation goes through this.</summary>
    public async Task<T> LockedAsync<T>(Func<DacFxComparison, T> action, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var comparison = Comparison
                ?? throw new InvalidOperationException("Comparison is not ready yet.");
            return action(comparison);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        // Cancel first: a comparison still extracting has to be told to stop before the
        // objects it is writing into go away.
        try
        {
            Cancellation.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        Comparison?.Dispose();
        Cancellation.Dispose();
        _gate.Dispose();
    }
}

public interface ISchemaCompareSessionStore
{
    /// <summary>
    /// Starts a new comparison for this browser session, replacing and disposing any previous
    /// one so a user can never hold two full schema models at once.
    /// </summary>
    SchemaCompareSessionEntry Start(string sessionId);

    SchemaCompareSessionEntry? Get(string comparisonId, string sessionId);

    void Touch(SchemaCompareSessionEntry entry);

    TimeSpan Ttl { get; }
}

public sealed class SchemaCompareSessionStore : ISchemaCompareSessionStore
{
    private readonly ConcurrentDictionary<string, SchemaCompareSessionEntry> _byComparisonId = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _activeBySession = new(StringComparer.Ordinal);
    private readonly ILogger<SchemaCompareSessionStore> _logger;

    public SchemaCompareSessionStore(IOptions<SqlSchemaOptions> options, ILogger<SchemaCompareSessionStore> logger)
    {
        Ttl = TimeSpan.FromMinutes(Math.Clamp(options.Value.ResultTtlMinutes, 1, 480));
        _logger = logger;
    }

    public TimeSpan Ttl { get; }

    public SchemaCompareSessionEntry Start(string sessionId)
    {
        PurgeExpired();

        if (_activeBySession.TryRemove(sessionId, out var previousId)
            && _byComparisonId.TryRemove(previousId, out var previous))
        {
            // Disposing cancels it. A second Compare click looks exactly like a deadline from
            // inside the engine, so say plainly which one it was.
            if (!previous.Completed)
            {
                _logger.LogWarning(
                    "Schema compare {ComparisonId} was still running and is being cancelled because the same browser session started a new comparison",
                    previous.ComparisonId);
            }

            previous.Dispose();
        }

        var entry = new SchemaCompareSessionEntry
        {
            ComparisonId = Guid.NewGuid().ToString("N"),
            SessionId = sessionId,
            Progress = new DacFxProgressCollector(),
            ExpiresAt = DateTime.Now.Add(Ttl)
        };

        _byComparisonId[entry.ComparisonId] = entry;
        _activeBySession[sessionId] = entry.ComparisonId;
        return entry;
    }

    public SchemaCompareSessionEntry? Get(string comparisonId, string sessionId)
    {
        if (string.IsNullOrEmpty(comparisonId) || !_byComparisonId.TryGetValue(comparisonId, out var entry))
        {
            return null;
        }

        if (!string.Equals(entry.SessionId, sessionId, StringComparison.Ordinal))
        {
            return null;
        }

        // The TTL governs how long a *finished* result stays available. A comparison that is
        // still running has its own deadline, and expiring it here would dispose it — which
        // cancels it, and looks from the engine exactly like the deadline expiring.
        if (entry.ExpiresAt <= DateTime.Now && entry.Completed)
        {
            Remove(entry);
            return null;
        }

        return entry;
    }

    public void Touch(SchemaCompareSessionEntry entry)
    {
        entry.ExpiresAt = DateTime.Now.Add(Ttl);
    }

    private void Remove(SchemaCompareSessionEntry entry)
    {
        _byComparisonId.TryRemove(entry.ComparisonId, out _);
        if (_activeBySession.TryGetValue(entry.SessionId, out var active) && active == entry.ComparisonId)
        {
            _activeBySession.TryRemove(entry.SessionId, out _);
        }

        entry.Dispose();
    }

    private void PurgeExpired()
    {
        var now = DateTime.Now;
        foreach (var pair in _byComparisonId)
        {
            if (pair.Value.ExpiresAt > now || !pair.Value.Completed)
            {
                continue;
            }

            _logger.LogInformation(
                "Releasing expired schema comparison {ComparisonId} created at {CreatedAt}",
                pair.Value.ComparisonId,
                pair.Value.CreatedAt);
            Remove(pair.Value);
        }
    }
}
