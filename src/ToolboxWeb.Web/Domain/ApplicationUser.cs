using Microsoft.AspNetCore.Identity;

namespace ToolboxWeb.Web.Domain;

public class ApplicationUser : IdentityUser
{
    // [PersonalData] puts a field in the "download your personal data" file (account page).
    [PersonalData]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [PersonalData]
    public string? AvatarUrl { get; set; }

    [PersonalData]
    public string? AuthSource { get; set; }

    [PersonalData]
    public string? JiraBaseUrl { get; set; }

    [PersonalData]
    public string? JiraUsername { get; set; }

    [PersonalData]
    public string? JiraDisplayName { get; set; }
}
