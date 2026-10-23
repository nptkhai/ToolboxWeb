using ToolboxWeb.Web.Domain;
using ToolboxWeb.Web.Enums;

namespace ToolboxWeb.Web.ViewModels;

/// <summary>Filter, search and sort of the prompt list; all three live in the URL.</summary>
public sealed record PromptListQuery(PromptCategory? Category, string? Search, string Sort);

/// <summary>List orders of the prompt page; pinned prompts stay on top in each.</summary>
public static class PromptSorts
{
    public const string Name = "name";
    public const string MostUsed = "used";
    public const string RecentlyUsed = "recent";

    public static string? Normalize(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        Name => Name,
        MostUsed => MostUsed,
        RecentlyUsed => RecentlyUsed,
        _ => null
    };
}

public class PromptTemplatesIndexViewModel
{
    public PromptListQuery Query { get; init; } = new(null, null, PromptSorts.Name);

    /// <summary>The prompts that match the filter and search, pinned first.</summary>
    public IReadOnlyList<PromptTemplate> Templates { get; init; } = [];

    /// <summary>Every prompt the user has, before any filter.</summary>
    public int Total { get; init; }

    public int PinnedCount { get; init; }

    /// <summary>Prompts per category, after the search; drives the filter chips.</summary>
    public IReadOnlyDictionary<PromptCategory, int> CategoryCounts { get; init; } = new Dictionary<PromptCategory, int>();

    public int SearchCount { get; init; }

    public PromptTemplateFormViewModel Form { get; set; } = new();

    /// <summary>Reopen the form dialog, after a failed save or when the page was opened to edit.</summary>
    public bool OpenForm { get; set; }

    public bool IsEditing => Form.Id.HasValue;

    /// <summary>The built-in prompts and how the user's copy compares (the "Restore built-in prompts" dialog).</summary>
    public IReadOnlyList<PromptSeedStatus> Seeds { get; set; } = [];
}

public enum PromptSeedState
{
    /// <summary>The user has no prompt with this name.</summary>
    Missing,

    /// <summary>The user has it, with a text other than the current built-in one.</summary>
    Changed,

    /// <summary>The user has the current built-in text.</summary>
    Current
}

public sealed record PromptSeedStatus(string Key, string Name, PromptSeedState State);

public sealed record PromptRestoreResult(int Added, int Replaced);

/// <summary>The JSON file "Export" writes and "Import" reads.</summary>
public sealed class PromptExportFile
{
    public const string FormatName = "toolboxweb.prompts";

    public string Format { get; set; } = FormatName;
    public int Version { get; set; } = 1;
    public DateTime ExportedAt { get; set; }
    public List<PromptExportItem> Prompts { get; set; } = [];
}

public sealed class PromptExportItem
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = nameof(PromptCategory.General);
    public string? Description { get; set; }
    public string? InputVariables { get; set; }
    public string? OutputFormat { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsPinned { get; set; }
}

public sealed record PromptImportResult(int Created, int Skipped);
