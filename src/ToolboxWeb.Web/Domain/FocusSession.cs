namespace ToolboxWeb.Web.Domain;

public class FocusSession
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
    public string? Note { get; set; }

    public ApplicationUser? User { get; set; }
}
