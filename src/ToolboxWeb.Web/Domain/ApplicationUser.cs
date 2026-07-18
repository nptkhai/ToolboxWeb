using Microsoft.AspNetCore.Identity;

namespace ToolboxWeb.Web.Domain;

public class ApplicationUser : IdentityUser
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? AvatarUrl { get; set; }
    public string? AuthSource { get; set; }
    public string? JiraBaseUrl { get; set; }
    public string? JiraUsername { get; set; }
    public string? JiraDisplayName { get; set; }
}
