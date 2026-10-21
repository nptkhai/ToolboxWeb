using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using ToolboxWeb.Web.Constants;
using ToolboxWeb.Web.Domain;

namespace ToolboxWeb.Web.Areas.Identity.Pages.Account;

/// <summary>Second step of sign-in for accounts with two-factor authentication.</summary>
public class LoginWith2faModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public LoginWith2faModel(SignInManager<ApplicationUser> signInManager, IStringLocalizer<SharedResource> localizer)
    {
        _signInManager = signInManager;
        _localizer = localizer;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public bool RememberMe { get; private set; }

    public string? ReturnUrl { get; private set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Account.Validation.Required")]
        [StringLength(7, MinimumLength = 6, ErrorMessage = "Account.Validation.CodeLength")]
        [DataType(DataType.Text)]
        public string TwoFactorCode { get; set; } = string.Empty;

        public bool RememberMachine { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(bool rememberMe, string? returnUrl = null)
    {
        // Only after the email and password step; otherwise start over.
        if (await _signInManager.GetTwoFactorAuthenticationUserAsync() is null)
        {
            return RedirectToPage("./Login", new { returnUrl });
        }

        ReturnUrl = returnUrl;
        RememberMe = rememberMe;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(bool rememberMe, string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
        RememberMe = rememberMe;
        if (await _signInManager.GetTwoFactorAuthenticationUserAsync() is null)
        {
            return RedirectToPage("./Login", new { returnUrl });
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var code = Input.TwoFactorCode.Replace(" ", string.Empty).Replace("-", string.Empty);
        var result = await _signInManager.TwoFactorAuthenticatorSignInAsync(code, rememberMe, Input.RememberMachine);
        if (result.Succeeded)
        {
            return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : Url.RouteUrl(ToolboxRouteSlugs.RouteNames.DashboardHtml) ?? "/dashboard.html");
        }

        if (result.IsLockedOut)
        {
            return RedirectToPage("./Lockout");
        }

        ModelState.AddModelError(string.Empty, _localizer["Account.TwoFactor.InvalidCode"].Value);
        return Page();
    }
}
