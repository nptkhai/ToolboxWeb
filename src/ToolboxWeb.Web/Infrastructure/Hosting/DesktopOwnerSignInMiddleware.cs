using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using ToolboxWeb.Web.Domain;
using ToolboxWeb.Web.Services;

namespace ToolboxWeb.Web.Infrastructure.Hosting;

/// <summary>
/// Desktop copies have no sign-in page: every request runs as the desktop owner. A request
/// without the owner's cookie (first start, after "delete my data", or an expired cookie) signs
/// the owner in and continues as the owner, so the page loads without a redirect.
/// </summary>
public sealed class DesktopOwnerSignInMiddleware
{
    private readonly RequestDelegate _next;

    public DesktopOwnerSignInMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IDeviceOwnerService owners, SignInManager<ApplicationUser> signIn)
    {
        var current = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (owners.OwnerId is null || current != owners.OwnerId)
        {
            var owner = await owners.GetOrCreateOwnerAsync(context.RequestAborted);
            var principal = await signIn.CreateUserPrincipalAsync(owner);
            await context.SignInAsync(IdentityConstants.ApplicationScheme, principal, new AuthenticationProperties { IsPersistent = true });
            context.User = principal;
        }

        await _next(context);
    }
}
