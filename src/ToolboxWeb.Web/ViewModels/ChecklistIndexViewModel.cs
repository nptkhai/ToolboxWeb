using ToolboxWeb.Web.Domain;

namespace ToolboxWeb.Web.ViewModels;

public class ChecklistIndexViewModel
{
    public IReadOnlyList<ChecklistItem> Items { get; init; } = [];
    public ChecklistItemFormViewModel Form { get; init; } = new();
}
