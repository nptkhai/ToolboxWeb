using ToolboxWeb.Web.Enums;

namespace ToolboxWeb.Web.Domain;

public class ActivityLog
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ActivityActionType ActionType { get; set; }
    public ActivityEntityType EntityType { get; set; }
    public string? EntityId { get; set; }
    public string Summary { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ApplicationUser? User { get; set; }
}
