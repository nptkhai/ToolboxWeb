using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using ToolboxWeb.Web.Constants;
using ToolboxWeb.Web.Domain;

namespace ToolboxWeb.Web.Areas.Identity.Pages.Account;

/// <summary>Second sign-in step with a one-time recovery code, when the authenticator app is not at hand.</summary>
public class LoginWithRecoveryCodeModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public LoginWithRecoveryCodeModel(SignInManager<ApplicationUser> signInManager, IStringLocalizer<SharedResource> localizer)
    {
        _signInManager = signInManager;
        _localizer = localizer;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; private set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Account.Validation.Required")]
        [DataType(DataType.Text)]
        public string RecoveryCode { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync(string? returnUrl = null)
    {
        if (await _signInManager.GetTwoFactorAuthenticationUserAsync() is null)
        {
            return RedirectToPage("./Login", new { returnUrl });
        }

        ReturnUrl = returnUrl;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
        if (await _signInManager.GetTwoFactorAuthenticationUserAsync() is null)
        {
            return RedirectToPage("./Login", new { returnUrl });
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await _signInManager.TwoFactorRecoveryCodeSignInAsync(Input.RecoveryCode.Replace(" ", string.Empty));
        if (result.Succeeded)
        {
            return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : Url.RouteUrl(ToolboxRouteSlugs.RouteNames.DashboardHtml) ?? "/dashboard.html");
        }

        if (result.IsLockedOut)
        {
            return RedirectToPage("./Lockout");
        }

        ModelState.AddModelError(string.Empty, _localizer["Login.RecoveryCode.Invalid"].Value);
        return Page();
    }
}
