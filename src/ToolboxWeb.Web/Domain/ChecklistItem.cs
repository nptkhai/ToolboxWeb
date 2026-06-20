using ToolboxWeb.Web.Enums;

namespace ToolboxWeb.Web.Domain;

public class ChecklistItem
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ChecklistStatus Status { get; set; } = ChecklistStatus.Todo;
    public DateTime? DueDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public ApplicationUser? User { get; set; }
}
