namespace ToolboxWeb.Web.ViewModels.Jira;

public sealed class WorklogEntry
{
    public DateTime Started { get; set; }
    public string TimeSpent { get; set; } = string.Empty;
}
