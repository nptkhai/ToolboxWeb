namespace ToolboxWeb.Web.ViewModels;

/// <summary>
/// One state a checklist row can be in. The slug is written into the note's text, so slugs must
/// stay as they are; the colour lives in notes.css under <see cref="CssClass"/>. Every state
/// also carries a name, so the list still reads correctly in black and white.
/// </summary>
public sealed record NoteTaskStatusInfo(string Slug, string LabelKey)
{
    public string CssClass => "nv-st-" + Slug;
}

public static class NoteTaskStatusCatalog
{
    public static readonly IReadOnlyList<NoteTaskStatusInfo> All =
    [
        new("todo", "Notes.Task.Status.Todo"),
        new("doing", "Notes.Task.Status.Doing"),
        new("review", "Notes.Task.Status.Review"),
        new("wait", "Notes.Task.Status.Wait"),
        new("high", "Notes.Task.Status.High"),
        new("done", "Notes.Task.Status.Done"),
        new("drop", "Notes.Task.Status.Drop")
    ];

    /// <summary>What a row with no state of its own gets.</summary>
    public static NoteTaskStatusInfo Default => All[0];

    /// <summary>The state counted as finished, for "3/7 done".</summary>
    public const string DoneSlug = "done";

    public static NoteTaskStatusInfo Get(string? slug) =>
        All.FirstOrDefault(x => string.Equals(x.Slug, slug?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? Default;
}
