using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using ToolboxWeb.Web.Domain;
using ToolboxWeb.Web.Services;
using ToolboxWeb.Web.ViewModels.Jira;

namespace ToolboxWeb.Web.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class LoginModel : PageModel
{
    private const string LocalMode = "local";
    private const string JiraMode = "jira";

    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly IJiraAuthService _jiraAuth;
    private readonly IOptions<JiraOptions> _jiraOptions;

    public LoginModel(
        SignInManager<ApplicationUser> signInManager,
        IStringLocalizer<SharedResource> localizer,
        IJiraAuthService jiraAuth,
        IOptions<JiraOptions> jiraOptions)
    {
        _signInManager = signInManager;
        _localizer = localizer;
        _jiraAuth = jiraAuth;
        _jiraOptions = jiraOptions;
    }

    [BindProperty]
    public LocalLoginInputModel LocalInput { get; set; } = new();

    [BindProperty]
    public JiraLoginInputModel JiraInput { get; set; } = new();

    public string ReturnUrl { get; private set; } = string.Empty;
    public string? LocalErrorMessage { get; private set; }
    public string? JiraErrorMessage { get; private set; }
    public string ActiveMode { get; private set; } = LocalMode;

    public IActionResult OnGet(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return LocalRedirect(ResolveReturnUrl(returnUrl));
        }

        ReturnUrl = ResolveReturnUrl(returnUrl);
        JiraInput.BaseUrl = _jiraOptions.Value.DefaultBaseUrl;
        ActiveMode = LocalMode;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        ReturnUrl = ResolveReturnUrl(returnUrl);
        JiraInput.BaseUrl = _jiraOptions.Value.DefaultBaseUrl;
        ActiveMode = LocalMode;

        if (string.IsNullOrWhiteSpace(LocalInput.Email) || string.IsNullOrWhiteSpace(LocalInput.Password))
        {
            LocalErrorMessage = _localizer["Login.Local.Required"].Value;
            return Page();
        }

        var result = await _signInManager.PasswordSignInAsync(LocalInput.Email.Trim(), LocalInput.Password, LocalInput.RememberMe, lockoutOnFailure: false);
        if (result.Succeeded)
        {
            return LocalRedirect(ReturnUrl);
        }

        if (result.IsLockedOut)
        {
            LocalErrorMessage = _localizer["Login.Local.LockedOut"].Value;
            return Page();
        }

        LocalErrorMessage = _localizer["Login.Local.Failed"].Value;
        return Page();
    }

    public async Task<IActionResult> OnPostJiraAsync(string? returnUrl = null)
    {
        ReturnUrl = ResolveReturnUrl(returnUrl);
        ActiveMode = JiraMode;

        if (string.IsNullOrWhiteSpace(JiraInput.BaseUrl)
            || string.IsNullOrWhiteSpace(JiraInput.Username)
            || string.IsNullOrWhiteSpace(JiraInput.Password))
        {
            JiraErrorMessage = _localizer["Login.Jira.Required"].Value;
            return Page();
        }

        var result = await _jiraAuth.SignInAsync(HttpContext, JiraInput.BaseUrl, JiraInput.Username, JiraInput.Password);
        if (result.Succeeded)
        {
            return LocalRedirect(ReturnUrl);
        }

        JiraErrorMessage = string.IsNullOrWhiteSpace(result.Message) ? _localizer["Jira.LoginFailed"].Value : result.Message;
        return Page();
    }

    private string ResolveReturnUrl(string? returnUrl)
    {
        return Url.IsLocalUrl(returnUrl) ? returnUrl! : Url.Content("~/dashboard.html");
    }
}
