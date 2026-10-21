using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ToolboxWeb.Web.Constants;
using ToolboxWeb.Web.Domain;

namespace ToolboxWeb.Web.Areas.Identity.Pages.Account;

/// <summary>
/// New ToolboxWeb account with email and password. Accounts are usable at once
/// (RequireConfirmedAccount is off and no email sender is configured).
/// </summary>
public class RegisterModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ILogger<RegisterModel> _logger;

    public RegisterModel(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ILogger<RegisterModel> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; private set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Account.Validation.Required")]
        [EmailAddress(ErrorMessage = "Account.Validation.Email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Account.Validation.Required")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Account.Validation.PasswordLength")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Account.Validation.PasswordMismatch")]
        public string? ConfirmPassword { get; set; }
    }

    public IActionResult OnGet(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return LocalRedirect(ResolveReturnUrl(returnUrl));
        }

        ReturnUrl = returnUrl;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var email = Input.Email.Trim();
        var user = new ApplicationUser { UserName = email, Email = email };
        var result = await _userManager.CreateAsync(user, Input.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return Page();
        }

        _logger.LogInformation("A user created a new account with a password.");
        await _signInManager.SignInAsync(user, isPersistent: false);
        return LocalRedirect(ResolveReturnUrl(returnUrl));
    }

    private string ResolveReturnUrl(string? returnUrl)
        => Url.IsLocalUrl(returnUrl) ? returnUrl! : Url.RouteUrl(ToolboxRouteSlugs.RouteNames.DashboardHtml) ?? "/dashboard.html";
}
