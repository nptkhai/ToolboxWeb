using ToolboxWeb.Web.Enums;

namespace ToolboxWeb.Web.Domain;

public class PromptTemplate
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public PromptCategory Category { get; set; } = PromptCategory.General;
    public string? Description { get; set; }
    public string Content { get; set; } = string.Empty;
    public int UseCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }

    public ApplicationUser? User { get; set; }
}
