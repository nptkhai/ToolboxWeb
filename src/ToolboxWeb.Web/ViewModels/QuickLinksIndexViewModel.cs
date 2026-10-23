using ToolboxWeb.Web.Domain;

namespace ToolboxWeb.Web.ViewModels;

/// <summary>Group filter, search and sort of the quick links page; all three live in the URL.</summary>
public sealed record QuickLinkListQuery(string? Group, string? Search, string Sort);

/// <summary>List orders; "manual" is the order set by dragging rows.</summary>
public static class QuickLinkSorts
{
    public const string Manual = "manual";
    public const string Name = "name";
    public const string MostOpened = "opened";

    /// <summary>The group filter value for links without a group.</summary>
    public const string NoGroup = "_none";

    public static string? Normalize(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        Manual => Manual,
        Name => Name,
        MostOpened => MostOpened,
        _ => null
    };
}

/// <summary>One block of the list: the pinned links, a named group, or the links without a group.</summary>
public sealed record QuickLinkSection(string Key, string? GroupName, bool Pinned, IReadOnlyList<QuickLink> Links);

public sealed record QuickLinkGroupCount(string Key, string? GroupName, int Count);

public class QuickLinksIndexViewModel
{
    public QuickLinkListQuery Query { get; init; } = new(null, null, QuickLinkSorts.Manual);
    public IReadOnlyList<QuickLinkSection> Sections { get; init; } = [];

    /// <summary>Every link the user has, before any filter.</summary>
    public int Total { get; init; }

    public int PinnedCount { get; init; }

    /// <summary>Links per group after the search, in display order; drives the filter chips.</summary>
    public IReadOnlyList<QuickLinkGroupCount> Groups { get; init; } = [];

    public int SearchCount { get; init; }

    /// <summary>Every group name in use, for the group box of the form.</summary>
    public IReadOnlyList<string> GroupNames { get; init; } = [];

    public QuickLinkFormViewModel Form { get; set; } = new();
    public bool OpenForm { get; set; }
    public bool IsEditing => Form.Id.HasValue;

    /// <summary>Rows can be dragged only in the manual order without a search.</summary>
    public bool CanReorder => Query.Sort == QuickLinkSorts.Manual && string.IsNullOrWhiteSpace(Query.Search);
}

public sealed record QuickLinkImportResult(int Created, int Skipped);
