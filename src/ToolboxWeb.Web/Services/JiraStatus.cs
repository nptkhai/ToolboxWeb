namespace ToolboxWeb.Web.Services;

/// <summary>
/// Classifies free-text Jira status names into the three tones the UI colors by. Jira workflows
/// differ per project, so this matches on keywords instead of exact names.
/// </summary>
public static class JiraStatus
{
    public const string Done = "done";
    public const string Doing = "doing";
    public const string Todo = "todo";

    private static readonly string[] DoneHints =
    [
        "done",
        "closed",
        "resolved",
        "complete",
        "completed",
        "finished",
        "deployed"
    ];

    private static readonly string[] DoingHints =
    [
        "progress",
        "develop",
        "analysis",
        "review",
        "doing",
        "testing",
        "qa",
        "verify"
    ];

    public static bool IsDone(string? status)
    {
        return !string.IsNullOrWhiteSpace(status)
            && DoneHints.Any(hint => status.Contains(hint, StringComparison.OrdinalIgnoreCase));
    }

    public static string Tone(string? status)
    {
        if (IsDone(status))
        {
            return Done;
        }

        return !string.IsNullOrWhiteSpace(status)
            && DoingHints.Any(hint => status.Contains(hint, StringComparison.OrdinalIgnoreCase))
            ? Doing
            : Todo;
    }
}
