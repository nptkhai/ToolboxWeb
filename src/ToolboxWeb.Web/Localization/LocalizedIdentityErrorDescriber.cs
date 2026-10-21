using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;

namespace ToolboxWeb.Web.Localization;

/// <summary>
/// ASP.NET Identity error messages from the app's localization files (Identity.Error.*),
/// so sign-up and password errors follow the UI language instead of always being English.
/// </summary>
public sealed class LocalizedIdentityErrorDescriber(IStringLocalizer<SharedResource> localizer) : IdentityErrorDescriber
{
    private IdentityError Error(string code, params object[] arguments)
        => new() { Code = code, Description = localizer[$"Identity.Error.{code}", arguments].Value };

    public override IdentityError DefaultError() => Error(nameof(DefaultError));
    public override IdentityError ConcurrencyFailure() => Error(nameof(ConcurrencyFailure));
    public override IdentityError PasswordMismatch() => Error(nameof(PasswordMismatch));
    public override IdentityError InvalidToken() => Error(nameof(InvalidToken));
    public override IdentityError RecoveryCodeRedemptionFailed() => Error(nameof(RecoveryCodeRedemptionFailed));
    public override IdentityError LoginAlreadyAssociated() => Error(nameof(LoginAlreadyAssociated));
    public override IdentityError InvalidUserName(string? userName) => Error(nameof(InvalidUserName), userName ?? string.Empty);
    public override IdentityError InvalidEmail(string? email) => Error(nameof(InvalidEmail), email ?? string.Empty);
    public override IdentityError DuplicateUserName(string userName) => Error(nameof(DuplicateUserName), userName);
    public override IdentityError DuplicateEmail(string email) => Error(nameof(DuplicateEmail), email);
    public override IdentityError UserAlreadyHasPassword() => Error(nameof(UserAlreadyHasPassword));
    public override IdentityError UserLockoutNotEnabled() => Error(nameof(UserLockoutNotEnabled));
    public override IdentityError PasswordTooShort(int length) => Error(nameof(PasswordTooShort), length);
    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => Error(nameof(PasswordRequiresUniqueChars), uniqueChars);
    public override IdentityError PasswordRequiresNonAlphanumeric() => Error(nameof(PasswordRequiresNonAlphanumeric));
    public override IdentityError PasswordRequiresDigit() => Error(nameof(PasswordRequiresDigit));
    public override IdentityError PasswordRequiresLower() => Error(nameof(PasswordRequiresLower));
    public override IdentityError PasswordRequiresUpper() => Error(nameof(PasswordRequiresUpper));
}
