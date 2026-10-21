using ToolboxWeb.Web.Domain;

namespace ToolboxWeb.Web.ViewModels;

public class ChecklistIndexViewModel
{
    public IReadOnlyList<ChecklistItem> Items { get; init; } = [];
    public ChecklistItemFormViewModel Form { get; init; } = new();

    /// <summary>One of <see cref="ChecklistFilters"/>.</summary>
    public string Filter { get; init; } = ChecklistFilters.All;
    public string? Search { get; init; }
    public ChecklistCounts Counts { get; init; } = new();

    /// <summary>False when the user has no to-dos at all, as opposed to none matching the filter.</summary>
    public bool HasAnyItems { get; init; }

    /// <summary>Reopens the create modal after a failed submit so the validation messages show.</summary>
    public bool OpenCreateForm { get; init; }

    /// <summary>Today in the app time zone, for the overdue and due-today marks.</summary>
    public DateTime Today { get; init; }
}

public sealed class ChecklistCounts
{
    public int All { get; init; }
    public int Todo { get; init; }
    public int Overdue { get; init; }
    public int Done { get; init; }
}

public sealed record ChecklistListResult(
    IReadOnlyList<ChecklistItem> Items,
    ChecklistCounts Counts,
    bool HasAnyItems,
    string Filter,
    string? Search);

/// <summary>Filter chip values of the to-do list; they appear in the query string.</summary>
public static class ChecklistFilters
{
    public const string All = "all";
    public const string Todo = "todo";
    public const string Overdue = "overdue";
    public const string Done = "done";

    public static string Normalize(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        Todo => Todo,
        Overdue => Overdue,
        Done => Done,
        _ => All
    };
}
