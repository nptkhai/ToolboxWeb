using ToolkitLicense.Web.Infrastructure;

namespace ToolkitLicense.Web.Services;

/// <summary>How long a license runs, chosen when it is approved or re-issued.</summary>
public enum LicenseTerm
{
    /// <summary>No end date (the default); revoking still stops it.</summary>
    None = 0,
    SixMonths = 1,
    OneYear = 2,

    /// <summary>Until the end of a chosen day (App:TimeZone).</summary>
    Date = 3,

    /// <summary>Re-issue only: the same end date as the current license.</summary>
    Keep = 4
}

public static class LicenseTerms
{
    /// <summary>The end in UTC, or null for no end; <c>ok</c> is false when the chosen day is not after today.</summary>
    public static (DateTime? ExpiresAtUtc, bool Ok) Resolve(LicenseTerm term, DateTime? date, DateTime? currentExpiresAtUtc)
    {
        var today = Display.TodayLocal;
        return term switch
        {
            LicenseTerm.None => (null, true),
            LicenseTerm.SixMonths => (Display.EndOfLocalDayUtc(today.AddMonths(6)), true),
            LicenseTerm.OneYear => (Display.EndOfLocalDayUtc(today.AddYears(1)), true),
            LicenseTerm.Date when date is { } day && day.Date > today => (Display.EndOfLocalDayUtc(day), true),
            LicenseTerm.Date => (null, false),
            LicenseTerm.Keep when currentExpiresAtUtc is { } end && end <= DateTime.UtcNow => (null, false),
            LicenseTerm.Keep => (currentExpiresAtUtc, true),
            _ => (null, false)
        };
    }
}
