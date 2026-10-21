using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ToolboxWeb.Web.Domain;
using ToolboxWeb.Web.Services;

namespace ToolboxWeb.Web.Areas.Identity.Pages.Account.Manage;

/// <summary>
/// Shared plumbing for the account pages: the signed-in user, the one-line status message
/// shown after a redirect, and whether the account signs in through Jira (Jira accounts have
/// no ToolboxWeb password, so the password and two-factor pages do not apply to them).
/// </summary>
public abstract class ManagePageModel : PageModel
{
    protected ManagePageModel(UserManager<ApplicationUser> userManager, ICurrentUserService currentUser)
    {
        UserManager = userManager;
        CurrentUser = currentUser;
    }

    protected UserManager<ApplicationUser> UserManager { get; }

    protected ICurrentUserService CurrentUser { get; }

    [TempData]
    public string? StatusMessage { get; set; }

    public bool IsJiraUser => CurrentUser.IsJiraUser;

    /// <summary>The signed-in user, or null when the account no longer exists.</summary>
    protected Task<ApplicationUser?> GetUserAsync() => UserManager.GetUserAsync(User);

    /// <summary>The account was deleted in another tab: back to sign-in instead of an error page.</summary>
    protected IActionResult UserMissing() => Challenge();

    protected void AddErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }
    }
}
