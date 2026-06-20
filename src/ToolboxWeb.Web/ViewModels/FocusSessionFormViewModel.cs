using System.ComponentModel.DataAnnotations;

namespace ToolboxWeb.Web.ViewModels;

public class FocusSessionFormViewModel
{
    [Range(1, 240)]
    public int DurationMinutes { get; set; } = 25;

    [StringLength(500)]
    public string? Note { get; set; }
}
