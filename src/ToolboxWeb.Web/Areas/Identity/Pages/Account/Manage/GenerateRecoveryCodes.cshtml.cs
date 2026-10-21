using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using ToolboxWeb.Web.Domain;
using ToolboxWeb.Web.Services;

namespace ToolboxWeb.Web.Areas.Identity.Pages.Account.Manage;

[Authorize]
public class GenerateRecoveryCodesModel : ManagePageModel
{
    private readonly IStringLocalizer<SharedResource> _localizer;

    public GenerateRecoveryCodesModel(
        UserManager<ApplicationUser> userManager,
        ICurrentUserService currentUser,
        IStringLocalizer<SharedResource> localizer)
        : base(userManager, currentUser)
    {
        _localizer = localizer;
    }

    [TempData]
    public string[]? RecoveryCodes { get; set; }

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

        if (!await UserManager.GetTwoFactorEnabledAsync(user))
        {
            return RedirectToPage("./TwoFactorAuthentication");
        }

        RecoveryCodes = (await UserManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10))?.ToArray();
        StatusMessage = _localizer["Account.TwoFactor.CodesGenerated"].Value;
        return RedirectToPage("./ShowRecoveryCodes");
    }
}
