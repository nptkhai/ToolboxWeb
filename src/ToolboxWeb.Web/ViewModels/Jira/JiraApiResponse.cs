namespace ToolboxWeb.Web.ViewModels.Jira;

public sealed class JiraApiResponse<T>
{
    public bool Success { get; init; }
    public bool RequiresLogin { get; init; }
    public string Message { get; init; } = string.Empty;
    public T? Data { get; init; }

    public static JiraApiResponse<T> Ok(T data, string message = "") => new()
    {
        Success = true,
        Data = data,
        Message = message
    };

    public static JiraApiResponse<T> Fail(string message) => new()
    {
        Success = false,
        Message = message
    };

    public static JiraApiResponse<T> LoginRequired(string message) => new()
    {
        Success = false,
        RequiresLogin = true,
        Message = message
    };
}
