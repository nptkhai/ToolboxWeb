namespace ToolboxWeb.Web.ViewModels.Jira;

public sealed class JiraSearchRequest
{
    public bool Advanced { get; set; }
    public string? AdvancedJql { get; set; }
    public string? Project { get; set; }
    public string? IssueKey { get; set; }
    public string? Status { get; set; }
    public string? Assignee { get; set; }
    public string? Reporter { get; set; }
    public DateTime FromDate { get; set; } = DateTime.Today;
    public DateTime ToDate { get; set; } = DateTime.Today;
}
