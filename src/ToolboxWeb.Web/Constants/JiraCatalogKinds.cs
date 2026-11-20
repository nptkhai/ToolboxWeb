namespace ToolboxWeb.Web.Constants;

/// <summary>The catalogs Toolkit keeps from Jira, in the order the catalog page lists them.</summary>
public static class JiraCatalogKinds
{
    public const string Projects = "projects";
    public const string Statuses = "statuses";
    public const string IssueTypes = "issuetypes";
    public const string SubTaskTypes = "subtasktypes";
    public const string Priorities = "priorities";
    public const string Resolutions = "resolutions";
    public const string Filters = "filters";
    public const string Users = "users";
    public const string Versions = "versions";
    public const string Config = "config";

    /// <summary>Not a catalog: the user's settings, stored next to them.</summary>
    public const string Settings = "settings";

    public static readonly IReadOnlyList<string> All =
        [Projects, Statuses, IssueTypes, SubTaskTypes, Priorities, Resolutions, Filters, Users, Versions, Config];

    /// <summary>What the first, automatic sync reads: everything but versions, which need chosen projects.</summary>
    public static readonly IReadOnlyList<string> FirstSync =
        [Projects, Statuses, IssueTypes, SubTaskTypes, Priorities, Resolutions, Filters, Users, Config];

    public static bool IsKnown(string? kind) => kind is not null && All.Contains(kind);
}
