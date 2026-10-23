using System.ComponentModel.DataAnnotations;

namespace ToolboxWeb.Web.ViewModels;

public class QuickLinkFormViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Validation.Required"), StringLength(120, ErrorMessage = "Validation.MaxLength")]
    public string Title { get; set; } = string.Empty;

    /// <summary>Checked by QuickLinkUrl after "https://" is added to an address typed without one.</summary>
    [Required(ErrorMessage = "Validation.Required"), StringLength(1000, ErrorMessage = "Validation.MaxLength")]
    public string Url { get; set; } = string.Empty;

    [StringLength(80, ErrorMessage = "Validation.MaxLength")]
    public string? GroupName { get; set; }

    // The list the user was on, so saving returns to it.
    public string? ReturnGroup { get; set; }
    public string? ReturnSearch { get; set; }
}
