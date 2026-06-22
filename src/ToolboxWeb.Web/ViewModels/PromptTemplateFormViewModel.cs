using System.ComponentModel.DataAnnotations;
using ToolboxWeb.Web.Enums;

namespace ToolboxWeb.Web.ViewModels;

public class PromptTemplateFormViewModel
{
    public int? Id { get; set; }

    [Required, StringLength(140)]
    public string Name { get; set; } = string.Empty;

    public PromptCategory Category { get; set; } = PromptCategory.General;

    [StringLength(500)]
    public string? Description { get; set; }

    [StringLength(1000)]
    public string? InputVariables { get; set; }

    [StringLength(1000)]
    public string? OutputFormat { get; set; }

    [Required]
    public string Content { get; set; } = string.Empty;
}
