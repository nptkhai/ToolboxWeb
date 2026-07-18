using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using ToolboxWeb.Web.Constants;
using ToolboxWeb.Web.Domain;

namespace ToolboxWeb.Web.Services;

public class ToolboxUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser>
{
    public ToolboxUserClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager,
        IOptions<IdentityOptions> optionsAccessor)
        : base(userManager, optionsAccessor)
    {
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        var authSource = string.IsNullOrWhiteSpace(user.AuthSource) ? AuthSources.Local : user.AuthSource;
        var displayName = string.Equals(authSource, AuthSources.Jira, StringComparison.OrdinalIgnoreCase)
            ? user.JiraDisplayName ?? user.JiraUsername ?? user.UserName ?? string.Empty
            : user.Email ?? user.UserName ?? string.Empty;

        identity.AddClaim(new Claim(ToolboxClaimTypes.AuthSource, authSource));

        if (!string.IsNullOrWhiteSpace(displayName))
        {
            identity.AddClaim(new Claim(ToolboxClaimTypes.DisplayName, displayName));
        }

        if (!string.IsNullOrWhiteSpace(user.JiraBaseUrl))
        {
            identity.AddClaim(new Claim(ToolboxClaimTypes.JiraBaseUrl, user.JiraBaseUrl));
        }

        if (!string.IsNullOrWhiteSpace(user.JiraUsername))
        {
            identity.AddClaim(new Claim(ToolboxClaimTypes.JiraUsername, user.JiraUsername));
        }

        return identity;
    }
}
