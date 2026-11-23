using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using ToolboxWeb.Web.Infrastructure.Hosting;
using ToolboxWeb.Web.Services;

namespace ToolboxWeb.Web.Infrastructure.Files;

/// <summary>
/// Who may use "Files &amp; folders": the owner of a desktop; on the IIS site only the local
/// accounts listed in <c>Files:AllowedUsers</c> and the Jira accounts listed, with their Jira
/// address, in <c>Files:AllowedJiraUsers</c>; nobody on the web copy.
/// </summary>
public sealed class FilesAccess
{
    private readonly AppModeInfo _mode;
    private readonly IOptionsMonitor<FilesOptions> _options;
    private readonly ICurrentUserService _currentUser;
    private readonly IHttpContextAccessor _http;

    public FilesAccess(AppModeInfo mode, IOptionsMonitor<FilesOptions> options, ICurrentUserService currentUser, IHttpContextAccessor http)
    {
        _mode = mode;
        _options = options;
        _currentUser = currentUser;
        _http = http;
    }

    /// <summary>
    /// "Open with the default program" and "Open the containing folder" start a program on the
    /// machine that runs Toolkit, so they are offered only when that machine is the visitor's own:
    /// the desktop copy, or the IIS / Visual Studio copy reached from the same Windows machine
    /// (a loopback address) while it runs in a signed-in Windows session. An IIS application pool
    /// runs as a service (no session), and a visitor from another computer would see nothing open.
    /// </summary>
    public bool CanOpenOnThisMachine
    {
        get
        {
            if (!IsAllowed || !WindowsShell.IsAvailable)
            {
                return false;
            }

            if (_mode.IsDesktop)
            {
                return true;
            }

            var remote = _http.HttpContext?.Connection.RemoteIpAddress;
            return Environment.UserInteractive && remote is not null && IPAddress.IsLoopback(remote);
        }
    }

    public bool IsAllowed
    {
        get
        {
            if (!_currentUser.IsAuthenticated || _mode.IsWeb)
            {
                return false;
            }

            if (_mode.IsDesktop)
            {
                return true;
            }

            if (_currentUser.IsJiraUser)
            {
                return IsListedJiraUser(_currentUser.JiraBaseUrl, _currentUser.JiraUsername);
            }

            var name = _currentUser.UserName;
            return !string.IsNullOrWhiteSpace(name)
                && _options.CurrentValue.AllowedUsers.Any(x => string.Equals(x?.Trim(), name, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// A Jira account matches an entry only when both the Jira address and the user name match,
    /// so someone signing in to another Jira with the same user name is not let in. Entries that
    /// are incomplete or carry an invalid address are skipped.
    /// </summary>
    private bool IsListedJiraUser(string? baseUrl, string? username)
    {
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(username))
        {
            return false;
        }

        foreach (var entry in _options.CurrentValue.AllowedJiraUsers)
        {
            if (entry is null || string.IsNullOrWhiteSpace(entry.BaseUrl) || string.IsNullOrWhiteSpace(entry.Username))
            {
                continue;
            }

            string entryBaseUrl;
            try
            {
                entryBaseUrl = JiraAuthService.NormalizeBaseUrl(entry.BaseUrl);
            }
            catch (InvalidOperationException)
            {
                continue;
            }

            if (string.Equals(entryBaseUrl, baseUrl, StringComparison.OrdinalIgnoreCase)
                && string.Equals(JiraAuthService.NormalizeJiraUsername(entry.Username), username, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The desktop may open any folder; the IIS site only folders under <c>Files:AllowedFolders</c>.</summary>
    public bool RestrictsFolders => !_mode.IsDesktop;

    public IReadOnlyList<string> AllowedFolders =>
        _options.CurrentValue.AllowedFolders
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => FilePathGuard.NormalizeRoot(x))
            .Distinct(FilePathGuard.PathComparer)
            .ToList();

    /// <summary>Whether a folder may be a root here. Links are followed first, so a link inside an allowed folder cannot lead out of it.</summary>
    public bool IsFolderAllowed(string fullPath)
    {
        if (!RestrictsFolders)
        {
            return true;
        }

        var real = FilePathGuard.RealPath(fullPath);
        return AllowedFolders.Any(allowed => FilePathGuard.IsInside(allowed, fullPath) && FilePathGuard.IsInside(FilePathGuard.RealPath(allowed), real));
    }
}

/// <summary>Answers 404 when <see cref="FilesAccess"/> denies, so the screen does not advertise itself.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class FilesAccessRequiredAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (!context.HttpContext.RequestServices.GetRequiredService<FilesAccess>().IsAllowed)
        {
            context.Result = new NotFoundResult();
        }
    }
}
