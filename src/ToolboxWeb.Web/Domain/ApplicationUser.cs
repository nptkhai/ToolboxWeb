using Microsoft.AspNetCore.Identity;

namespace ToolboxWeb.Web.Domain;

public class ApplicationUser : IdentityUser
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
