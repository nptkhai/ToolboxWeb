using ToolboxWeb.Web.Domain;

namespace ToolboxWeb.Web.ViewModels;

public class NotesIndexViewModel
{
    public string? Search { get; init; }
    public IReadOnlyList<Note> Notes { get; init; } = [];
    public NoteFormViewModel Form { get; init; } = new();
}
