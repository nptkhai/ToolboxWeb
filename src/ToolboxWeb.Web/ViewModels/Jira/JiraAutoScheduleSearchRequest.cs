namespace ToolboxWeb.Web.ViewModels.Jira;

public sealed class JiraAutoScheduleSearchRequest
{
    public string? Project { get; init; }
    public string? Status { get; init; } = "all";
    public string? Text { get; init; }
    public DateTime? DueFrom { get; init; }
    public DateTime? DueTo { get; init; }
}
