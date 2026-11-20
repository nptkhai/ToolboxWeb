namespace ToolboxWeb.Web.Services;

/// <summary>
/// The three tones the UI colors a Jira status by. Jira puts every status in a category
/// ("new", "indeterminate", "done"), which is used whenever it is known; the keyword match on the
/// name is only a fallback for issues read without it.
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

    /// <summary>The tone of a status from Jira's category; falls back to the name when there is none.</summary>
    public static string Tone(string? status, string? category)
    {
        return category?.Trim().ToLowerInvariant() switch
        {
            "done" => Done,
            "indeterminate" => Doing,
            "new" or "undefined" => Todo,
            _ => Tone(status)
        };
    }

    public static bool IsDone(string? status, string? category) => Tone(status, category) == Done;

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

    /// <summary>
    /// The <c>tbx-pill</c> tone class for a status. Same mapping as <c>statusPill</c> in
    /// Content/js/jira-common.js: done = success, doing = accent, todo = neutral.
    /// </summary>
    public static string PillClass(string? status)
    {
        return Tone(status) switch
        {
            Done => "is-success",
            Doing => "is-accent",
            _ => "is-neutral"
        };
    }
}
