using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using ToolkitLicense.Web.Services;

namespace ToolkitLicense.Web.Infrastructure;

/// <summary>One file in the updates folder.</summary>
public sealed record UpdateFileInfo(string Name, long Size, DateTime ModifiedAtUtc);

/// <summary>
/// The updates folder (Storage:DataRoot/updates): what tools/release/publish-desktop.ps1 makes with
/// Velopack — ToolkitApp-win-Setup.exe for first installs, the .nupkg packages and releases.win.json
/// that installed copies update from. Served at /updates; the owner uploads it on the Releases page
/// or copies it to the server.
/// </summary>
public sealed partial class UpdateFiles(LicenseStorage storage, IAuditLog audit, Data.LicenseDbContext db)
{
    public const string SetupFileName = "ToolkitApp-win-Setup.exe";
    public const string FeedFileName = "releases.win.json";

    public static StaticFileOptions Options(LicenseStorage storage)
    {
        var types = new FileExtensionContentTypeProvider();
        types.Mappings.Clear();
        types.Mappings[".nupkg"] = "application/octet-stream";
        types.Mappings[".json"] = "application/json";
        types.Mappings[".exe"] = "application/octet-stream";
        types.Mappings[".zip"] = "application/zip";
        return new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(storage.UpdatesPath),
            RequestPath = "/updates",
            ContentTypeProvider = types,
            ServeUnknownFileTypes = false,
            OnPrepareResponse = context =>
            {
                // The feed changes with every release; packages never change once published.
                context.Context.Response.Headers.CacheControl = context.File.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                    ? "no-cache"
                    : "public, max-age=86400";
            }
        };
    }

    public List<UpdateFileInfo> List() =>
        new DirectoryInfo(storage.UpdatesPath).EnumerateFiles()
            .Select(x => new UpdateFileInfo(x.Name, x.Length, x.LastWriteTimeUtc))
            .OrderByDescending(x => x.ModifiedAtUtc).ThenBy(x => x.Name)
            .ToList();

    public UpdateFileInfo? Setup =>
        File.Exists(Path.Combine(storage.UpdatesPath, SetupFileName))
            ? List().FirstOrDefault(x => x.Name == SetupFileName)
            : null;

    /// <summary>The newest version in releases.win.json, or null when there is no feed yet.</summary>
    public string? FeedVersion()
    {
        var path = Path.Combine(storage.UpdatesPath, FeedFileName);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            return document.RootElement.TryGetProperty("Assets", out var assets)
                ? assets.EnumerateArray()
                    .Select(x => x.TryGetProperty("Version", out var v) ? v.GetString() : null)
                    .Where(x => x is not null)
                    .OrderByDescending(x => x, Comparer<string?>.Create(ReleaseService.Compare))
                    .FirstOrDefault()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Saves uploaded Velopack output; other files are refused. Returns the names saved, or an error key.</summary>
    public async Task<(List<string> Saved, string? ErrorKey)> SaveAsync(IReadOnlyList<IFormFile> files)
    {
        if (files.Count == 0)
        {
            return ([], "Updates.Error.NoFiles");
        }

        if (files.Any(x => !AllowedName().IsMatch(Path.GetFileName(x.FileName))))
        {
            return ([], "Updates.Error.Name");
        }

        var saved = new List<string>();
        foreach (var file in files)
        {
            var name = Path.GetFileName(file.FileName);
            var target = Path.Combine(storage.UpdatesPath, name);
            var temporary = target + ".uploading";
            await using (var stream = File.Create(temporary))
            {
                await file.CopyToAsync(stream);
            }

            File.Move(temporary, target, overwrite: true);
            saved.Add(name);
        }

        audit.Add(AuditActions.UpdatesUploaded, string.Join(", ", saved));
        await db.SaveChangesAsync();
        return (saved, null);
    }

    // Velopack output only: packages, feeds, the installer, the portable zip and the legacy RELEASES file.
    [GeneratedRegex(@"^(RELEASES(-[A-Za-z0-9]+)?|[A-Za-z0-9][A-Za-z0-9._-]{0,150}\.(nupkg|json|exe|zip))$")]
    private static partial Regex AllowedName();
}
