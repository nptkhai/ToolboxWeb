namespace ToolboxWeb.Web.ViewModels.Jira;

public interface IJiraClientFactory
{
    IJiraClient Create(string baseUrl, string username, string password);

    /// <summary>A client that signs in with a personal access token (Jira Server / Data Center 8.14+).</summary>
    IJiraClient CreateWithToken(string baseUrl, string token);
}
