namespace ToolboxWeb.Web.ViewModels.Jira;

public sealed class JiraDashboardStatViewModel
{
    public string Label { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public string Hint { get; init; } = string.Empty;
    public string AccentClass { get; init; } = "neutral";
}
