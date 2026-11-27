using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using ToolboxWeb.Web.Infrastructure.Hosting;
using ToolboxWeb.Web.Services;

namespace ToolboxWeb.Web.Infrastructure.Web;

/// <summary>
/// Kept screens: on a wide screen the app opens once as a shell (sidebar, header, a strip of open
/// screens) and every screen runs in its own frame inside it, so moving between screens does not
/// reload them (Content/js/screens.js, Content/js/screen-frame.js).
/// <list type="bullet">
/// <item>A page asked for by a frame of the shell (<c>Sec-Fetch-Dest: iframe</c> from the same
/// origin) is drawn without the sidebar and header (<see cref="IsFrameRequest"/>).</item>
/// <item>A page opened as the browser's own document (address bar, F5, a link from outside) is
/// answered with the shell instead of its content (<see cref="ScreenShellFilter"/>), unless the
/// site does not offer them (App:KeepScreens = false), the person turned them off (cookie
/// <see cref="KeepAliveCookie"/> = off), the window is
/// phone-narrow (cookie <see cref="NarrowCookie"/>, set by the layout), or the action says
/// [<see cref="NoScreenShellAttribute"/>].</item>
/// </list>
/// </summary>
public static class ScreenFrames
{
    public const string KeepAliveCookie = "toolbox.keepAlive";
    public const string NarrowCookie = "toolbox.narrow";
    private const string ShellItemKey = "toolbox.screenShell";

    /// <summary>The page is loaded by a frame of the shell (same origin), not as the browser's own document.</summary>
    public static bool IsFrameRequest(HttpRequest request) =>
        string.Equals(request.Headers["Sec-Fetch-Dest"], "iframe", StringComparison.OrdinalIgnoreCase)
        && string.Equals(request.Headers["Sec-Fetch-Site"], "same-origin", StringComparison.OrdinalIgnoreCase);

    /// <summary>This response is the shell (set by <see cref="ScreenShellFilter"/>).</summary>
    public static bool IsShell(HttpContext context) => context.Items.ContainsKey(ShellItemKey);

    internal static void MarkShell(HttpContext context) => context.Items[ShellItemKey] = true;

    /// <summary>The site offers kept screens (App:KeepScreens, on unless set to false).</summary>
    public static bool IsOffered(HttpContext context) =>
        context.RequestServices.GetRequiredService<IConfiguration>().GetValue("App:KeepScreens", true);

    /// <summary>Kept screens are on: the site offers them and this browser did not turn them off.</summary>
    public static bool IsEnabled(HttpContext context) =>
        IsOffered(context)
        && !string.Equals(context.Request.Cookies[KeepAliveCookie], "off", StringComparison.OrdinalIgnoreCase);
}

/// <summary>A page that is never wrapped in the screen shell (its own document: activation, update, run, offline…).</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class NoScreenShellAttribute : Attribute
{
}

/// <summary>
/// Global result filter: a full page opened as the browser's own document becomes the screen shell,
/// which then loads the same address in a frame. The action has run; only its view is not drawn.
/// </summary>
public sealed class ScreenShellFilter : IAsyncResultFilter
{
    private readonly IModelMetadataProvider _metadata;
    private readonly AppModeInfo _mode;
    private readonly IDeviceLicenseService _license;
    private readonly IDesktopUpdateService _updates;

    public ScreenShellFilter(IModelMetadataProvider metadata, AppModeInfo mode, IDeviceLicenseService license, IDesktopUpdateService updates)
    {
        _metadata = metadata;
        _mode = mode;
        _license = license;
        _updates = updates;
    }

    public Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (ShouldWrap(context) && context.Result is ViewResult view)
        {
            ScreenFrames.MarkShell(context.HttpContext);
            context.Result = new ViewResult
            {
                ViewName = "~/Views/Shared/ScreenShell.cshtml",
                ViewData = new ViewDataDictionary(_metadata, context.ModelState),
                TempData = view.TempData
            };
        }

        return next();
    }

    private bool ShouldWrap(ResultExecutingContext context)
    {
        var http = context.HttpContext;
        var request = http.Request;
        if (context.Result is not ViewResult view || (view.StatusCode is not null && view.StatusCode != StatusCodes.Status200OK))
        {
            return false;
        }

        if (!HttpMethods.IsGet(request.Method)
            || !string.Equals(request.Headers["Sec-Fetch-Dest"], "document", StringComparison.OrdinalIgnoreCase)
            || http.User.Identity?.IsAuthenticated != true
            || !ScreenFrames.IsEnabled(http)
            || request.Cookies[ScreenFrames.NarrowCookie] == "1"
            || context.ActionDescriptor.EndpointMetadata.OfType<NoScreenShellAttribute>().Any())
        {
            return false;
        }

        // Same rule as the layout: without a usable license, or while a required update waits,
        // only that page opens, without the app around it.
        if (_mode.IsDesktop)
        {
            var license = _license.GetStatus();
            if (license.IsBlocked || (license.UpdateRequired && _updates.IsInstalled))
            {
                return false;
            }
        }

        return true;
    }
}
