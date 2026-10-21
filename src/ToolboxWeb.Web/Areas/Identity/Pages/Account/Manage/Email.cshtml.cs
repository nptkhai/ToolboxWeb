using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ToolboxWeb.Web.Domain;
using ToolboxWeb.Web.Services;

namespace ToolboxWeb.Web.Areas.Identity.Pages.Account.Manage;

/// <summary>
/// Shows the sign-in email. Changing it needs a confirmation email to the new address and the
/// app has no email sender configured, so the page explains that instead of offering a form
/// that would never complete.
/// </summary>
[Authorize]
public class EmailModel : ManagePageModel
{
    public EmailModel(UserManager<ApplicationUser> userManager, ICurrentUserService currentUser)
        : base(userManager, currentUser)
    {
    }

    public string? Email { get; private set; }

    public string? JiraUsername { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await GetUserAsync();
        if (user is null)
        {
            return UserMissing();
        }

        Email = await UserManager.GetEmailAsync(user);
        JiraUsername = user.JiraUsername;
        return Page();
    }
}
