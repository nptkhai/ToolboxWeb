using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ToolboxWeb.Web.Infrastructure.Hosting;

/// <summary>
/// Marks a controller (or action) that needs the app's database, so it does not exist on the web
/// copy (App:Mode=Web): <see cref="WebModeFilter"/> answers 404 there.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class NotInWebModeAttribute : Attribute
{
}

/// <summary>Global MVC filter: on the web copy, a controller or action marked [NotInWebMode] is not found.</summary>
public sealed class WebModeFilter : IActionFilter
{
    private readonly AppModeInfo _mode;

    public WebModeFilter(AppModeInfo mode)
    {
        _mode = mode;
    }

    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (_mode.IsWeb && context.ActionDescriptor.EndpointMetadata.OfType<NotInWebModeAttribute>().Any())
        {
            context.Result = new NotFoundResult();
        }
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }
}

/// <summary>
/// Web copy request rules for the Identity pages (Razor Pages, outside the MVC filter): only the
/// Jira sign-in exists, so registration and the account pages, which read the database, are not found.
/// </summary>
public static class WebModeRules
{
    private static readonly string[] HiddenPrefixes =
    [
        "/Identity/Account/Register",
        "/Identity/Account/Manage",
        "/Identity/Account/LoginWith2fa",
        "/Identity/Account/LoginWithRecoveryCode"
    ];

    public static void UseWeb(WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            var path = context.Request.Path;
            if (HiddenPrefixes.Any(prefix => path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            await next();
        });
    }
}
