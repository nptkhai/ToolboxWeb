using System.ComponentModel.DataAnnotations;
using ToolboxWeb.Web.Enums;

namespace ToolboxWeb.Web.ViewModels;

public class PromptTemplateFormViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Validation.Required"), StringLength(140, ErrorMessage = "Validation.MaxLength")]
    public string Name { get; set; } = string.Empty;

    public PromptCategory Category { get; set; } = PromptCategory.General;

    [StringLength(500, ErrorMessage = "Validation.MaxLength")]
    public string? Description { get; set; }

    [StringLength(1000, ErrorMessage = "Validation.MaxLength")]
    public string? InputVariables { get; set; }

    [StringLength(1000, ErrorMessage = "Validation.MaxLength")]
    public string? OutputFormat { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    public string Content { get; set; } = string.Empty;

    // The list the user was on, so saving returns to it.
    public string? ReturnCategory { get; set; }
    public string? ReturnSearch { get; set; }
}
