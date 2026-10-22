using ToolboxWeb.Web.Domain;

namespace ToolboxWeb.Web.ViewModels;

public class ChecklistIndexViewModel
{
    /// <summary>The items on screen, grouped by when they are due (done items last).</summary>
    public IReadOnlyList<ChecklistGroupViewModel> Groups { get; init; } = [];
    public ChecklistItemFormViewModel Form { get; init; } = new();

    public ChecklistListQuery Query { get; init; } = new(ChecklistFilters.All, null, ChecklistSorts.Due);
    public ChecklistCounts Counts { get; init; } = new();
    public ChecklistWeekProgress Week { get; init; } = new();

    /// <summary>False when the user has no to-dos at all, as opposed to none matching the filter.</summary>
    public bool HasAnyItems { get; init; }

    /// <summary>Reopens the form modal after a failed submit so the validation messages show.</summary>
    public bool OpenForm { get; init; }

    /// <summary>Today in the app time zone, for the overdue and due-today marks.</summary>
    public DateTime Today { get; init; }

    /// <summary>The last complete or delete, offered back as "Undo" until the next action.</summary>
    public ChecklistUndoViewModel? Undo { get; init; }

    public int ItemCount => Groups.Sum(x => x.Items.Count);
}

public enum ChecklistGroup
{
    Overdue,
    Today,
    Upcoming,
    NoDueDate,
    Done
}

public sealed class ChecklistGroupViewModel
{
    public ChecklistGroup Group { get; init; }
    public IReadOnlyList<ChecklistItem> Items { get; init; } = [];
}

public sealed class ChecklistCounts
{
    public int All { get; init; }
    public int Todo { get; init; }
    public int Today { get; init; }
    public int Overdue { get; init; }
    public int Done { get; init; }
}

/// <summary>Done this week against what was due by the end of it (overdue included).</summary>
public sealed class ChecklistWeekProgress
{
    public int Done { get; init; }
    public int Planned { get; init; }
}

/// <summary>Filter chip, search text and sort order; they live in the query string.</summary>
public sealed record ChecklistListQuery(string Filter, string? Search, string Sort);

public sealed record ChecklistListResult(
    IReadOnlyList<ChecklistGroupViewModel> Groups,
    ChecklistCounts Counts,
    ChecklistWeekProgress Week,
    bool HasAnyItems,
    ChecklistListQuery Query);

/// <summary>What "Undo" reverses: marking items done, or deleting them.</summary>
public sealed record ChecklistUndoViewModel(string Kind, IReadOnlyList<int> Ids, string Message);

public static class ChecklistUndoKinds
{
    public const string Complete = "complete";
    public const string Delete = "delete";
}

/// <summary>Filter chip values of the to-do list.</summary>
public static class ChecklistFilters
{
    public const string All = "all";
    public const string Todo = "todo";
    public const string Today = "today";
    public const string Overdue = "overdue";
    public const string Done = "done";

    public static string Normalize(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        Todo => Todo,
        Today => Today,
        Overdue => Overdue,
        Done => Done,
        _ => All
    };
}

/// <summary>Sort orders inside each due group.</summary>
public static class ChecklistSorts
{
    public const string Due = "due";
    public const string Created = "created";
    public const string Priority = "priority";

    public static string? Normalize(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        Due => Due,
        Created => Created,
        Priority => Priority,
        _ => null
    };
}

/// <summary>Quick due-date choices on a row.</summary>
public static class ChecklistDueShortcuts
{
    public const string Today = "today";
    public const string Tomorrow = "tomorrow";
    public const string NextWeek = "next-week";
    public const string None = "none";
}
