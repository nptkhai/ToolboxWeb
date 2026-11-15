using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Localization;
using ToolboxWeb.Web.Constants;
using ToolboxWeb.Web.Services;

namespace ToolboxWeb.Web.Infrastructure.Hosting;

/// <summary>
/// Marks a POST (or a whole controller / page) that still runs when the desktop's license has
/// ended (read-only): it either changes no app data (a Jira search, a schema comparison, the
/// language) or it is how the license is renewed. Every other write is refused while read-only.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AllowWhenUnlicensedAttribute : Attribute
{
}

/// <summary>
/// Desktop only, after the owner is signed in: applies the license (DeviceLicenseService).
/// <list type="bullet">
/// <item>No usable license: every page goes to the activation screen.</item>
/// <item>Expired or revoked: pages open, but writes are refused unless marked [AllowWhenUnlicensed].</item>
/// </list>
/// </summary>
public sealed class DesktopLicenseMiddleware(RequestDelegate next)
{
    // Open even without a license: activation itself, quitting the app, its version and offline
    // pages, and the language switch.
    private static readonly string[] OpenPrefixes =
    [
        "/" + ToolboxRouteSlugs.HtmlPages.Activate,
        "/activate",
        "/desktop",
        "/app",
        "/Culture",
        "/offline.html",
        "/sw.js",
        "/Home/Error",
        "/error.html"
    ];

    public async Task InvokeAsync(HttpContext context, IDeviceLicenseService licenses, ITempDataDictionaryFactory tempData, IStringLocalizer<SharedResource> text)
    {
        var status = licenses.GetStatus();
        if (status.State is DeviceLicenseState.Off or DeviceLicenseState.Active || IsOpen(context.Request.Path))
        {
            await next(context);
            return;
        }

        var reading = HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method);
        if (status.IsBlocked)
        {
            if (reading && !WantsJson(context.Request))
            {
                context.Response.Redirect("/" + ToolboxRouteSlugs.HtmlPages.Activate);
                return;
            }

            await RefuseAsync(context, tempData, text["License.Blocked.NotActivated"].Value, reading);
            return;
        }

        // Read-only (expired or revoked).
        if (reading || context.GetEndpoint()?.Metadata.GetMetadata<AllowWhenUnlicensedAttribute>() is not null)
        {
            await next(context);
            return;
        }

        await RefuseAsync(context, tempData, text[status.State == DeviceLicenseState.Revoked ? "License.Blocked.Revoked" : "License.Blocked.Expired"].Value, reading);
    }

    private static async Task RefuseAsync(HttpContext context, ITempDataDictionaryFactory tempData, string message, bool reading)
    {
        if (WantsJson(context.Request) || reading)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { message, error = message, license = true });
            return;
        }

        // A plain form: back to the page it came from, with the reason.
        var data = tempData.GetTempData(context);
        data[TempDataKeys.ErrorMessage] = message;
        data.Save();
        var referer = context.Request.Headers.Referer.ToString();
        var back = Uri.TryCreate(referer, UriKind.Absolute, out var uri) && string.Equals(uri.Host, context.Request.Host.Host, StringComparison.OrdinalIgnoreCase)
            ? uri.PathAndQuery
            : "/";
        context.Response.Redirect(back);
    }

    private static bool IsOpen(PathString path) =>
        OpenPrefixes.Any(prefix => path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase));

    private static bool WantsJson(HttpRequest request) =>
        string.Equals(request.Headers.XRequestedWith, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase)
        || request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase)
        || (request.ContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) ?? false);
}
