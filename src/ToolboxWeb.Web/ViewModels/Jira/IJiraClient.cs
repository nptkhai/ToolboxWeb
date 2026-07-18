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
}
