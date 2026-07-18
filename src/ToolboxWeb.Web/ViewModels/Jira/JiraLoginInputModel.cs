namespace ToolboxWeb.Web.ViewModels.Jira;

public sealed class JiraLoginInputModel
{
    public string BaseUrl { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
