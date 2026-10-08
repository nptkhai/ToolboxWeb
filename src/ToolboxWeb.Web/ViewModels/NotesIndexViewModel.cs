namespace ToolboxWeb.Web.ViewModels;

public class NotesIndexViewModel
{
    public string? Search { get; init; }

    /// <summary>The format filter in effect, or null for all formats.</summary>
    public NoteFormatInfo? ActiveFormat { get; init; }

    /// <summary>Every note the user owns, sub-notes included.</summary>
    public int TotalCount { get; init; }

    /// <summary>How many formats at least one group uses.</summary>
    public int FormatsInUse { get; init; }

    /// <summary>Groups matching the search, whatever their format: the "All" chip.</summary>
    public int MatchingCount { get; init; }

    public IReadOnlyList<NoteFormatCount> FormatCounts { get; init; } = [];
    public IReadOnlyList<NoteListItemViewModel> Items { get; init; } = [];
    public NoteDetailViewModel? Selected { get; init; }

    /// <summary>
    /// True when the URL named a note. The first note is shown on wide screens anyway, but a
    /// phone only switches from the list to the detail when the user actually picked one.
    /// </summary>
    public bool HasExplicitSelection { get; init; }

    /// <summary>Sub-note to open and scroll to after a save.</summary>
    public int? FocusChildId { get; init; }

    /// <summary>State of the "new note" dialog, refilled when a submission fails validation.</summary>
    public NoteFormViewModel Form { get; set; } = new();
    public bool OpenCreateModal { get; set; }

    public bool HasAnyNotes => TotalCount > 0;
    public bool IsFiltered => !string.IsNullOrWhiteSpace(Search) || ActiveFormat is not null;
}
