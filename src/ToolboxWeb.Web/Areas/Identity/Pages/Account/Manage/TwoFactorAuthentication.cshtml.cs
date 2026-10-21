using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using ToolboxWeb.Web.Domain;
using ToolboxWeb.Web.Services;

namespace ToolboxWeb.Web.Areas.Identity.Pages.Account.Manage;

[Authorize]
public class TwoFactorAuthenticationModel : ManagePageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public TwoFactorAuthenticationModel(
        UserManager<ApplicationUser> userManager,
        ICurrentUserService currentUser,
        SignInManager<ApplicationUser> signInManager,
        IStringLocalizer<SharedResource> localizer)
        : base(userManager, currentUser)
    {
        _signInManager = signInManager;
        _localizer = localizer;
    }

    public bool HasAuthenticator { get; private set; }

    public bool Is2faEnabled { get; private set; }

    public bool IsMachineRemembered { get; private set; }

    public int RecoveryCodesLeft { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await GetUserAsync();
        if (user is null)
        {
            return UserMissing();
        }

        if (IsJiraUser)
        {
            return RedirectToPage("./Index");
        }

        HasAuthenticator = await UserManager.GetAuthenticatorKeyAsync(user) is not null;
        Is2faEnabled = await UserManager.GetTwoFactorEnabledAsync(user);
        IsMachineRemembered = await _signInManager.IsTwoFactorClientRememberedAsync(user);
        RecoveryCodesLeft = await UserManager.CountRecoveryCodesAsync(user);
        return Page();
    }

    /// <summary>Forget this browser: the next sign-in here asks for a code again.</summary>
    public async Task<IActionResult> OnPostAsync()
    {
        if (await GetUserAsync() is null)
        {
            return UserMissing();
        }

        await _signInManager.ForgetTwoFactorClientAsync();
        StatusMessage = _localizer["Account.TwoFactor.BrowserForgotten"].Value;
        return RedirectToPage();
    }
}
