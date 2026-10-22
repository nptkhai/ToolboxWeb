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
    public ChecklistPriority Priority { get; set; } = ChecklistPriority.Normal;

    /// <summary>
    /// Set when the user deletes the item. It stays a day so "Undo" can bring it back, then
    /// ChecklistService removes it for good. Every query skips rows that have it.
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    public ApplicationUser? User { get; set; }
}
