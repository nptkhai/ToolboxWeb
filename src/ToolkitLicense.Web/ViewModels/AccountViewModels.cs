namespace ToolkitLicense.Web.ViewModels;

public sealed class SetupInput
{
    public string? Code { get; set; }
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string? ConfirmPassword { get; set; }
}

public sealed class LoginInput
{
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string? ReturnUrl { get; set; }
}

public sealed class TwoFactorLoginInput
{
    public string? Code { get; set; }
    public bool UseRecoveryCode { get; set; }
    public string? ReturnUrl { get; set; }
}

public sealed class TwoFactorSetupViewModel
{
    /// <summary>The key to type into the authenticator app, in groups of four.</summary>
    public string SharedKey { get; set; } = string.Empty;

    /// <summary>otpauth:// link for apps that accept a pasted link.</summary>
    public string AuthenticatorUri { get; set; } = string.Empty;
    public string? Code { get; set; }
}

public sealed class AccountViewModel
{
    public string UserName { get; set; } = string.Empty;
    public int RecoveryCodesLeft { get; set; }
    public string? CurrentPassword { get; set; }
    public string? NewPassword { get; set; }
    public string? ConfirmPassword { get; set; }
}
