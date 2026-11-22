using ToolboxWeb.Web.Infrastructure.Files;

namespace ToolboxWeb.Web.Services.Files;

/// <summary>A folder that changed (relative to its root), or <see cref="All"/> when the watcher lost track.</summary>
public sealed record FileChange(int Root, string? Folder, bool All);

/// <summary>
/// Changes made outside Toolkit, as a feed the page reads every few seconds with a cursor
/// (GET /Files/Changes): a FileSystemWatcher per root (<see cref="FileWatchService"/>) appends
/// numbered entries; a root nobody asked about for two minutes stops being watched. Polling
/// instead of a held-open connection keeps IIS, proxies and page-load checks simple.
/// </summary>
public sealed class FileChangeFeed : IDisposable
{
    private const int Keep = 500;
    private static readonly TimeSpan Idle = TimeSpan.FromMinutes(2);

    private readonly FileWatchService _watch;
    private readonly Dictionary<string, Feed> _feeds = new(FilePathGuard.PathComparer);
    private readonly Timer _sweep;
    private long _sequence = 1; // cursor 0 means "first call"

    public FileChangeFeed(FileWatchService watch)
    {
        _watch = watch;
        _sweep = new Timer(_ => Sweep(), null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    /// <summary>
    /// Changes in the given roots after <paramref name="since"/> (0 on the first call: nothing,
    /// just the cursor), and the cursor to send next time.
    /// </summary>
    public (long Cursor, IReadOnlyList<FileChange> Changes) Read(IReadOnlyList<(int Id, string Path)> roots, long since)
    {
        var changes = new List<FileChange>();
        lock (_feeds)
        {
            foreach (var (id, path) in roots)
            {
                var stamp = Stamp(path);
                if (_feeds.TryGetValue(path, out var feed) && feed.Stamp != stamp)
                {
                    // The folder was deleted and made again (or swapped): the old watcher sees nothing now.
                    feed.Subscription?.Dispose();
                    _feeds.Remove(path);
                    feed = null;
                    if (since > 0)
                    {
                        changes.Add(new FileChange(id, null, true));
                    }
                }

                if (feed is null)
                {
                    var created = new Feed(path) { Stamp = stamp };
                    created.Subscription = _watch.Subscribe(path, changed => Add(created, changed));
                    _feeds[path] = created;
                    feed = created;
                }

                feed.LastRead = DateTime.UtcNow;
                if (since <= 0)
                {
                    continue;
                }

                if (feed.Dropped > since)
                {
                    changes.Add(new FileChange(id, null, true));
                    continue;
                }

                foreach (var (sequence, folder) in feed.Entries)
                {
                    if (sequence > since)
                    {
                        changes.Add(new FileChange(id, folder, folder is null));
                    }
                }
            }

            return (Interlocked.Read(ref _sequence), changes.Distinct().ToList());
        }
    }

    /// <summary>
    /// When the folder was created: a different value means it was deleted and made again. Only
    /// Windows keeps a real creation time (elsewhere .NET reports a time that moves with every change).
    /// </summary>
    private static DateTime Stamp(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            return default;
        }

        try
        {
            return Directory.GetCreationTimeUtc(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return default;
        }
    }

    private void Add(Feed feed, string? changedPath)
    {
        string? folder = null;
        if (changedPath is not null)
        {
            var parent = Path.GetDirectoryName(changedPath);
            if (parent is null || !FilePathGuard.IsInside(feed.Path, parent))
            {
                return;
            }

            // Our own temporary files (".~name.toolkit-tmp") change on every save; the save already updates the page.
            if (Path.GetFileName(changedPath).StartsWith(".~", StringComparison.Ordinal))
            {
                return;
            }

            folder = FilePathGuard.Relative(feed.Path, parent);
        }

        lock (_feeds)
        {
            var sequence = Interlocked.Increment(ref _sequence);
            feed.Entries.Enqueue((sequence, folder));
            while (feed.Entries.Count > Keep)
            {
                feed.Dropped = feed.Entries.Dequeue().Sequence;
            }
        }
    }

    private void Sweep()
    {
        lock (_feeds)
        {
            foreach (var key in _feeds.Where(x => DateTime.UtcNow - x.Value.LastRead > Idle).Select(x => x.Key).ToList())
            {
                _feeds[key].Subscription?.Dispose();
                _feeds.Remove(key);
            }
        }
    }

    public void Dispose()
    {
        _sweep.Dispose();
        lock (_feeds)
        {
            foreach (var feed in _feeds.Values)
            {
                feed.Subscription?.Dispose();
            }

            _feeds.Clear();
        }
    }

    private sealed class Feed(string path)
    {
        public string Path { get; } = path;
        public Queue<(long Sequence, string? Folder)> Entries { get; } = new();
        public long Dropped { get; set; }
        public DateTime Stamp { get; init; }
        public DateTime LastRead { get; set; } = DateTime.UtcNow;
        public IDisposable? Subscription { get; set; }
    }
}
