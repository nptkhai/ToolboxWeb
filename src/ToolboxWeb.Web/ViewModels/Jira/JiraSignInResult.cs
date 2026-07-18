namespace ToolboxWeb.Web.ViewModels.Jira;

public sealed class JiraSignInResult
{
    public bool Succeeded { get; init; }
    public string Message { get; init; } = string.Empty;
    public JiraSession? Session { get; init; }
}
