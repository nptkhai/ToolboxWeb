using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using ToolboxWeb.Web.Infrastructure.Hosting;
using ToolboxWeb.Web.Services;

namespace ToolboxWeb.Web.Infrastructure.Files;

/// <summary>
/// Who may use "Files &amp; folders": the owner of a desktop; on the IIS site only the local
/// accounts listed in <c>Files:AllowedUsers</c> (never a Jira account); nobody on the web copy.
/// </summary>
public sealed class FilesAccess
{
    private readonly AppModeInfo _mode;
    private readonly IOptionsMonitor<FilesOptions> _options;
    private readonly ICurrentUserService _currentUser;

    public FilesAccess(AppModeInfo mode, IOptionsMonitor<FilesOptions> options, ICurrentUserService currentUser)
    {
        _mode = mode;
        _options = options;
        _currentUser = currentUser;
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

            var name = _currentUser.UserName;
            return !_currentUser.IsJiraUser
                && !string.IsNullOrWhiteSpace(name)
                && _options.CurrentValue.AllowedUsers.Any(x => string.Equals(x?.Trim(), name, StringComparison.OrdinalIgnoreCase));
        }
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
