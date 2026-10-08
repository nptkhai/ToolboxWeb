using ToolboxWeb.Web.Enums;

namespace ToolboxWeb.Web.ViewModels;

/// <summary>
/// Presentation facts about one <see cref="NoteFormat"/>: the short slug used in URLs and
/// badges (".js"), its label key, and how its content should be edited and downloaded.
/// </summary>
public sealed record NoteFormatInfo(
    NoteFormat Format,
    string Slug,
    string LabelKey,
    bool IsCode,
    string DownloadExtension,
    string MimeType)
{
    /// <summary>Colour hook in notes.css.</summary>
    public string CssClass => "nv-fmt-" + Slug;

    public string Badge => "." + Slug;
}

public static class NoteFormatCatalog
{
    public static readonly IReadOnlyList<NoteFormatInfo> All =
    [
        new(NoteFormat.Text, "txt", "Notes.Format.Text", false, "txt", "text/plain"),
        new(NoteFormat.JavaScript, "js", "Notes.Format.JavaScript", true, "js", "text/javascript"),
        new(NoteFormat.Css, "css", "Notes.Format.Css", true, "css", "text/css"),
        new(NoteFormat.Json, "json", "Notes.Format.Json", true, "json", "application/json"),
        new(NoteFormat.Xml, "xml", "Notes.Format.Xml", true, "xml", "application/xml"),
        new(NoteFormat.Link, "url", "Notes.Format.Link", false, "txt", "text/plain"),
        new(NoteFormat.Server, "srv", "Notes.Format.Server", false, "txt", "text/plain"),
        new(NoteFormat.Checklist, "chk", "Notes.Format.Checklist", false, "txt", "text/plain"),
        new(NoteFormat.Mixed, "mix", "Notes.Format.Mixed", false, "txt", "text/plain")
    ];

    /// <summary>The formats a sub-note of a Mixed group may take; a group cannot nest another group.</summary>
    public static IEnumerable<NoteFormatInfo> ChildFormats => All.Where(x => x.Format != NoteFormat.Mixed);

    public static NoteFormatInfo Get(NoteFormat format) =>
        All.FirstOrDefault(x => x.Format == format) ?? All[0];

    public static NoteFormatInfo? FindBySlug(string? slug) =>
        string.IsNullOrWhiteSpace(slug)
            ? null
            : All.FirstOrDefault(x => string.Equals(x.Slug, slug.Trim(), StringComparison.OrdinalIgnoreCase));
}
