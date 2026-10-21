namespace ToolboxWeb.Web.Infrastructure.Time;

/// <summary>
/// The one time zone the app shows dates in (setting <c>App:TimeZone</c>, IANA id, default
/// Asia/Ho_Chi_Minh). The server converts with it and the client clock receives the same id
/// through <c>data-time-zone</c> on &lt;html&gt;, so both sides agree on "today"
/// (docs/DESIGN_DECISIONS.md 2.I). Configured once at startup.
/// </summary>
public static class AppClock
{
    public const string DefaultTimeZoneId = "Asia/Ho_Chi_Minh";

    public static string TimeZoneId { get; private set; } = DefaultTimeZoneId;

    public static TimeZoneInfo TimeZone { get; private set; } = Resolve(DefaultTimeZoneId);

    public static void Configure(string? timeZoneId)
    {
        var id = string.IsNullOrWhiteSpace(timeZoneId) ? DefaultTimeZoneId : timeZoneId.Trim();
        TimeZone = Resolve(id);
        TimeZoneId = id;
    }

    /// <summary>Current wall-clock time in the app time zone.</summary>
    public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZone);

    public static DateTime Today => Now.Date;

    /// <summary>Converts a stored time to the app time zone. Unspecified kinds are treated as UTC.</summary>
    public static DateTime ToAppTime(this DateTime value)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

        return TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZone);
    }

    // IANA ids work on Linux and on Windows with ICU; older Windows needs the Windows id.
    private static TimeZoneInfo Resolve(string id)
    {
        if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone))
        {
            return zone;
        }

        if (TimeZoneInfo.TryConvertIanaIdToWindowsId(id, out var windowsId)
            && TimeZoneInfo.TryFindSystemTimeZoneById(windowsId, out zone))
        {
            return zone;
        }

        throw new InvalidOperationException($"App:TimeZone \"{id}\" is not a known time zone. Use an IANA id such as {DefaultTimeZoneId}.");
    }
}
