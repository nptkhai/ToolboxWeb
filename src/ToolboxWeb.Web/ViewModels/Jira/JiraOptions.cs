namespace ToolboxWeb.Web.ViewModels.Jira;

public class JiraOptions
{
    public string DefaultBaseUrl { get; set; } = "https://task.ascvn.com.vn";
    public int SessionTimeoutHours { get; set; } = 4;
}
