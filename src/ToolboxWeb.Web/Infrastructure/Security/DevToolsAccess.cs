using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ToolboxWeb.Web.Infrastructure.Security;

/// <summary>
/// Developer pages (component hub, Kendo and Tabulator demos) are a style guide for whoever
/// builds ToolboxWeb, not part of the product: they are open in Development, or to a user in
/// the <see cref="RoleName"/> role. ASP.NET Identity roles are not enabled yet, so outside
/// Development nobody passes until they are.
/// </summary>
public static class DevToolsAccess
{
    public const string RoleName = "Developer";

    public static bool IsAllowed(IWebHostEnvironment environment, ClaimsPrincipal user)
        => environment.IsDevelopment() || user.IsInRole(RoleName);
}

/// <summary>Answers 404 when <see cref="DevToolsAccess"/> denies, so the pages do not advertise themselves.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class DevToolsOnlyAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var environment = context.HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
        if (!DevToolsAccess.IsAllowed(environment, context.HttpContext.User))
        {
            context.Result = new NotFoundResult();
        }
    }
}
