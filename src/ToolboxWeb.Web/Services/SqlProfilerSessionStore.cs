using System.Collections.Concurrent;
using ToolboxWeb.Web.Infrastructure.SqlProfiler;

namespace ToolboxWeb.Web.Services;

/// <summary>
/// One running capture. Holds the events gathered so far, keyed so that re-reading the ring
/// buffer does not duplicate them.
/// </summary>
public sealed class ProfilerCaptureEntry : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, RawProfilerEvent> _events = new(StringComparer.Ordinal);

    public required string CaptureId { get; init; }
    public required string SessionId { get; init; }
    public required string SessionName { get; init; }
    public required string ConnectionString { get; init; }
    public required string Database { get; init; }
    public required string ServerLabel { get; init; }

    public DateTime StartedAt { get; init; } = DateTime.Now;
    public DateTime AutoStopAt { get; init; }
    public bool Running { get; set; } = true;
    public string? Error { get; set; }
    public int DroppedCount { get; set; }

    public bool IsExpired => DateTime.Now >= AutoStopAt;

    public async Task<T> LockedAsync<T>(Func<Dictionary<string, RawProfilerEvent>, T> action)
    {
        await _gate.WaitAsync();
        try
        {
            return action(_events);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();
}

public interface ISqlProfilerSessionStore
{
    ProfilerCaptureEntry Start(string browserSessionId, ProfilerCaptureEntry entry);

    ProfilerCaptureEntry? Get(string captureId, string browserSessionId);

    ProfilerCaptureEntry? GetActive(string browserSessionId);

    void Remove(ProfilerCaptureEntry entry);

    /// <summary>Session names still in use, so the orphan sweep does not drop a live capture.</summary>
    IReadOnlyCollection<string> ActiveSessionNames();

    IReadOnlyList<ProfilerCaptureEntry> ExpiredEntries();
}

public sealed class SqlProfilerSessionStore : ISqlProfilerSessionStore
{
    private readonly ConcurrentDictionary<string, ProfilerCaptureEntry> _byCaptureId = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _activeByBrowser = new(StringComparer.Ordinal);

    public ProfilerCaptureEntry Start(string browserSessionId, ProfilerCaptureEntry entry)
    {
        // One live capture per browser session: two event sessions per user would double the
        // load on the server for no benefit.
        if (_activeByBrowser.TryGetValue(browserSessionId, out var previousId)
            && _byCaptureId.TryGetValue(previousId, out var previous))
        {
            previous.Running = false;
        }

        _byCaptureId[entry.CaptureId] = entry;
        _activeByBrowser[browserSessionId] = entry.CaptureId;
        return entry;
    }

    public ProfilerCaptureEntry? Get(string captureId, string browserSessionId)
    {
        if (string.IsNullOrEmpty(captureId) || !_byCaptureId.TryGetValue(captureId, out var entry))
        {
            return null;
        }

        return string.Equals(entry.SessionId, browserSessionId, StringComparison.Ordinal) ? entry : null;
    }

    public ProfilerCaptureEntry? GetActive(string browserSessionId)
    {
        return _activeByBrowser.TryGetValue(browserSessionId, out var captureId)
            ? Get(captureId, browserSessionId)
            : null;
    }

    public void Remove(ProfilerCaptureEntry entry)
    {
        _byCaptureId.TryRemove(entry.CaptureId, out _);
        if (_activeByBrowser.TryGetValue(entry.SessionId, out var active) && active == entry.CaptureId)
        {
            _activeByBrowser.TryRemove(entry.SessionId, out _);
        }

        entry.Dispose();
    }

    public IReadOnlyCollection<string> ActiveSessionNames() =>
        _byCaptureId.Values.Where(x => x.Running).Select(x => x.SessionName).ToHashSet(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<ProfilerCaptureEntry> ExpiredEntries() =>
        _byCaptureId.Values.Where(x => x.Running && x.IsExpired).ToArray();
}
