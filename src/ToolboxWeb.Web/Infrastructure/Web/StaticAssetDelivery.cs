using System.IO.Compression;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.StaticFiles;

namespace ToolboxWeb.Web.Infrastructure.Web;

/// <summary>
/// How the files under Content/ reach the browser: compressed, and cached so a page does not
/// fetch Bootstrap, Kendo or Tabulator again on every visit.
/// </summary>
public static class StaticAssetDelivery
{
    /// <summary>
    /// Only static text files are compressed. HTML pages and JSON answers are left alone: they can
    /// carry the antiforgery token or user data next to text an attacker controls (BREACH).
    /// </summary>
    private static readonly string[] CompressedTypes =
    [
        "text/css",
        "text/javascript",
        "application/javascript",
        "image/svg+xml"
    ];

    // A versioned link (asp-append-version adds ?v=<hash>) changes whenever the file changes,
    // so the file can be kept for a year. Libraries linked without a version (lazy Kendo, the
    // Inter font files) are kept a week: an upgrade reaches everyone within a week.
    private const string VersionedCache = "public,max-age=31536000,immutable";
    private const string LibraryCache = "public,max-age=604800";

    public static IServiceCollection AddStaticAssetCompression(this IServiceCollection services)
    {
        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.MimeTypes = CompressedTypes;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
        });
        // Fastest: files are compressed on each request (then cached by the browser), and
        // Brotli's best level costs far more CPU for a few percent.
        services.Configure<BrotliCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);
        services.Configure<GzipCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);
        return services;
    }

    public static void SetCacheHeaders(StaticFileResponseContext context)
    {
        var request = context.Context.Request;
        if (request.Query.ContainsKey("v"))
        {
            context.Context.Response.Headers.CacheControl = VersionedCache;
        }
        else if (request.Path.StartsWithSegments("/vendor") || request.Path.StartsWithSegments("/lib"))
        {
            context.Context.Response.Headers.CacheControl = LibraryCache;
        }
    }
}
