using ToolboxWeb.Web.Enums;

namespace ToolboxWeb.Web.Domain;

public class TabulatorTask
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public TabulatorTaskCategory Category { get; set; } = TabulatorTaskCategory.Ui;
    public TabulatorTaskStatus Status { get; set; } = TabulatorTaskStatus.Todo;
    public TabulatorTaskPriority Priority { get; set; } = TabulatorTaskPriority.Medium;
    public int Progress { get; set; }
    public decimal Budget { get; set; }
    public DateTime? DueDate { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ApplicationUser? User { get; set; }
}
