using System.Globalization;

namespace ToolboxWeb.Web.Extensions;

public static class DateTimeExtensions
{
    private static readonly TimeZoneInfo VietnamTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");

    public static DateTime ToVietnamTime(this DateTime dateTime)
    {
        var utcDateTime = dateTime.Kind switch
        {
            DateTimeKind.Utc => dateTime,
            DateTimeKind.Local => dateTime.ToUniversalTime(),
            _ => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
        };

        return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, VietnamTimeZone);
    }

    public static DateTime VietnamNow()
    {
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone);
    }

    public static DateTime VietnamToday()
    {
        return VietnamNow().Date;
    }

    public static string FormatVietnamDateTime(this DateTime dateTime, string format = "g")
    {
        return dateTime.ToVietnamTime().ToString(format, CultureInfo.CurrentCulture);
    }

    public static string FormatLocalDate(this DateTime dateTime, string format = "d")
    {
        return dateTime.ToString(format, CultureInfo.CurrentCulture);
    }
}
