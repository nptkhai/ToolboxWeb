using System.ComponentModel.DataAnnotations;

namespace ToolboxWeb.Web.ViewModels;

public class ChecklistItemFormViewModel
{
    public int? Id { get; set; }

    [Required, StringLength(180)]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [DataType(DataType.Date)]
    public DateTime? DueDate { get; set; }
}
