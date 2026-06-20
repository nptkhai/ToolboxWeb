using System.ComponentModel.DataAnnotations;

namespace ToolboxWeb.Web.ViewModels;

public class NoteFormViewModel
{
    public int? Id { get; set; }

    [Required, StringLength(160)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Content { get; set; } = string.Empty;

    [StringLength(250)]
    public string? Tags { get; set; }

    public bool IsPinned { get; set; }
}
