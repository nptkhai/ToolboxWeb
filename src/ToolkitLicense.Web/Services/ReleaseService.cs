using Microsoft.EntityFrameworkCore;
using ToolkitLicense.Web.Data;
using ToolkitLicense.Web.Domain;

namespace ToolkitLicense.Web.Services;

public sealed record ReleaseInput(string? Version, string? Notes, bool Mandatory, string? DownloadUrl);

/// <summary>One version in use: how many devices report it.</summary>
public sealed record VersionUse(string Version, int Devices, bool IsLatest, bool Behind);

/// <summary>Published Toolkit versions and which version each device runs.</summary>
public sealed class ReleaseService(LicenseDbContext db, IAuditLog audit)
{
    public async Task<List<Release>> AllAsync() =>
        (await db.Releases.ToListAsync()).OrderByDescending(x => x.Version, Comparer<string>.Create(Compare)).ToList();

    public async Task<Release?> LatestAsync() => (await AllAsync()).FirstOrDefault();

    public async Task<List<VersionUse>> VersionsInUseAsync()
    {
        var latest = (await LatestAsync())?.Version;
        var counts = await db.Devices.GroupBy(x => x.AppVersion ?? string.Empty).Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
        return counts
            .OrderByDescending(x => x.Key, Comparer<string>.Create(Compare))
            .Select(x => new VersionUse(x.Key, x.Count, latest is not null && Compare(x.Key, latest) == 0, latest is not null && Compare(latest, x.Key) > 0))
            .ToList();
    }

    /// <returns>Null on success, else a localization key.</returns>
    public async Task<string?> CreateAsync(ReleaseInput input)
    {
        var version = (input.Version ?? string.Empty).Trim();
        if (Parse(version) is null)
        {
            return "Release.Error.Version";
        }

        if ((await db.Releases.Select(x => x.Version).ToListAsync()).Any(x => Compare(x, version) == 0))
        {
            return "Release.Error.Exists";
        }

        var url = string.IsNullOrWhiteSpace(input.DownloadUrl) ? null : input.DownloadUrl.Trim();
        if (url is not null && (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)))
        {
            return "Release.Error.Url";
        }

        db.Releases.Add(new Release
        {
            Version = version,
            Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : DeviceTokens.Clip(input.Notes, 2000),
            Mandatory = input.Mandatory,
            DownloadUrl = url
        });
        audit.Add(AuditActions.ReleaseCreated, version + (input.Mandatory ? " (mandatory)" : string.Empty));
        await db.SaveChangesAsync();
        return null;
    }

    public async Task<string?> DeleteAsync(int id)
    {
        var release = await db.Releases.FindAsync(id);
        if (release is null)
        {
            return "Release.Error.NotFound";
        }

        db.Releases.Remove(release);
        audit.Add(AuditActions.ReleaseDeleted, release.Version);
        await db.SaveChangesAsync();
        return null;
    }

    /// <summary>Compares "1.2.3" style versions; text that is not a version sorts below every version.</summary>
    public static int Compare(string? a, string? b)
    {
        var x = Parse(a);
        var y = Parse(b);
        if (x is null || y is null)
        {
            return (x is null ? 0 : 1) - (y is null ? 0 : 1);
        }

        return x.CompareTo(y);
    }

    private static Version? Parse(string? text)
    {
        var core = (text ?? string.Empty).Trim().Split('-', '+')[0];
        if (!Version.TryParse(core, out var version) || core.Split('.').Length < 2)
        {
            return null;
        }

        // 1.2 and 1.2.0 are the same release.
        return new Version(version.Major, version.Minor, Math.Max(0, version.Build), Math.Max(0, version.Revision));
    }
}
