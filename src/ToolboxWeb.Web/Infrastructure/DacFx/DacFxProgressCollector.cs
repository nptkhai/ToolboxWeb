using System.Collections.Concurrent;
using Microsoft.SqlServer.Dac;

namespace ToolboxWeb.Web.Infrastructure.DacFx;

public enum CompareSide
{
    Source,
    Target
}

public enum ProgressState
{
    Running,
    Completed,
    Failed
}

public sealed record ProgressEntry(
    CompareSide Side,
    string Operation,
    ProgressState State,
    DateTime At);

/// <summary>
/// Collects the progress DacFx reports while it extracts a database, so the UI can show a
/// running log per side.
/// <para>
/// <see cref="Microsoft.SqlServer.Dac.Compare.SchemaComparison"/> itself reports nothing:
/// <c>Compare()</c> is a single blocking call. Extracting each side to a package first is
/// the only way to get progress, and it also makes every later re-compare far cheaper
/// because the databases are no longer touched.
/// </para>
/// </summary>
public sealed class DacFxProgressCollector
{
    private readonly ConcurrentQueue<ProgressEntry> _entries = new();

    public IReadOnlyList<ProgressEntry> Snapshot() => _entries.ToArray();

    public void Add(CompareSide side, string operation, ProgressState state)
    {
        _entries.Enqueue(new ProgressEntry(side, operation, state, DateTime.Now));
    }

    /// <summary>
    /// Subscribes to a <see cref="DacServices"/> instance. DacFx raises these on background
    /// threads, hence the concurrent queue.
    /// </summary>
    public void Attach(DacServices services, CompareSide side)
    {
        services.ProgressChanged += (_, e) => Add(side, e.Message, Map(e.Status));

        services.Message += (_, e) =>
        {
            if (e.Message.MessageType == DacMessageType.Message)
            {
                // "Gathering users", "Gathering procedures", ... one per object category.
                Add(side, e.Message.Message, ProgressState.Completed);
            }
            else
            {
                Add(side, e.Message.Message, ProgressState.Failed);
            }
        };
    }

    private static ProgressState Map(DacOperationStatus status) => status switch
    {
        DacOperationStatus.Completed => ProgressState.Completed,
        DacOperationStatus.Faulted or DacOperationStatus.Cancelled => ProgressState.Failed,
        _ => ProgressState.Running
    };
}
