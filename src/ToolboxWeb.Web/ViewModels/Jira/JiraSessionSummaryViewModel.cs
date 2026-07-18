namespace ToolboxWeb.Web.ViewModels.Jira;

public sealed class JiraSessionSummaryViewModel
{
    public string DisplayName { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = string.Empty;
    public DateTime LoginAt { get; init; }
    public DateTime ExpiresAt { get; init; }
}
