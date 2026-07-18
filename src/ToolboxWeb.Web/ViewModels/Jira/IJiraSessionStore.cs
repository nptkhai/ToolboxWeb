namespace ToolboxWeb.Web.ViewModels.Jira;

public interface IJiraSessionStore
{
    Task<JiraSession?> GetAsync(string browserSessionId);
    Task<JiraSession> CreateAsync(string browserSessionId, string baseUrl, string username, string password);
    Task<bool> IsActiveAsync(string browserSessionId);
    Task InvalidateAsync(string browserSessionId);
}
