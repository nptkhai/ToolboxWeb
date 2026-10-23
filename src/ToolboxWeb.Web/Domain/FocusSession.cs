namespace ToolboxWeb.Web.Domain;

public class FocusSession
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
    public string? Note { get; set; }

    /// <summary>The to-do worked on, if one was picked; cleared when that to-do is purged.</summary>
    public int? ChecklistItemId { get; set; }
    public ChecklistItem? ChecklistItem { get; set; }

    public ApplicationUser? User { get; set; }
}
