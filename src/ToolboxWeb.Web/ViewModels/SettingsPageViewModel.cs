using ToolboxWeb.Web.Services;

namespace ToolboxWeb.Web.ViewModels;

/// <summary>The desktop Settings page (App:Mode=Desktop): the Jira connection and the license.</summary>
public sealed class SettingsPageViewModel
{
    public required DesktopJiraStatus Jira { get; init; }

    public required DeviceLicenseStatus License { get; init; }

    public required string DeviceId { get; init; }

    public JiraSettingsForm Form { get; init; } = new();

    /// <summary>Why the last save failed; shown under the form.</summary>
    public string? FormError { get; init; }

    /// <summary>A Jira page that sent the owner here; the save goes back to it.</summary>
    public string? ReturnUrl { get; init; }
}

/// <summary>The Jira connection form. The password or token is posted as "secret" and never sent back to the page.</summary>
public sealed class JiraSettingsForm
{
    public string? BaseUrl { get; set; }

    public JiraSignInKind Kind { get; set; } = JiraSignInKind.Password;

    public string? Username { get; set; }
}
