using System.Globalization;

namespace ToolkitLicense.Web.Infrastructure;

/// <summary>Dates as the owner reads them: App:TimeZone (default Asia/Ho_Chi_Minh), dd/MM/yyyy HH:mm.</summary>
public static class Display
{
    private static TimeZoneInfo _zone = TimeZoneInfo.Utc;

    public static void Configure(string? timeZoneId)
    {
        try
        {
            _zone = TimeZoneInfo.FindSystemTimeZoneById(string.IsNullOrWhiteSpace(timeZoneId) ? "Asia/Ho_Chi_Minh" : timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            _zone = TimeZoneInfo.Utc;
        }
    }

    public static DateTime ToLocal(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _zone);

    public static DateTime TodayLocal => ToLocal(DateTime.UtcNow).Date;

    /// <summary>The end of a local day, in UTC (licenses run until the end of their last day).</summary>
    public static DateTime EndOfLocalDayUtc(DateTime localDate) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localDate.Date.AddDays(1).AddSeconds(-1), DateTimeKind.Unspecified), _zone);

    public static string DateTimeText(DateTime? utc) =>
        utc is null ? "—" : ToLocal(utc.Value).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    public static string DateText(DateTime? utc) =>
        utc is null ? "—" : ToLocal(utc.Value).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
}
