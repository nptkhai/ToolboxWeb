using ToolboxWeb.Web.Domain;

namespace ToolboxWeb.Web.ViewModels;

public class PromptTemplatesIndexViewModel
{
    public IReadOnlyList<PromptTemplate> Templates { get; init; } = [];
    public PromptTemplateFormViewModel Form { get; init; } = new();
}
