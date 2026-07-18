namespace ToolboxWeb.Web.ViewModels.Jira;

public interface IJiraClientFactory
{
    IJiraClient Create(string baseUrl, string username, string password);
}
