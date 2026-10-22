using System.ComponentModel.DataAnnotations;
using ToolboxWeb.Web.Enums;

namespace ToolboxWeb.Web.ViewModels;

public class ChecklistItemFormViewModel
{
    /// <summary>Set when the form edits an existing to-do.</summary>
    public int? Id { get; set; }

    [Required(ErrorMessage = "Validation.Required"), StringLength(180, ErrorMessage = "Validation.MaxLength")]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "Validation.MaxLength")]
    public string? Description { get; set; }

    [DataType(DataType.Date)]
    public DateTime? DueDate { get; set; }

    public ChecklistPriority Priority { get; set; } = ChecklistPriority.Normal;
}
