namespace ToolboxWeb.Web.ViewModels;

/// <summary>
/// The text compare page (/text-compare.html). The comparison runs in the browser
/// (Content/js/text-compare.js), so the page only carries its limits and the key under which
/// this browser keeps the user's last comparisons.
/// </summary>
public sealed class TextComparePageViewModel
{
    /// <summary>How many comparisons the browser remembers.</summary>
    public const int MaxRecent = 10;

    /// <summary>A side longer than this is compared but not remembered, to stay inside the browser's storage.</summary>
    public const int MaxSavedChars = 300_000;

    /// <summary>Largest file the "Open file" button and drag and drop read.</summary>
    public const int MaxFileBytes = 5 * 1024 * 1024;

    /// <summary>More lines than this on one side is refused: the page would stop responding.</summary>
    public const int MaxLines = 50_000;

    /// <summary>Above this many lines on a side the code colours are skipped so the result stays quick.</summary>
    public const int HighlightMaxLines = 5_000;

    /// <summary>
    /// The highlight.js languages offered (all in the bundled build, plus PowerShell), as
    /// (id, name shown). The names are product names and are not translated.
    /// </summary>
    public static readonly IReadOnlyList<(string Id, string Name)> Languages =
    [
        ("sql", "SQL"),
        ("csharp", "C#"),
        ("javascript", "JavaScript"),
        ("typescript", "TypeScript"),
        ("json", "JSON"),
        ("xml", "XML / HTML"),
        ("css", "CSS"),
        ("yaml", "YAML"),
        ("powershell", "PowerShell"),
        ("bash", "Bash"),
        ("python", "Python"),
        ("java", "Java"),
        ("markdown", "Markdown"),
        ("ini", "INI / config")
    ];

    /// <summary>localStorage key of the last comparisons, one list per account on the same browser.</summary>
    public required string RecentStorageKey { get; init; }
}
