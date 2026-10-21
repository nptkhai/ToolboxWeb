using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Localization;
using ToolboxWeb.Web.ViewModels;

namespace ToolboxWeb.Web.Infrastructure.TagHelpers;

/// <summary>
/// Status pill of one component hub entry: <c>&lt;tbx-component-status key="kendo-datepicker" /&gt;</c>.
/// Reads <see cref="ComponentCatalog"/>, so demo cards and the catalog tab always agree.
/// The <c>data-status-badge</c> attribute lets the page flag a widget that failed to load.
/// </summary>
[HtmlTargetElement("tbx-component-status", TagStructure = TagStructure.WithoutEndTag)]
public sealed class ComponentStatusTagHelper(IStringLocalizer<SharedResource> localizer) : TagHelper
{
    public string Key { get; set; } = string.Empty;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var status = ComponentCatalog.Get(Key).Status;
        output.TagName = "span";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", $"tbx-pill {ComponentCatalog.StatusTone(status)}");
        output.Attributes.SetAttribute("data-status-badge", null);
        output.Content.SetContent(localizer[ComponentCatalog.StatusLabelKey(status)].Value);
    }
}
