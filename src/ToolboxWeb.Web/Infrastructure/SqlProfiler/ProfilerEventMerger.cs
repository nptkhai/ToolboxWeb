using System.Globalization;
using ToolboxWeb.Web.ViewModels.SqlProfiler;

namespace ToolboxWeb.Web.Infrastructure.SqlProfiler;

/// <summary>
/// Turns raw events into the rows the grid shows.
/// <para>
/// Every procedure call raises <c>rpc_completed</c> <em>and</em> <c>module_end</c>, which
/// would show each call twice. They are merged into one row: the statement and resource
/// figures come from rpc_completed, the object type from module_end. A module_end with no
/// matching rpc_completed is a trigger, a function, or a procedure invoked inside a batch,
/// and stays as its own row.
/// </para>
/// <para>
/// An <c>error_reported</c> is attached to the call it happened inside instead of becoming a
/// separate row, so one failed call reads as one red line rather than two.
/// </para>
/// </summary>
public static class ProfilerEventMerger
{
    /// <summary>rpc_completed and module_end for one call land within a few ms of each other.</summary>
    private static readonly TimeSpan PairWindow = TimeSpan.FromMilliseconds(150);

    /// <summary>An error surfaces slightly before the call that contained it completes.</summary>
    private static readonly TimeSpan ErrorWindow = TimeSpan.FromMilliseconds(250);

    public static IReadOnlyList<ProfilerEventViewModel> Merge(
        IEnumerable<RawProfilerEvent> rawEvents,
        bool maskParameters)
    {
        var ordered = rawEvents.OrderBy(x => x.UtcTime).ToList();

        var rpcEvents = ordered.Where(x => x.EventName == "rpc_completed").ToList();
        var moduleEvents = ordered.Where(x => x.EventName == "module_end").ToList();
        var errorEvents = ordered.Where(x => x.EventName == "error_reported").ToList();

        var usedModules = new HashSet<RawProfilerEvent>();
        var rows = new List<(RawProfilerEvent Source, ProfilerEventViewModel Row)>();

        foreach (var rpc in rpcEvents)
        {
            var objectName = ProfilerEventParser.ExtractObjectName(rpc.Statement, rpc.ObjectName);

            var pairedModule = moduleEvents.FirstOrDefault(module =>
                !usedModules.Contains(module)
                && module.SessionId == rpc.SessionId
                && NamesMatch(module.ObjectName, objectName)
                && Within(module.UtcTime, rpc.UtcTime, PairWindow));

            if (pairedModule is not null)
            {
                usedModules.Add(pairedModule);
            }

            rows.Add((rpc, Build(rpc, objectName, pairedModule?.ObjectType ?? "P", maskParameters)));
        }

        foreach (var module in moduleEvents.Where(x => !usedModules.Contains(x)))
        {
            rows.Add((module, Build(module, module.ObjectName, module.ObjectType, maskParameters)));
        }

        AttachErrors(rows, errorEvents, maskParameters);

        return rows
            .Select(x => x.Row)
            .OrderByDescending(x => x.DurationMs)
            .ThenByDescending(x => x.At)
            .ToArray();
    }

    private static void AttachErrors(
        List<(RawProfilerEvent Source, ProfilerEventViewModel Row)> rows,
        List<RawProfilerEvent> errorEvents,
        bool maskParameters)
    {
        foreach (var error in errorEvents)
        {
            // An event's timestamp is when the call *finished*, and the error happened
            // inside it, so the owner is the next call to complete on the same session.
            // Picking merely the first row inside the window attaches the error to whichever
            // call happens to come first in the list, which is usually the wrong one.
            var index = -1;
            var bestGap = TimeSpan.MaxValue;

            for (var i = 0; i < rows.Count; i++)
            {
                var candidate = rows[i];
                if (candidate.Source.SessionId != error.SessionId || candidate.Row.HasError)
                {
                    continue;
                }

                var gap = candidate.Source.UtcTime - error.UtcTime;
                if (gap < TimeSpan.Zero || gap > ErrorWindow || gap >= bestGap)
                {
                    continue;
                }

                bestGap = gap;
                index = i;
            }

            if (index >= 0)
            {
                var existing = rows[index];
                rows[index] = (existing.Source, WithError(existing.Row, error.Message));
                continue;
            }

            // Nothing to attach it to: show the error on its own.
            rows.Add((error, new ProfilerEventViewModel
            {
                Id = BuildId(error),
                At = error.UtcTime.ToLocalTime(),
                Time = error.UtcTime.ToLocalTime().ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture),
                Kind = "Error",
                ObjectName = string.IsNullOrWhiteSpace(error.ObjectName) ? "(SQL)" : error.ObjectName,
                Database = error.DatabaseName,
                UserName = error.UserName,
                ClientHost = error.ClientHost,
                AppName = error.AppName,
                SessionId = error.SessionId,
                HasError = true,
                ErrorMessage = error.Message,
                Statement = maskParameters ? string.Empty : error.Statement
            }));
        }
    }

    private static ProfilerEventViewModel Build(
        RawProfilerEvent raw,
        string objectName,
        string objectType,
        bool maskParameters)
    {
        var parameters = ProfilerEventParser.ExtractParameters(raw.Statement);
        var statement = maskParameters
            ? ProfilerEventParser.MaskStatement(raw.Statement, parameters)
            : raw.Statement;

        return new ProfilerEventViewModel
        {
            Id = BuildId(raw),
            At = raw.UtcTime.ToLocalTime(),
            Time = raw.UtcTime.ToLocalTime().ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture),
            Kind = DescribeKind(objectType),
            ObjectName = objectName,
            Database = raw.DatabaseName,
            UserName = raw.UserName,
            ClientHost = raw.ClientHost,
            AppName = raw.AppName,
            SessionId = raw.SessionId,
            DurationMs = Math.Round(raw.DurationMicroseconds / 1000d, 1),
            CpuMs = Math.Round(raw.CpuMicroseconds / 1000d, 1),
            LogicalReads = raw.LogicalReads,
            RowCount = raw.RowCount,
            Statement = statement,
            Parameters = maskParameters
                ? parameters.Select(x => new ProfilerParameterViewModel { Name = x.Name, Kind = x.Kind, Value = "***" }).ToArray()
                : parameters
        };
    }

    private static ProfilerEventViewModel WithError(ProfilerEventViewModel row, string message) => new()
    {
        Id = row.Id,
        At = row.At,
        Time = row.Time,
        Kind = row.Kind,
        ObjectName = row.ObjectName,
        Database = row.Database,
        UserName = row.UserName,
        ClientHost = row.ClientHost,
        AppName = row.AppName,
        SessionId = row.SessionId,
        DurationMs = row.DurationMs,
        CpuMs = row.CpuMs,
        LogicalReads = row.LogicalReads,
        RowCount = row.RowCount,
        Statement = row.Statement,
        Parameters = row.Parameters,
        HasError = true,
        ErrorMessage = message
    };

    public static ProfilerSummaryViewModel Summarise(IReadOnlyList<ProfilerEventViewModel> rows)
    {
        var calls = rows.Where(x => x.Kind != "Error").ToList();
        var slowest = calls.MaxBy(x => x.DurationMs);

        return new ProfilerSummaryViewModel
        {
            TotalCalls = calls.Count,
            TotalDurationMs = Math.Round(calls.Sum(x => x.DurationMs), 1),
            SlowestMs = slowest?.DurationMs ?? 0,
            SlowestObject = slowest?.ObjectName ?? string.Empty,
            ErrorCount = rows.Count(x => x.HasError),
            DistinctObjects = calls.Select(x => x.ObjectName).Distinct(StringComparer.OrdinalIgnoreCase).Count()
        };
    }

    private static string DescribeKind(string objectType) => objectType.Trim().ToUpperInvariant() switch
    {
        "P" or "PC" => "Procedure",
        "TR" => "Trigger",
        "FN" or "IF" or "TF" or "AF" => "Function",
        _ => "Procedure"
    };

    private static bool NamesMatch(string left, string right)
    {
        // module_end reports the bare name; the statement carries "dbo.Name".
        return string.Equals(StripSchema(left), StripSchema(right), StringComparison.OrdinalIgnoreCase);
    }

    private static string StripSchema(string name)
    {
        var cleaned = name.Replace("[", string.Empty, StringComparison.Ordinal)
            .Replace("]", string.Empty, StringComparison.Ordinal);
        var dot = cleaned.LastIndexOf('.');
        return dot >= 0 ? cleaned[(dot + 1)..] : cleaned;
    }

    private static bool Within(DateTime left, DateTime right, TimeSpan window) =>
        (left - right).Duration() <= window;

    private static string BuildId(RawProfilerEvent raw) =>
        Math.Abs(raw.Key.GetHashCode(StringComparison.Ordinal)).ToString(CultureInfo.InvariantCulture);
}
