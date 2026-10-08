using ToolboxWeb.Web.Enums;

namespace ToolboxWeb.Web.Domain;

public class Note
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? Tags { get; set; }
    public bool IsPinned { get; set; }
    public NoteFormat Format { get; set; } = NoteFormat.Text;

    /// <summary>
    /// Null for a top-level note. Set for a sub-note; nesting is one level deep only, so a
    /// sub-note never has children of its own.
    /// </summary>
    public int? ParentId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Only shown when <see cref="Format"/> is Server. Kept rather than cleared when the format
    /// changes, so switching a note's format by mistake does not destroy stored credentials.
    /// </summary>
    public NoteServerConnection Server { get; set; } = new();

    public ApplicationUser? User { get; set; }
    public Note? Parent { get; set; }
    public List<Note> Children { get; set; } = [];
}
