using ToolboxWeb.Web.Domain;

namespace ToolboxWeb.Web.ViewModels;

public class QuickLinksIndexViewModel
{
    public IReadOnlyList<QuickLink> Links { get; init; } = [];
    public QuickLinkFormViewModel Form { get; init; } = new();
}
