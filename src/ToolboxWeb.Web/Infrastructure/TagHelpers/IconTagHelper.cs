using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace ToolboxWeb.Web.Infrastructure.TagHelpers;

/// <summary>
/// Renders one Lucide icon from the local sprite: <c>&lt;tbx-icon name="search" /&gt;</c>.
/// Icons are decorative by default (aria-hidden); pass <c>label</c> for an icon that stands alone.
/// The sprite only holds the icons listed in tools/lucide/icons.txt.
/// </summary>
[HtmlTargetElement("tbx-icon", TagStructure = TagStructure.WithoutEndTag)]
public sealed class IconTagHelper(IFileVersionProvider fileVersionProvider) : TagHelper
{
    private const string SpritePath = "/vendor/lucide/lucide-sprite.svg";

    public string Name { get; set; } = string.Empty;

    public string? Label { get; set; }

    public int Size { get; set; } = 16;

    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var request = ViewContext.HttpContext.Request;
        var sprite = fileVersionProvider.AddFileVersionToPath(request.PathBase, SpritePath);
        var href = $"{request.PathBase}{sprite}#{Name}";

        output.TagName = "svg";
        output.TagMode = TagMode.StartTagAndEndTag;

        var existingClass = output.Attributes["class"]?.Value?.ToString();
        output.Attributes.SetAttribute("class", string.IsNullOrWhiteSpace(existingClass) ? "tbx-icon" : $"tbx-icon {existingClass}");
        output.Attributes.SetAttribute("width", Size);
        output.Attributes.SetAttribute("height", Size);
        output.Attributes.SetAttribute("focusable", "false");

        if (string.IsNullOrWhiteSpace(Label))
        {
            output.Attributes.SetAttribute("aria-hidden", "true");
        }
        else
        {
            output.Attributes.SetAttribute("role", "img");
            output.Attributes.SetAttribute("aria-label", Label);
        }

        output.Content.SetHtmlContent(new HtmlString($"<use href=\"{System.Net.WebUtility.HtmlEncode(href)}\"></use>"));
    }
}
