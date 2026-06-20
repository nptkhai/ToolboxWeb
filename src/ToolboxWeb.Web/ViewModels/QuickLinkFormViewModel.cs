using System.ComponentModel.DataAnnotations;

namespace ToolboxWeb.Web.ViewModels;

public class QuickLinkFormViewModel
{
    public int? Id { get; set; }

    [Required, StringLength(120)]
    public string Title { get; set; } = string.Empty;

    [Required, Url, StringLength(1000)]
    public string Url { get; set; } = string.Empty;

    [StringLength(80)]
    public string? GroupName { get; set; }

    public int SortOrder { get; set; }
}
