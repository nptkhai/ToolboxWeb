using System.ComponentModel.DataAnnotations;

namespace ToolboxWeb.Web.ViewModels;

/// <summary>The one-line "add a task" form on the dashboard.</summary>
public class DashboardQuickTaskViewModel
{
    [Required(ErrorMessage = "Validation.Required"), StringLength(180, ErrorMessage = "Validation.MaxLength")]
    public string Title { get; set; } = string.Empty;

    /// <summary>"today" gives the task today's date; anything else leaves it without one.</summary>
    public string? Due { get; set; } = "today";
}
