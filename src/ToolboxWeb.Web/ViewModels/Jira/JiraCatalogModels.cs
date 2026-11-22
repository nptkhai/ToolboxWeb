namespace ToolboxWeb.Web.ViewModels.Jira;

// Reference data read from Jira for the catalogs (Services/JiraCatalogService.cs). Plain records,
// serialised as they are into the stored catalog (camelCase JSON).

/// <param name="Category">Jira's project category (EMS.MAINT, EMS.ONGOING…); empty when none.</param>
/// <param name="Lead">Display name of the project lead.</param>
public sealed record JiraCatalogProject(string Key, string Name, string Category, string Type, string Lead);

/// <param name="Category">Jira's status category key: "new", "indeterminate", "done" or "undefined".</param>
public sealed record JiraCatalogStatus(string Id, string Name, string Category);

public sealed record JiraCatalogIssueType(string Id, string Name, bool Subtask);

/// <summary>A priority or a resolution: an id and a name.</summary>
public sealed record JiraCatalogItem(string Id, string Name);

/// <param name="SchemaCustom">The custom field type ("…:select", "…:gh-epic-link"); empty for system fields.</param>
public sealed record JiraCatalogField(string Id, string Name, bool Custom, string SchemaCustom);

/// <param name="ReleaseDate">yyyy-MM-dd, or null when not set.</param>
public sealed record JiraCatalogVersion(string Name, bool Released, bool Archived, string? ReleaseDate);

/// <summary>Time tracking settings and server facts from /configuration and /serverInfo.</summary>
public sealed class JiraServerConfig
{
    public decimal HoursPerDay { get; init; } = 8m;
    public decimal DaysPerWeek { get; init; } = 5m;
    public string TimeFormat { get; init; } = string.Empty;
    public string DefaultUnit { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;

    /// <summary>Id of the "Sub-task Type" custom field, when the server has one.</summary>
    public string? SubTaskTypeField { get; init; }
}
