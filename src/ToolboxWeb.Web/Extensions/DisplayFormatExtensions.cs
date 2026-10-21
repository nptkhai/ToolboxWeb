using System.Globalization;
using ToolboxWeb.Web.Infrastructure.Time;

namespace ToolboxWeb.Web.Extensions;

/// <summary>
/// Dates, times and numbers as the user sees them, one format per UI culture
/// (docs/DESIGN_DECISIONS.md 2.I): vi-VN <c>dd/MM/yyyy</c>, <c>HH:mm</c>, <c>17,5</c>;
/// en-US <c>MM/dd/yyyy</c>, <c>h:mm tt</c>, <c>17.5</c>; hours as <c>17,5h</c>.
/// The client has the same rules in <c>Toolbox.format</c> (site.js).
/// </summary>
public static class DisplayFormatExtensions
{
    private static bool IsVietnamese => CultureInfo.CurrentCulture.TwoLetterISOLanguageName == "vi";

    public static string DatePattern => IsVietnamese ? "dd/MM/yyyy" : "MM/dd/yyyy";

    public static string TimePattern => IsVietnamese ? "HH:mm" : "h:mm tt";

    public static string ToDisplayDate(this DateTime value) => value.ToString(DatePattern, CultureInfo.CurrentCulture);

    public static string ToDisplayDate(this DateTime? value, string empty = "-") => value?.ToDisplayDate() ?? empty;

    public static string ToDisplayTime(this DateTime value) => value.ToString(TimePattern, CultureInfo.CurrentCulture);

    public static string ToDisplayDateTime(this DateTime value) => $"{value.ToDisplayDate()} {value.ToDisplayTime()}";

    /// <summary>A stored (UTC) time shown in the app time zone.</summary>
    public static string ToDisplayAppDateTime(this DateTime value) => value.ToAppTime().ToDisplayDateTime();

    public static string ToDisplayNumber(this double value, int maxDecimals = 2)
        => value.ToString(maxDecimals > 0 ? "#,0." + new string('#', maxDecimals) : "#,0", CultureInfo.CurrentCulture);

    public static string ToDisplayNumber(this decimal value, int maxDecimals = 2) => ((double)value).ToDisplayNumber(maxDecimals);

    /// <summary>Hours with up to two decimals and an "h" suffix, for example 17,5h.</summary>
    public static string ToDisplayHours(this double value) => value.ToDisplayNumber() + "h";
}
