using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using ToolboxWeb.Web.Constants;
using ToolboxWeb.Web.Domain;
using ToolboxWeb.Web.ViewModels.Jira;

namespace ToolboxWeb.Web.Services;

public interface IJiraAuthService
{
    Task<JiraSignInResult> SignInAsync(HttpContext httpContext, string baseUrl, string username, string password);
    Task<JiraSession?> GetCurrentSessionAsync(HttpContext httpContext);
    Task SignOutAsync(HttpContext httpContext);
}

public sealed class JiraAuthService : IJiraAuthService
{
    private const string SessionMarkerKey = "Toolbox.Session.Active";

    private readonly IJiraSessionStore _sessions;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public JiraAuthService(
        IJiraSessionStore sessions,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IStringLocalizer<SharedResource> localizer)
    {
        _sessions = sessions;
        _userManager = userManager;
        _signInManager = signInManager;
        _localizer = localizer;
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
        return _sessions.GetAsync(BrowserSessionId(httpContext));
    }

    public async Task SignOutAsync(HttpContext httpContext)
    {
        var browserSessionId = BrowserSessionId(httpContext);
        await _sessions.InvalidateAsync(browserSessionId);
        await _signInManager.SignOutAsync();
    }

    private static string BrowserSessionId(HttpContext httpContext)
    {
        httpContext.Session.SetString(SessionMarkerKey, "1");
        return httpContext.Session.Id;
    }

    private static string NormalizeBaseUrl(string baseUrl)
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

    private static string NormalizeJiraUsername(string userName)
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
