namespace ToolboxWeb.Web.Domain;

public class QuickLink
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? GroupName { get; set; }

    /// <summary>Position inside its group, set by dragging rows on the quick links page.</summary>
    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>How often the link was opened from ToolboxWeb, and when last (UTC).</summary>
    public int OpenCount { get; set; }
    public DateTime? LastOpenedAt { get; set; }

    /// <summary>Pinned links sit above the groups and come first on the overview.</summary>
    public bool IsPinned { get; set; }

    public ApplicationUser? User { get; set; }
}
