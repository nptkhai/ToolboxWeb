using System.Globalization;
using System.Xml.Linq;
using ToolboxWeb.Web.ViewModels.SqlProfiler;

namespace ToolboxWeb.Web.Infrastructure.SqlProfiler;

/// <summary>One event exactly as it came out of the ring buffer, before merging.</summary>
public sealed record RawProfilerEvent(
    string EventName,
    DateTime UtcTime,
    long DurationMicroseconds,
    long CpuMicroseconds,
    long LogicalReads,
    long RowCount,
    string ObjectName,
    string ObjectType,
    string UserName,
    string ClientHost,
    string AppName,
    string DatabaseName,
    int SessionId,
    string Statement,
    string Message)
{
    /// <summary>Identity used to avoid re-adding events that are still in the ring buffer.</summary>
    public string Key => string.Create(
        CultureInfo.InvariantCulture,
        $"{EventName}|{UtcTime.Ticks}|{SessionId}|{ObjectName}|{DurationMicroseconds}|{Message.Length}");
}

public sealed record RingBufferSnapshot(IReadOnlyList<RawProfilerEvent> Events, int DroppedCount);

/// <summary>
/// Parses the XML the ring buffer target returns.
/// <para>
/// The ring buffer hands back every event it still holds on each read, so the caller keeps a
/// running set keyed by <see cref="RawProfilerEvent.Key"/> and only adds what it has not seen.
/// That way the grid keeps history even after old events age out of the buffer.
/// </para>
/// </summary>
public static class XEventRingBufferReader
{
    public static RingBufferSnapshot Parse(string? targetData)
    {
        if (string.IsNullOrWhiteSpace(targetData))
        {
            return new RingBufferSnapshot([], 0);
        }

        XDocument document;
        try
        {
            document = XDocument.Parse(targetData);
        }
        catch (System.Xml.XmlException)
        {
            // The ring buffer truncates its XML once it fills, which can cut a tag in half.
            return new RingBufferSnapshot([], 0);
        }

        var root = document.Root;
        if (root is null)
        {
            return new RingBufferSnapshot([], 0);
        }

        var dropped = ReadInt(root.Attribute("droppedCount"))
            + ReadInt(root.Attribute("eventsDropped"))
            + ReadInt(root.Attribute("truncated"));

        var events = root.Elements("event").Select(ParseEvent).ToArray();
        return new RingBufferSnapshot(events, dropped);
    }

    private static RawProfilerEvent ParseEvent(XElement element)
    {
        return new RawProfilerEvent(
            EventName: element.Attribute("name")?.Value ?? string.Empty,
            UtcTime: ParseTimestamp(element.Attribute("timestamp")?.Value),
            DurationMicroseconds: DataLong(element, "duration"),
            CpuMicroseconds: DataLong(element, "cpu_time"),
            LogicalReads: DataLong(element, "logical_reads"),
            RowCount: DataLong(element, "row_count"),
            ObjectName: DataText(element, "object_name"),
            // SQL Server pads this to two characters: "P ", "TR", "FN".
            ObjectType: DataText(element, "object_type").Trim(),
            UserName: ActionText(element, "username"),
            ClientHost: ActionText(element, "client_hostname"),
            AppName: ActionText(element, "client_app_name"),
            DatabaseName: ActionText(element, "database_name"),
            SessionId: (int)ActionLong(element, "session_id"),
            Statement: FirstNonEmpty(DataText(element, "statement"), ActionText(element, "sql_text")),
            Message: DataText(element, "message"));
    }

    private static DateTime ParseTimestamp(string? value)
    {
        return DateTime.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
            out var parsed)
            ? DateTime.SpecifyKind(parsed, DateTimeKind.Utc)
            : DateTime.UtcNow;
    }

    private static string DataText(XElement element, string name) =>
        element.Elements("data")
            .FirstOrDefault(x => x.Attribute("name")?.Value == name)
            ?.Element("value")?.Value ?? string.Empty;

    private static long DataLong(XElement element, string name) =>
        long.TryParse(DataText(element, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;

    private static string ActionText(XElement element, string name) =>
        element.Elements("action")
            .FirstOrDefault(x => x.Attribute("name")?.Value == name)
            ?.Element("value")?.Value ?? string.Empty;

    private static long ActionLong(XElement element, string name) =>
        long.TryParse(ActionText(element, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;

    private static int ReadInt(XAttribute? attribute) =>
        attribute is not null && int.TryParse(attribute.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;
}
