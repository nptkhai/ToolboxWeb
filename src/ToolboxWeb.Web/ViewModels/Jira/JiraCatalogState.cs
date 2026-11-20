using System.Text.Json.Nodes;

namespace ToolboxWeb.Web.ViewModels.Jira;

/// <summary>
/// The catalogs of one user on one Jira server, as the catalog page and the Jira screens read
/// them (GET /JiraCatalog/State, POST /JiraCatalog/Sync). On the web copy the browser keeps them
/// (StoredInBrowser) and the server only answers with what it just read.
/// </summary>
public sealed class JiraCatalogState
{
    public string BaseUrl { get; init; } = string.Empty;

    /// <summary>True on the web copy: catalogs live in this browser's storage, not on the server.</summary>
    public bool StoredInBrowser { get; init; }

    public Dictionary<string, JiraCatalogEntryState> Catalogs { get; init; } = new(StringComparer.Ordinal);

    public JiraCatalogSettings Settings { get; init; } = new();
}

public sealed class JiraCatalogEntryState
{
    public int Count { get; init; }

    /// <summary>UTC time it was read from Jira.</summary>
    public DateTime SyncedAt { get; init; }

    /// <summary>The items, as JSON (array, or object for config and versions).</summary>
    public JsonNode? Data { get; init; }

    /// <summary>Set when this catalog could not be read in the last sync; the old items are kept.</summary>
    public string? Error { get; init; }
}

/// <summary>The user's own choices next to the catalogs.</summary>
public sealed class JiraCatalogSettings
{
    /// <summary>Statuses that count as in progress but never as late.</summary>
    public List<string> WaitingStatuses { get; set; } = [];

    /// <summary>Projects whose versions the Versions catalog reads.</summary>
    public List<string> VersionProjects { get; set; } = [];
}

/// <summary>Body of POST /JiraCatalog/Sync.</summary>
public sealed class JiraCatalogSyncRequest
{
    public List<string> Kinds { get; set; } = [];

    /// <summary>Projects for the Versions catalog (the web copy sends them from the browser).</summary>
    public List<string>? VersionProjects { get; set; }

    /// <summary>The automatic first sync: no activity log line and quieter errors.</summary>
    public bool First { get; set; }
}
