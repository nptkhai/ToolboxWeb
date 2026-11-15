using ToolboxWeb.Web.ViewModels.Jira;

namespace ToolboxWeb.Web.Infrastructure.Jira;

public sealed class JiraClientFactory : IJiraClientFactory
{
    public IJiraClient Create(string baseUrl, string username, string password)
    {
        return new JiraClient(baseUrl, username, password);
    }

    public IJiraClient CreateWithToken(string baseUrl, string token)
    {
        return JiraClient.WithToken(baseUrl, token);
    }
}
