using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using ToolboxWeb.Web.Domain;
using ToolboxWeb.Web.Enums;
using ToolboxWeb.Web.Services;

namespace ToolboxWeb.Web.Areas.Identity.Pages.Account.Manage;

[Authorize]
public class EnableAuthenticatorModel : ManagePageModel
{
    private readonly IActivityLogService _activityLog;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public EnableAuthenticatorModel(
        UserManager<ApplicationUser> userManager,
        ICurrentUserService currentUser,
        IActivityLogService activityLog,
        IStringLocalizer<SharedResource> localizer)
        : base(userManager, currentUser)
    {
        _activityLog = activityLog;
        _localizer = localizer;
    }

    /// <summary>The key to type into the authenticator app, in groups of four.</summary>
    public string SharedKey { get; private set; } = string.Empty;

    [TempData]
    public string[]? RecoveryCodes { get; set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required(ErrorMessage = "Account.Validation.Required")]
        [StringLength(7, MinimumLength = 6, ErrorMessage = "Account.Validation.CodeLength")]
        [DataType(DataType.Text)]
        public string Code { get; set; } = string.Empty;
    }

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

        await LoadSharedKeyAsync(user);
        return Page();
    }

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

        if (!ModelState.IsValid)
        {
            await LoadSharedKeyAsync(user);
            return Page();
        }

        var code = Input.Code.Replace(" ", string.Empty).Replace("-", string.Empty);
        var valid = await UserManager.VerifyTwoFactorTokenAsync(user, UserManager.Options.Tokens.AuthenticatorTokenProvider, code);
        if (!valid)
        {
            ModelState.AddModelError("Input.Code", _localizer["Account.TwoFactor.InvalidCode"].Value);
            await LoadSharedKeyAsync(user);
            return Page();
        }

        await UserManager.SetTwoFactorEnabledAsync(user, true);
        await _activityLog.LogAsync(user.Id, ActivityActionType.Update, ActivityEntityType.UserProfile, user.Id, _localizer["ActivityLog.TwoFactorEnabledSummary"].Value);
        StatusMessage = _localizer["Account.TwoFactor.AppVerified"].Value;

        if (await UserManager.CountRecoveryCodesAsync(user) == 0)
        {
            RecoveryCodes = (await UserManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10))?.ToArray();
            return RedirectToPage("./ShowRecoveryCodes");
        }

        return RedirectToPage("./TwoFactorAuthentication");
    }

    private async Task LoadSharedKeyAsync(ApplicationUser user)
    {
        var key = await UserManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(key))
        {
            await UserManager.ResetAuthenticatorKeyAsync(user);
            key = await UserManager.GetAuthenticatorKeyAsync(user) ?? string.Empty;
        }

        var grouped = new StringBuilder();
        for (var index = 0; index < key.Length; index += 4)
        {
            grouped.Append(key.AsSpan(index, Math.Min(4, key.Length - index))).Append(' ');
        }

        SharedKey = grouped.ToString().TrimEnd().ToLowerInvariant();
    }
}
