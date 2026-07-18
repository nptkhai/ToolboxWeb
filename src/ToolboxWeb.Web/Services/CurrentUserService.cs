using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using ToolboxWeb.Web.Constants;

namespace ToolboxWeb.Web.Services;

public interface ICurrentUserService
{
    string UserId { get; }
    string? UserName { get; }
    string? DisplayName { get; }
    bool IsAuthenticated { get; }
    string AuthSource { get; }
    bool IsJiraUser { get; }
}

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string UserId => _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    public string? UserName => _httpContextAccessor.HttpContext?.User.Identity?.Name;
    public string? DisplayName => _httpContextAccessor.HttpContext?.User.FindFirstValue(ToolboxClaimTypes.DisplayName) ?? UserName;
    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;
    public string AuthSource => _httpContextAccessor.HttpContext?.User.FindFirstValue(ToolboxClaimTypes.AuthSource) ?? AuthSources.Local;
    public bool IsJiraUser => string.Equals(AuthSource, AuthSources.Jira, StringComparison.OrdinalIgnoreCase);
}
