using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using ToolboxWeb.Web.Domain;
using ToolboxWeb.Web.Services;

namespace ToolboxWeb.Web.Areas.Identity.Pages.Account.Manage;

/// <summary>For a ToolboxWeb account that has no password yet. Jira accounts do not get one.</summary>
[Authorize]
public class SetPasswordModel : ManagePageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public SetPasswordModel(
        UserManager<ApplicationUser> userManager,
        ICurrentUserService currentUser,
        SignInManager<ApplicationUser> signInManager,
        IStringLocalizer<SharedResource> localizer)
        : base(userManager, currentUser)
    {
        _signInManager = signInManager;
        _localizer = localizer;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
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

        return IsJiraUser || await UserManager.HasPasswordAsync(user) ? RedirectToPage("./ChangePassword") : Page();
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
            return RedirectToPage("./ChangePassword");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await UserManager.AddPasswordAsync(user, Input.NewPassword);
        if (!result.Succeeded)
        {
            AddErrors(result);
            return Page();
        }

        await _signInManager.RefreshSignInAsync(user);
        StatusMessage = _localizer["Account.Password.Set"].Value;
        return RedirectToPage("./ChangePassword");
    }
}
