using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using ToolboxWeb.Web.Constants;
using ToolboxWeb.Web.Domain;
using ToolboxWeb.Web.Infrastructure.Hosting;
using ToolboxWeb.Web.ViewModels.Jira;

namespace ToolboxWeb.Web.Services;

public interface IJiraAuthService
{
    Task<JiraSignInResult> SignInAsync(HttpContext httpContext, string baseUrl, string username, string password);
    Task<JiraSession?> GetCurrentSessionAsync(HttpContext httpContext);
    Task SignOutAsync(HttpContext httpContext);

    /// <summary>
    /// Whether Jira screens apply: a Jira session on the server; a saved Jira connection on a
    /// desktop (the session itself is opened by the first Jira request).
    /// </summary>
    Task<bool> IsAvailableAsync(HttpContext httpContext);
}

public sealed class JiraAuthService : IJiraAuthService
{
    private const string SessionMarkerKey = "Toolbox.Session.Active";

    private readonly IJiraSessionStore _sessions;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly AppModeInfo _mode;
    private readonly IDesktopJiraService _desktopJira;

    public JiraAuthService(
        IJiraSessionStore sessions,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IStringLocalizer<SharedResource> localizer,
        AppModeInfo mode,
        IDesktopJiraService desktopJira)
    {
        _sessions = sessions;
        _userManager = userManager;
        _signInManager = signInManager;
        _localizer = localizer;
        _mode = mode;
        _desktopJira = desktopJira;
    }

    public async Task<JiraSignInResult> SignInAsync(HttpContext httpContext, string baseUrl, string username, string password)
    {
        try
        {
            var normalizedBaseUrl = NormalizeBaseUrl(baseUrl);
            var normalizedUserName = NormalizeJiraUsername(username);
            var session = await _sessions.CreateAsync(BrowserSessionId(httpContext), normalizedBaseUrl, normalizedUserName, password);

            var shadowUserName = BuildShadowUserName(normalizedBaseUrl, normalizedUserName);
            var user = await _userManager.FindByNameAsync(shadowUserName);
            if (user is null)
            {
                user = new ApplicationUser
                {
                    UserName = shadowUserName,
                    EmailConfirmed = true,
                    AuthSource = AuthSources.Jira,
                    JiraBaseUrl = normalizedBaseUrl,
                    JiraUsername = normalizedUserName,
                    JiraDisplayName = session.DisplayName
                };

                var createResult = await _userManager.CreateAsync(user);
                if (!createResult.Succeeded)
                {
                    await _sessions.InvalidateAsync(session.BrowserSessionId);
                    return new JiraSignInResult
                    {
                        Message = string.Join(Environment.NewLine, createResult.Errors.Select(x => x.Description))
                    };
                }
            }
            else
            {
                var changed = false;
                if (!string.Equals(user.AuthSource, AuthSources.Jira, StringComparison.OrdinalIgnoreCase))
                {
                    user.AuthSource = AuthSources.Jira;
                    changed = true;
                }

                if (!string.Equals(user.JiraBaseUrl, normalizedBaseUrl, StringComparison.OrdinalIgnoreCase))
                {
                    user.JiraBaseUrl = normalizedBaseUrl;
                    changed = true;
                }

                if (!string.Equals(user.JiraUsername, normalizedUserName, StringComparison.OrdinalIgnoreCase))
                {
                    user.JiraUsername = normalizedUserName;
                    changed = true;
                }

                if (!string.Equals(user.JiraDisplayName, session.DisplayName, StringComparison.Ordinal))
                {
                    user.JiraDisplayName = session.DisplayName;
                    changed = true;
                }

                if (changed)
                {
                    var updateResult = await _userManager.UpdateAsync(user);
                    if (!updateResult.Succeeded)
                    {
                        await _sessions.InvalidateAsync(session.BrowserSessionId);
                        return new JiraSignInResult
                        {
                            Message = string.Join(Environment.NewLine, updateResult.Errors.Select(x => x.Description))
                        };
                    }
                }
            }

            await _signInManager.SignInAsync(user, isPersistent: false);

            return new JiraSignInResult
            {
                Succeeded = true,
                Session = session
            };
        }
        catch (Exception ex)
        {
            return new JiraSignInResult
            {
                Message = Infrastructure.Jira.JiraErrorHelper.ToUserMessage(
                    ex,
                    _localizer["Jira.LoginFailed"].Value,
                    null,
                    _localizer["Jira.NotFound"].Value,
                    _localizer["Jira.InvalidRequest"].Value)
            };
        }
    }

    public Task<JiraSession?> GetCurrentSessionAsync(HttpContext httpContext)
    {
        // A desktop has one Jira connection, saved on the settings page (DesktopJiraService).
        return _mode.IsDesktop
            ? _desktopJira.GetSessionAsync(httpContext.RequestAborted)
            : _sessions.GetAsync(BrowserSessionId(httpContext));
    }

    public async Task<bool> IsAvailableAsync(HttpContext httpContext)
    {
        return _mode.IsDesktop
            ? _desktopJira.IsConfigured
            : await GetCurrentSessionAsync(httpContext) is not null;
    }

    public async Task SignOutAsync(HttpContext httpContext)
    {
        if (_mode.IsDesktop)
        {
            // The desktop owner stays signed in; only the Jira session is closed and reopened later.
            await _desktopJira.DropSessionAsync();
            return;
        }

        var browserSessionId = BrowserSessionId(httpContext);
        await _sessions.InvalidateAsync(browserSessionId);
        await _signInManager.SignOutAsync();
    }

    private static string BrowserSessionId(HttpContext httpContext)
    {
        httpContext.Session.SetString(SessionMarkerKey, "1");
        return httpContext.Session.Id;
    }

    /// <summary>"https://jira.example.com/" → "https://jira.example.com" (scheme and host lower case, no query).</summary>
    public static string NormalizeBaseUrl(string baseUrl)
    {
        var normalized = baseUrl?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("Jira URL is required.");
        }

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException("Jira URL is invalid.");
        }

        var builder = new UriBuilder(uri)
        {
            Query = string.Empty,
            Fragment = string.Empty
        };

        var absolutePath = builder.Path?.TrimEnd('/') ?? string.Empty;
        var basePath = string.IsNullOrWhiteSpace(absolutePath) || absolutePath == "/"
            ? string.Empty
            : absolutePath;

        return $"{builder.Scheme.ToLowerInvariant()}://{builder.Uri.Authority.ToLowerInvariant()}{basePath}";
    }

    public static string NormalizeJiraUsername(string userName)
    {
        var normalized = userName?.Trim().ToLowerInvariant() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("Jira username is required.");
        }

        return normalized;
    }

    private static string BuildShadowUserName(string baseUrl, string jiraUsername)
    {
        var uri = new Uri(baseUrl);
        var scope = $"{uri.Authority}{uri.AbsolutePath}".TrimEnd('/')
            .ToLowerInvariant()
            .Replace("/", "-")
            .Replace(":", "-");
        return $"jira:{scope}:{jiraUsername}";
    }
}
