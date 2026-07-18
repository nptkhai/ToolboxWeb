namespace ToolboxWeb.Web.ViewModels.Jira;

public sealed class JiraSession
{
    public string BrowserSessionId { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public IJiraClient Client { get; init; } = default!;
    public DateTime LoginAt { get; init; }
    public DateTime ExpiresAt { get; init; }
}
