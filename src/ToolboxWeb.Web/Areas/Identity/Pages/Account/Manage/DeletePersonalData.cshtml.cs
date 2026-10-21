using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using ToolboxWeb.Web.Domain;
using ToolboxWeb.Web.Services;

namespace ToolboxWeb.Web.Areas.Identity.Pages.Account.Manage;

/// <summary>
/// Deletes the account and everything the user owns (IAccountService). ToolboxWeb accounts
/// re-enter their password; Jira accounts have none, so the confirmation dialog is the check.
/// </summary>
[Authorize]
public class DeletePersonalDataModel : ManagePageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IAccountService _accounts;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly ILogger<DeletePersonalDataModel> _logger;

    public DeletePersonalDataModel(
        UserManager<ApplicationUser> userManager,
        ICurrentUserService currentUser,
        SignInManager<ApplicationUser> signInManager,
        IAccountService accounts,
        IStringLocalizer<SharedResource> localizer,
        ILogger<DeletePersonalDataModel> logger)
        : base(userManager, currentUser)
    {
        _signInManager = signInManager;
        _accounts = accounts;
        _localizer = localizer;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [DataType(DataType.Password)]
        public string? Password { get; set; }
    }

    public bool RequirePassword { get; private set; }

    public string AccountName { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await GetUserAsync();
        if (user is null)
        {
            return UserMissing();
        }

        await LoadAsync(user);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await GetUserAsync();
        if (user is null)
        {
            return UserMissing();
        }

        await LoadAsync(user);
        if (RequirePassword && (string.IsNullOrEmpty(Input.Password) || !await UserManager.CheckPasswordAsync(user, Input.Password)))
        {
            ModelState.AddModelError("Input.Password", _localizer["Account.Delete.WrongPassword"].Value);
            return Page();
        }

        var result = await _accounts.DeleteAccountAsync(user);
        if (!result.Succeeded)
        {
            AddErrors(result);
            return Page();
        }

        await _signInManager.SignOutAsync();
        _logger.LogInformation("A user deleted their account.");
        return Redirect("~/");
    }

    private async Task LoadAsync(ApplicationUser user)
    {
        RequirePassword = await UserManager.HasPasswordAsync(user);
        AccountName = user.Email ?? user.JiraUsername ?? user.UserName ?? string.Empty;
    }
}
