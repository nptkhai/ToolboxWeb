namespace ToolboxWeb.Web.ViewModels.Jira;

/// <summary>Input for Views/Jira/_JiraJqlBlock.cshtml.</summary>
public sealed class JiraJqlBlockViewModel
{
    /// <summary>Id of the code element, unique on the page; the copy button targets it.</summary>
    public required string Id { get; init; }

    public string? Jql { get; init; }

    /// <summary>Jira base URL for the "Open in Jira" link; the link is hidden without it.</summary>
    public string? BrowseBaseUrl { get; init; }

    public string SearchUrl => string.IsNullOrWhiteSpace(BrowseBaseUrl) || string.IsNullOrWhiteSpace(Jql)
        ? string.Empty
        : $"{BrowseBaseUrl.TrimEnd('/')}/issues/?jql={Uri.EscapeDataString(Jql)}";
}
