namespace ToolboxWeb.Web.ViewModels;

/// <summary>A library the user can tick in "Libraries": shipped with the app, so it works offline.</summary>
/// <param name="Files">Versioned app paths, CSS before scripts.</param>
public sealed record PlaygroundLibrary(string Id, string Name, IReadOnlyList<string> Files);

/// <summary>
/// The code playground (/code-playground.html). The user's HTML, CSS and JS never reach the
/// server: Content/js/code-playground.js runs them in a sandboxed frame and keeps the work in
/// this browser, so the page only carries limits, the storage key and library addresses.
/// </summary>
public sealed class CodePlaygroundPageViewModel
{
    /// <summary>How many named pieces of work the browser keeps.</summary>
    public const int MaxSaved = 20;

    /// <summary>A share link longer than this is refused (chat apps cut long links); Download works instead.</summary>
    public const int MaxShareLength = 30_000;

    /// <summary>Console lines kept; older ones drop off.</summary>
    public const int MaxConsoleLines = 500;

    /// <summary>localStorage key of the draft and the saved work, one per account on the same browser.</summary>
    public required string StorageKey { get; init; }

    /// <summary>Versioned address of the formatter (js-beautify), loaded when Format is first used.</summary>
    public required string FormatterUrl { get; init; }

    /// <summary>Address of the page that shows the result in its own tab.</summary>
    public required string RunUrl { get; init; }

    public required IReadOnlyList<PlaygroundLibrary> Libraries { get; init; }
}
