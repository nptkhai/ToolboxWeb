using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using ToolboxWeb.Web.Domain;
using ToolboxWeb.Web.Enums;
using ToolboxWeb.Web.Services;

namespace ToolboxWeb.Web.Areas.Identity.Pages.Account.Manage;

[Authorize]
public class Disable2faModel : ManagePageModel
{
    private readonly IActivityLogService _activityLog;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public Disable2faModel(
        UserManager<ApplicationUser> userManager,
        ICurrentUserService currentUser,
        IActivityLogService activityLog,
        IStringLocalizer<SharedResource> localizer)
        : base(userManager, currentUser)
    {
        _activityLog = activityLog;
        _localizer = localizer;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await GetUserAsync();
        if (user is null)
        {
            return UserMissing();
        }

        return await UserManager.GetTwoFactorEnabledAsync(user) ? Page() : RedirectToPage("./TwoFactorAuthentication");
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await GetUserAsync();
        if (user is null)
        {
            return UserMissing();
        }

        var result = await UserManager.SetTwoFactorEnabledAsync(user, false);
        if (!result.Succeeded)
        {
            AddErrors(result);
            return Page();
        }

        await _activityLog.LogAsync(user.Id, ActivityActionType.Update, ActivityEntityType.UserProfile, user.Id, _localizer["ActivityLog.TwoFactorDisabledSummary"].Value);
        StatusMessage = _localizer["Account.TwoFactor.Disabled"].Value;
        return RedirectToPage("./TwoFactorAuthentication");
    }
}
