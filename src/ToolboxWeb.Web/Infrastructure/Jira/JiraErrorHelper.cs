namespace ToolboxWeb.Web.Infrastructure.Jira;

public static class JiraErrorHelper
{
    public static string ToUserMessage(
        Exception exception,
        string fallback,
        string? sessionExpired = null,
        string? notFound = null,
        string? invalidRequest = null)
    {
        var message = exception.Message ?? string.Empty;

        if (message.Contains("401", StringComparison.OrdinalIgnoreCase)
            || message.Contains("403", StringComparison.OrdinalIgnoreCase)
            || message.Contains("xác thực", StringComparison.OrdinalIgnoreCase))
        {
            return sessionExpired ?? fallback;
        }

        if (message.Contains("404", StringComparison.OrdinalIgnoreCase))
        {
            return notFound ?? fallback;
        }

        if (message.Contains("400", StringComparison.OrdinalIgnoreCase))
        {
            return invalidRequest ?? fallback;
        }

        return fallback;
    }
}
