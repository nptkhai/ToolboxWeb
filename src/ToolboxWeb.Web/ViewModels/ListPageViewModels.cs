namespace ToolboxWeb.Web.ViewModels;

// Building blocks of the list page template (docs/LIST_PAGE_TEMPLATE.md), taken from Notes:
// _ListToolbar (summary, search, primary action) and _FilterChips. Pass already-localized text.

/// <summary>Input for Views/Shared/_ListToolbar.cshtml.</summary>
public sealed class ListToolbarViewModel
{
    /// <summary>Short totals line, for example "12 to-dos · 2 overdue".</summary>
    public string? Summary { get; init; }

    /// <summary>GET target of the search box; null hides the search box.</summary>
    public string? SearchAction { get; init; }

    public string? SearchValue { get; init; }

    /// <summary>Accessible name of the search box; also shown as its placeholder.</summary>
    public string SearchLabel { get; init; } = string.Empty;

    /// <summary>Query values the search keeps, such as the active filter chip.</summary>
    public IReadOnlyDictionary<string, string?> SearchKeep { get; init; } = new Dictionary<string, string?>();

    public string? PrimaryText { get; init; }

    /// <summary>Selector of the Bootstrap modal the primary button opens (and the N shortcut).</summary>
    public string? PrimaryModal { get; init; }

    public string PrimaryIcon { get; init; } = "plus";
}

/// <summary>Input for Views/Shared/_FilterChips.cshtml: single-choice chips that are links.</summary>
public sealed class FilterChipsViewModel
{
    public required string Label { get; init; }

    public IReadOnlyList<FilterChipViewModel> Chips { get; init; } = [];
}

public sealed record FilterChipViewModel(string Text, string Href, int? Count = null, bool Active = false);
