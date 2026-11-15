using Microsoft.AspNetCore.Identity;
using ToolkitLicense.Web.Domain;

namespace ToolkitLicense.Web.Infrastructure;

/// <summary>
/// Two-factor sign-in is required: an administrator who has not set up an authenticator app yet
/// can open nothing but the set-up page (and sign out) until it is done.
/// </summary>
public sealed class RequireTwoFactorMiddleware(RequestDelegate next)
{
    public const string SetupPath = "/account/two-factor";

    public async Task InvokeAsync(HttpContext context, UserManager<AdminUser> users)
    {
        if (context.User.Identity?.IsAuthenticated == true && !IsAllowed(context.Request.Path))
        {
            var user = await users.GetUserAsync(context.User);
            if (user is not null && !user.TwoFactorEnabled)
            {
                context.Response.Redirect(SetupPath);
                return;
            }
        }

        await next(context);
    }

    private static bool IsAllowed(PathString path) =>
        path.StartsWithSegments(SetupPath, StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/account/logout", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/account/recovery-codes", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/culture", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase);
}
