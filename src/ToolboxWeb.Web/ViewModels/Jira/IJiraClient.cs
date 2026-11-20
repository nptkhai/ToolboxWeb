namespace ToolboxWeb.Web.ViewModels.Jira;

public interface IJiraClient : IDisposable
{
    string BaseUrl { get; }
    string Username { get; }
    Task LoginAsync(CancellationToken cancellationToken = default);
    Task LogoutAsync(CancellationToken cancellationToken = default);
    Task<string> GetCurrentUserDisplayNameAsync(CancellationToken cancellationToken = default);
    Task<List<JiraFilter>> GetFavouriteFiltersAsync(CancellationToken cancellationToken = default);
    Task<List<JiraUser>> GetUsersAsync(CancellationToken cancellationToken = default);
    Task<List<JiraIssue>> SearchIssuesAsync(string jql, bool includeWorklogs = true, CancellationToken cancellationToken = default);

    // Catalogs (reference data), read only when the user presses "Sync" (JiraCatalogService).
    Task<List<JiraCatalogProject>> GetProjectsAsync(CancellationToken cancellationToken = default);
    Task<List<JiraCatalogStatus>> GetStatusesAsync(CancellationToken cancellationToken = default);
    Task<List<JiraCatalogIssueType>> GetIssueTypesAsync(CancellationToken cancellationToken = default);
    Task<List<JiraCatalogItem>> GetPrioritiesAsync(CancellationToken cancellationToken = default);
    Task<List<JiraCatalogItem>> GetResolutionsAsync(CancellationToken cancellationToken = default);
    Task<List<JiraCatalogField>> GetFieldsAsync(CancellationToken cancellationToken = default);
    Task<JiraServerConfig> GetServerConfigAsync(CancellationToken cancellationToken = default);

    /// <summary>The values of the "Sub-task Type" field: the field's options plus the values seen on the user's subtasks.</summary>
    Task<List<string>> GetSubTaskTypesAsync(CancellationToken cancellationToken = default);

    Task<List<JiraCatalogVersion>> GetVersionsAsync(string projectKey, CancellationToken cancellationToken = default);

    /// <summary>Sets (or clears, with null) the due date of one issue.</summary>
    Task UpdateDueDateAsync(string issueKey, DateTime? dueDate, CancellationToken cancellationToken = default);
}
