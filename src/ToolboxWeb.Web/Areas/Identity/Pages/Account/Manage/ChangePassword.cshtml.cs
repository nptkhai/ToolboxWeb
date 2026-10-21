using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using ToolboxWeb.Web.Domain;
using ToolboxWeb.Web.Enums;
using ToolboxWeb.Web.Services;

namespace ToolboxWeb.Web.Areas.Identity.Pages.Account.Manage;

[Authorize]
public class ChangePasswordModel : ManagePageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IActivityLogService _activityLog;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public ChangePasswordModel(
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

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required(ErrorMessage = "Account.Validation.Required")]
        [DataType(DataType.Password)]
        public string OldPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Account.Validation.Required")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Account.Validation.PasswordLength")]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "Account.Validation.PasswordMismatch")]
        public string? ConfirmPassword { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await GetUserAsync();
        if (user is null)
        {
            return UserMissing();
        }

        if (!IsJiraUser && !await UserManager.HasPasswordAsync(user))
        {
            return RedirectToPage("./SetPassword");
        }

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
            return RedirectToPage();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await UserManager.ChangePasswordAsync(user, Input.OldPassword, Input.NewPassword);
        if (!result.Succeeded)
        {
            AddErrors(result);
            return Page();
        }

        await _signInManager.RefreshSignInAsync(user);
        await _activityLog.LogAsync(user.Id, ActivityActionType.Update, ActivityEntityType.UserProfile, user.Id, _localizer["ActivityLog.PasswordChangedSummary"].Value);
        StatusMessage = _localizer["Account.Password.Changed"].Value;
        return RedirectToPage();
    }
}
