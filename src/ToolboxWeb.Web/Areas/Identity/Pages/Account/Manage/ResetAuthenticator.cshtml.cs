using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using ToolboxWeb.Web.Domain;
using ToolboxWeb.Web.Enums;
using ToolboxWeb.Web.Services;

namespace ToolboxWeb.Web.Areas.Identity.Pages.Account.Manage;

[Authorize]
public class ResetAuthenticatorModel : ManagePageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IActivityLogService _activityLog;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public ResetAuthenticatorModel(
        UserManager<ApplicationUser> userManager,
        ICurrentUserService currentUser,
        SignInManager<ApplicationUser> signInManager,
        IActivityLogService activityLog,
        IStringLocalizer<SharedResource> localizer)
        : base(userManager, currentUser)
    {
        _signInManager = signInManager;
        _activityLog = activityLog;
        _localizer = localizer;
    }

    public async Task<IActionResult> OnGetAsync()
        => await GetUserAsync() is null ? UserMissing() : IsJiraUser ? RedirectToPage("./Index") : Page();

    public async Task<IActionResult> OnPostAsync()
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

        await UserManager.SetTwoFactorEnabledAsync(user, false);
        await UserManager.ResetAuthenticatorKeyAsync(user);
        await _signInManager.RefreshSignInAsync(user);
        await _activityLog.LogAsync(user.Id, ActivityActionType.Update, ActivityEntityType.UserProfile, user.Id, _localizer["ActivityLog.TwoFactorDisabledSummary"].Value);
        StatusMessage = _localizer["Account.TwoFactor.AppReset"].Value;
        return RedirectToPage("./EnableAuthenticator");
    }
}
