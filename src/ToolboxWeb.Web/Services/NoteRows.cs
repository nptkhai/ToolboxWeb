using System.Text.RegularExpressions;
using ToolboxWeb.Web.ViewModels;

namespace ToolboxWeb.Web.Services;

/// <summary>
/// Link and checklist notes keep their rows in the single Content column, one row per line, in a
/// shape that still reads as plain text if the note is switched to another format:
/// <code>
/// Jira PMT-12 | https://jira.example/browse/PMT-12   (the name before "|" is optional)
/// [doing] Gửi báo cáo tháng 9
/// </code>
/// A line that fits neither shape is kept as it is, so nothing typed by hand is lost.
/// </summary>
public static class NoteRows
{
    public const string LinkSeparator = " | ";
    private const int MaxRows = 200;

    private static readonly Regex TaskLine = new(@"^\[([a-zA-Z]{2,10})\]\s?(.*)$", RegexOptions.Compiled);

    public static IReadOnlyList<NoteLinkRow> ParseLinks(string? content) =>
        Lines(content).Select(ParseLink).ToArray();

    public static IReadOnlyList<NoteTaskRow> ParseTasks(string? content) =>
        Lines(content).Select(ParseTask).ToArray();

    /// <summary>Builds the stored text from the rows a form posted. Rows with no address are dropped.</summary>
    public static string FromLinks(IList<string>? names, IList<string>? urls)
    {
        var lines = new List<string>();
        for (var i = 0; i < (urls?.Count ?? 0) && lines.Count < MaxRows; i++)
        {
            var url = urls![i]?.Trim();
            if (string.IsNullOrEmpty(url))
            {
                continue;
            }

            var name = i < (names?.Count ?? 0) ? names![i]?.Trim() : null;
            lines.Add(string.IsNullOrEmpty(name) ? url : name + LinkSeparator + url);
        }

        return string.Join("\n", lines);
    }

    /// <summary>Builds the stored text from checklist rows. Rows with no text are dropped.</summary>
    public static string FromTasks(IList<string>? texts, IList<string>? statuses)
    {
        var lines = new List<string>();
        for (var i = 0; i < (texts?.Count ?? 0) && lines.Count < MaxRows; i++)
        {
            var text = texts![i]?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            var status = NoteTaskStatusCatalog.Get(i < (statuses?.Count ?? 0) ? statuses![i] : null);
            lines.Add($"[{status.Slug}] {text}");
        }

        return string.Join("\n", lines);
    }

    /// <summary>True when a posted row list holds at least one filled-in value.</summary>
    public static bool HasAny(IList<string>? values) =>
        values is not null && values.Any(value => !string.IsNullOrWhiteSpace(value));

    public static int DoneCount(IReadOnlyList<NoteTaskRow> rows) =>
        rows.Count(row => row.Status.Slug == NoteTaskStatusCatalog.DoneSlug);

    private static IEnumerable<string> Lines(string? content) =>
        (content ?? string.Empty)
        .Split('\n')
        .Select(line => line.Trim())
        .Where(line => line.Length > 0)
        .Take(MaxRows);

    private static NoteLinkRow ParseLink(string line)
    {
        var separator = line.IndexOf(LinkSeparator, StringComparison.Ordinal);
        return separator > 0
            ? new NoteLinkRow(line[..separator].Trim(), line[(separator + LinkSeparator.Length)..].Trim())
            : new NoteLinkRow(string.Empty, line);
    }

    private static NoteTaskRow ParseTask(string line)
    {
        var match = TaskLine.Match(line);
        if (!match.Success)
        {
            return new NoteTaskRow(line, NoteTaskStatusCatalog.Default);
        }

        var slug = match.Groups[1].Value;
        var known = NoteTaskStatusCatalog.All.FirstOrDefault(
            status => string.Equals(status.Slug, slug, StringComparison.OrdinalIgnoreCase));

        // "[note] something" that is not one of our states stays whole, prefix and all.
        return known is null
            ? new NoteTaskRow(line, NoteTaskStatusCatalog.Default)
            : new NoteTaskRow(match.Groups[2].Value.Trim(), known);
    }
}
