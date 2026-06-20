using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace ToolboxWeb.Web.Services;

public interface ICurrentUserService
{
    string UserId { get; }
    string? UserName { get; }
    bool IsAuthenticated { get; }
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
    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;
}
