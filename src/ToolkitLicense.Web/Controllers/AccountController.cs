using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;
using ToolkitLicense.Web.Data;
using ToolkitLicense.Web.Domain;
using ToolkitLicense.Web.Infrastructure;
using ToolkitLicense.Web.Services;
using ToolkitLicense.Web.ViewModels;

namespace ToolkitLicense.Web.Controllers;

/// <summary>
/// The administrator's account: first set-up (needs the code from setup-code.txt), sign-in with
/// password then authenticator code, the required authenticator set-up, password and recovery codes.
/// </summary>
[Route("account")]
public sealed class AccountController(
    UserManager<AdminUser> users,
    SignInManager<AdminUser> signIn,
    SetupCode setupCode,
    LicenseDbContext db,
    IAuditLog audit,
    IStringLocalizer<SharedResource> T) : Controller
{
    private const string RecoveryCodesKey = "RecoveryCodes";

    // ------------------------------------------------------------------ first set-up

    [AllowAnonymous, HttpGet("/setup")]
    public IActionResult Setup()
    {
        if (users.Users.Any())
        {
            return RedirectToAction(nameof(Login));
        }

        ViewData["SetupFile"] = setupCode.FilePath;
        return View(new SetupInput { UserName = "admin" });
    }

    [AllowAnonymous, HttpPost("/setup"), EnableRateLimiting(RateLimits.SignIn)]
    public async Task<IActionResult> Setup(SetupInput input)
    {
        if (users.Users.Any())
        {
            return RedirectToAction(nameof(Login));
        }

        ViewData["SetupFile"] = setupCode.FilePath;
        if (!setupCode.Matches(input.Code))
        {
            ModelState.AddModelError(nameof(input.Code), T["Setup.Error.Code"]);
        }

        if (string.IsNullOrWhiteSpace(input.UserName))
        {
            ModelState.AddModelError(nameof(input.UserName), T["Account.Error.UserName"]);
        }

        if (input.Password != input.ConfirmPassword)
        {
            ModelState.AddModelError(nameof(input.ConfirmPassword), T["Account.Error.Confirm"]);
        }

        if (!ModelState.IsValid)
        {
            return View(input);
        }

        var user = new AdminUser { UserName = input.UserName!.Trim() };
        var result = await users.CreateAsync(user, input.Password ?? string.Empty);
        if (!result.Succeeded)
        {
            AddErrors(result, nameof(input.Password));
            return View(input);
        }

        setupCode.Delete();
        await signIn.SignInAsync(user, isPersistent: false);
        audit.Add(AuditActions.SetupAdmin, user.UserName!);
        await db.SaveChangesAsync();
        // The middleware sends a new administrator straight to the authenticator set-up.
        return Redirect(RequireTwoFactorMiddleware.SetupPath);
    }

    // ------------------------------------------------------------------ sign-in

    [AllowAnonymous, HttpGet("login")]
    public IActionResult Login(string? returnUrl)
    {
        if (!users.Users.Any())
        {
            return RedirectToAction(nameof(Setup));
        }

        return View(new LoginInput { ReturnUrl = returnUrl });
    }

    [AllowAnonymous, HttpPost("login"), EnableRateLimiting(RateLimits.SignIn)]
    public async Task<IActionResult> Login(LoginInput input)
    {
        var result = await signIn.PasswordSignInAsync(input.UserName?.Trim() ?? string.Empty, input.Password ?? string.Empty, isPersistent: false, lockoutOnFailure: true);
        if (result.RequiresTwoFactor)
        {
            return RedirectToAction(nameof(LoginTwoFactor), new { returnUrl = input.ReturnUrl });
        }

        if (result.Succeeded)
        {
            // Only before the authenticator is set up; the middleware sends the administrator there.
            return LocalRedirect(SafeReturn(input.ReturnUrl));
        }

        ModelState.AddModelError(string.Empty, T[result.IsLockedOut ? "Login.Error.Locked" : "Login.Error.Invalid"]);
        input.Password = null;
        return View(input);
    }

    [AllowAnonymous, HttpGet("login-code")]
    public async Task<IActionResult> LoginTwoFactor(string? returnUrl, bool recovery = false)
    {
        if (await signIn.GetTwoFactorAuthenticationUserAsync() is null)
        {
            return RedirectToAction(nameof(Login));
        }

        return View(new TwoFactorLoginInput { ReturnUrl = returnUrl, UseRecoveryCode = recovery });
    }

    [AllowAnonymous, HttpPost("login-code"), EnableRateLimiting(RateLimits.SignIn)]
    public async Task<IActionResult> LoginTwoFactor(TwoFactorLoginInput input)
    {
        if (await signIn.GetTwoFactorAuthenticationUserAsync() is null)
        {
            return RedirectToAction(nameof(Login));
        }

        // Recovery codes keep their dash ("abcde-fghij"); authenticator codes are digits only.
        var code = (input.Code ?? string.Empty).Replace(" ", string.Empty);
        if (!input.UseRecoveryCode)
        {
            code = code.Replace("-", string.Empty);
        }

        var result = input.UseRecoveryCode
            ? await signIn.TwoFactorRecoveryCodeSignInAsync(code)
            : await signIn.TwoFactorAuthenticatorSignInAsync(code, isPersistent: false, rememberClient: false);
        if (result.Succeeded)
        {
            return LocalRedirect(SafeReturn(input.ReturnUrl));
        }

        ModelState.AddModelError(nameof(input.Code), T[result.IsLockedOut ? "Login.Error.Locked" : "Login.Error.Code"]);
        input.Code = null;
        return View(input);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    // ------------------------------------------------------------------ authenticator set-up

    [HttpGet("two-factor")]
    public async Task<IActionResult> TwoFactor()
    {
        var user = await users.GetUserAsync(User);
        if (user is null)
        {
            return RedirectToAction(nameof(Login));
        }

        if (user.TwoFactorEnabled)
        {
            return RedirectToAction(nameof(Index));
        }

        return View(await BuildSetupAsync(user));
    }

    [HttpPost("two-factor")]
    public async Task<IActionResult> TwoFactor(TwoFactorSetupViewModel input)
    {
        var user = await users.GetUserAsync(User);
        if (user is null)
        {
            return RedirectToAction(nameof(Login));
        }

        var code = (input.Code ?? string.Empty).Replace(" ", string.Empty).Replace("-", string.Empty);
        var valid = await users.VerifyTwoFactorTokenAsync(user, users.Options.Tokens.AuthenticatorTokenProvider, code);
        if (!valid)
        {
            ModelState.AddModelError(nameof(input.Code), T["TwoFactor.Error.Code"]);
            return View(await BuildSetupAsync(user));
        }

        await users.SetTwoFactorEnabledAsync(user, true);
        var codes = await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
        audit.Add(AuditActions.TwoFactorEnabled, user.UserName!);
        await db.SaveChangesAsync();
        await signIn.RefreshSignInAsync(user);
        TempData[RecoveryCodesKey] = string.Join(' ', codes ?? []);
        return RedirectToAction(nameof(RecoveryCodes));
    }

    /// <summary>Shown once, right after the codes were made.</summary>
    [HttpGet("recovery-codes")]
    public IActionResult RecoveryCodes()
    {
        if (TempData[RecoveryCodesKey] is not string codes)
        {
            return RedirectToAction(nameof(Index));
        }

        return View(codes.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    // ------------------------------------------------------------------ account page

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var user = await users.GetUserAsync(User);
        if (user is null)
        {
            return RedirectToAction(nameof(Login));
        }

        return View(new AccountViewModel { UserName = user.UserName ?? string.Empty, RecoveryCodesLeft = await users.CountRecoveryCodesAsync(user) });
    }

    [HttpPost("password")]
    public async Task<IActionResult> ChangePassword(AccountViewModel input)
    {
        var user = await users.GetUserAsync(User);
        if (user is null)
        {
            return RedirectToAction(nameof(Login));
        }

        input.UserName = user.UserName ?? string.Empty;
        input.RecoveryCodesLeft = await users.CountRecoveryCodesAsync(user);
        if (input.NewPassword != input.ConfirmPassword)
        {
            ModelState.AddModelError(nameof(input.ConfirmPassword), T["Account.Error.Confirm"]);
            return View(nameof(Index), input);
        }

        var result = await users.ChangePasswordAsync(user, input.CurrentPassword ?? string.Empty, input.NewPassword ?? string.Empty);
        if (!result.Succeeded)
        {
            AddErrors(result, nameof(input.NewPassword));
            return View(nameof(Index), input);
        }

        audit.Add(AuditActions.PasswordChanged, user.UserName!);
        await db.SaveChangesAsync();
        await signIn.RefreshSignInAsync(user);
        TempData["Success"] = T["Account.PasswordChanged"].Value;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("recovery-codes")]
    public async Task<IActionResult> NewRecoveryCodes()
    {
        var user = await users.GetUserAsync(User);
        if (user is null)
        {
            return RedirectToAction(nameof(Login));
        }

        var codes = await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
        audit.Add(AuditActions.RecoveryCodesRenewed, user.UserName!);
        await db.SaveChangesAsync();
        TempData[RecoveryCodesKey] = string.Join(' ', codes ?? []);
        return RedirectToAction(nameof(RecoveryCodes));
    }

    // ------------------------------------------------------------------ helpers

    private async Task<TwoFactorSetupViewModel> BuildSetupAsync(AdminUser user)
    {
        var key = await users.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(key))
        {
            await users.ResetAuthenticatorKeyAsync(user);
            key = await users.GetAuthenticatorKeyAsync(user);
        }

        var grouped = new StringBuilder();
        for (var i = 0; i < key!.Length; i += 4)
        {
            grouped.Append(key.AsSpan(i, Math.Min(4, key.Length - i))).Append(' ');
        }

        var issuer = UrlEncoder.Default.Encode("Toolkit License");
        return new TwoFactorSetupViewModel
        {
            SharedKey = grouped.ToString().Trim().ToLowerInvariant(),
            AuthenticatorUri = $"otpauth://totp/{issuer}:{UrlEncoder.Default.Encode(user.UserName!)}?secret={key}&issuer={issuer}&digits=6"
        };
    }

    private string SafeReturn(string? returnUrl) => Url.IsLocalUrl(returnUrl) ? returnUrl! : "/";

    private void AddErrors(IdentityResult result, string field)
    {
        foreach (var error in result.Errors)
        {
            var key = $"Identity.{error.Code}";
            var text = T[key];
            ModelState.AddModelError(field, text.ResourceNotFound ? error.Description : text.Value);
        }
    }
}
