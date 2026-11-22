using System.Collections.Concurrent;
using ToolboxWeb.Web.Infrastructure.Files;

namespace ToolboxWeb.Web.Services.Files;

/// <summary>
/// Notices files added, removed, renamed or changed outside Toolkit, for "Files &amp; folders":
/// one FileSystemWatcher per root folder, shared by every open page that shows it, created on the
/// first subscription and disposed with the last. Pages hear about it through GET /Files/Events
/// (Server-Sent Events); open files are also polled, since network shares may stay silent.
/// </summary>
public sealed class FileWatchService : IDisposable
{
    private readonly ConcurrentDictionary<string, Watch> _watches = new(FilePathGuard.PathComparer);
    private readonly ILogger<FileWatchService> _logger;

    public FileWatchService(ILogger<FileWatchService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Calls <paramref name="onChange"/> with the full path that changed (or null when the watcher
    /// lost track and the whole root should be read again). Dispose the result to stop.
    /// </summary>
    public IDisposable Subscribe(string rootPath, Action<string?> onChange)
    {
        var key = FilePathGuard.NormalizeRoot(rootPath);
        lock (_watches)
        {
            var watch = _watches.GetOrAdd(key, path => new Watch(path, _logger));
            lock (watch.Listeners)
            {
                watch.Listeners.Add(onChange);
            }

            return new Subscription(() =>
            {
                lock (_watches)
                {
                    bool empty;
                    lock (watch.Listeners)
                    {
                        watch.Listeners.Remove(onChange);
                        empty = watch.Listeners.Count == 0;
                    }

                    if (empty && _watches.TryRemove(key, out var removed))
                    {
                        removed.Dispose();
                    }
                }
            });
        }
    }

    public void Dispose()
    {
        foreach (var watch in _watches.Values)
        {
            watch.Dispose();
        }

        _watches.Clear();
    }

    private sealed class Watch : IDisposable
    {
        private readonly FileSystemWatcher? _watcher;

        public Watch(string path, ILogger logger)
        {
            try
            {
                _watcher = new FileSystemWatcher(path)
                {
                    IncludeSubdirectories = true,
                    InternalBufferSize = 64 * 1024,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size
                };
                _watcher.Created += (_, e) => Raise(e.FullPath);
                _watcher.Deleted += (_, e) => Raise(e.FullPath);
                _watcher.Changed += (_, e) => Raise(e.FullPath);
                _watcher.Renamed += (_, e) =>
                {
                    Raise(e.OldFullPath);
                    Raise(e.FullPath);
                };
                _watcher.Error += (_, _) => Raise(null);
                _watcher.EnableRaisingEvents = true;
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or PlatformNotSupportedException or UnauthorizedAccessException)
            {
                // Pages still poll open files; the tree refreshes with its button.
                logger.LogWarning(ex, "Cannot watch {Path} for changes.", path);
                _watcher?.Dispose();
                _watcher = null;
            }
        }

        public List<Action<string?>> Listeners { get; } = [];

        private void Raise(string? fullPath)
        {
            Action<string?>[] listeners;
            lock (Listeners)
            {
                listeners = Listeners.ToArray();
            }

            foreach (var listener in listeners)
            {
                listener(fullPath);
            }
        }

        public void Dispose() => _watcher?.Dispose();
    }

    private sealed class Subscription(Action dispose) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                dispose();
            }
        }
    }
}
