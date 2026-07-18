namespace ToolboxWeb.Web.ViewModels.Jira;

public sealed class LocalLoginInputModel
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool RememberMe { get; set; }
}
