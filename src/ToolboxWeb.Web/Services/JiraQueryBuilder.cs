using ToolboxWeb.Web.ViewModels.Jira;

namespace ToolboxWeb.Web.Services;

public static class JiraQueryBuilder
{
    /// <summary>Checks the simple filters before a search; returns the message to show, or null.</summary>
    public static string? Validate(JiraSearchRequest request, string assigneeRequired, string dateOrder)
    {
        if (request.Advanced)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(request.Assignee))
        {
            return assigneeRequired;
        }

        return request.FromDate.Date > request.ToDate.Date ? dateOrder : null;
    }

    public static string Build(JiraSearchRequest request, string validationMessage)
    {
        if (request.Advanced)
        {
            return string.IsNullOrWhiteSpace(request.AdvancedJql)
                ? JiraDefaults.DefaultJql
                : request.AdvancedJql.Trim();
        }

        if (string.IsNullOrWhiteSpace(request.Assignee))
        {
            throw new InvalidOperationException(validationMessage);
        }

        var parts = new List<string>();
        AddEquals(parts, "project", request.Project);
        AddEquals(parts, "key", request.IssueKey);
        AddEquals(parts, "status", request.Status);
        AddEquals(parts, "\"Sub-task Type\"", request.SubTaskType);
        AddUser(parts, "assignee", request.Assignee);
        AddUser(parts, "reporter", request.Reporter);
        parts.Add($"due >= \"{request.FromDate:yyyy-MM-dd}\"");
        parts.Add($"due <= \"{request.ToDate:yyyy-MM-dd}\"");
        return string.Join(" AND ", parts) + " ORDER BY due DESC";
    }

    private static void AddEquals(List<string> parts, string field, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parts.Add($"{field} = {Quote(value)}");
        }
    }

    private static void AddUser(List<string> parts, string field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var text = value.Trim();
        parts.Add(text == "currentUser()" ? $"{field} = currentUser()" : $"{field} = {Quote(text)}");
    }

    /// <summary>A JQL string literal: quotes and backslashes in the value are escaped.</summary>
    public static string Quote(string value) =>
        "\"" + value.Trim().Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}
