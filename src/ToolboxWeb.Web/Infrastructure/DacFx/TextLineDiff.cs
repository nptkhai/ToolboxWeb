using ToolboxWeb.Web.ViewModels.SchemaCompare;

namespace ToolboxWeb.Web.Infrastructure.DacFx;

/// <summary>
/// Line-by-line diff for the side-by-side detail panel. Classic LCS: good enough for object
/// definitions, which are at most a few hundred lines.
/// </summary>
public static class TextLineDiff
{
    private const int MaxLines = 4000;

    public static IReadOnlyList<DiffLineViewModel> Build(string? sourceText, string? targetText)
    {
        var source = SplitLines(sourceText);
        var target = SplitLines(targetText);

        // One side missing means the whole object is added or removed.
        if (source.Length == 0 && target.Length == 0)
        {
            return [];
        }

        if (source.Length == 0)
        {
            return target
                .Select((line, i) => new DiffLineViewModel
                {
                    TargetLine = i + 1,
                    TargetText = line,
                    State = nameof(DiffLineState.OnlyInTarget)
                })
                .ToArray();
        }

        if (target.Length == 0)
        {
            return source
                .Select((line, i) => new DiffLineViewModel
                {
                    SourceLine = i + 1,
                    SourceText = line,
                    State = nameof(DiffLineState.OnlyInSource)
                })
                .ToArray();
        }

        var lcs = BuildLcsTable(source, target);
        var rows = new List<DiffLineViewModel>();
        int si = 0, ti = 0;

        while (si < source.Length && ti < target.Length)
        {
            if (string.Equals(source[si], target[ti], StringComparison.Ordinal))
            {
                rows.Add(new DiffLineViewModel
                {
                    SourceLine = si + 1,
                    SourceText = source[si],
                    TargetLine = ti + 1,
                    TargetText = target[ti],
                    State = nameof(DiffLineState.Same)
                });
                si++;
                ti++;
            }
            else if (lcs[si + 1, ti] >= lcs[si, ti + 1])
            {
                rows.Add(new DiffLineViewModel
                {
                    SourceLine = si + 1,
                    SourceText = source[si],
                    State = nameof(DiffLineState.OnlyInSource)
                });
                si++;
            }
            else
            {
                rows.Add(new DiffLineViewModel
                {
                    TargetLine = ti + 1,
                    TargetText = target[ti],
                    State = nameof(DiffLineState.OnlyInTarget)
                });
                ti++;
            }
        }

        while (si < source.Length)
        {
            rows.Add(new DiffLineViewModel
            {
                SourceLine = si + 1,
                SourceText = source[si],
                State = nameof(DiffLineState.OnlyInSource)
            });
            si++;
        }

        while (ti < target.Length)
        {
            rows.Add(new DiffLineViewModel
            {
                TargetLine = ti + 1,
                TargetText = target[ti],
                State = nameof(DiffLineState.OnlyInTarget)
            });
            ti++;
        }

        return PairAdjacent(rows);
    }

    /// <summary>
    /// Collapses a removed line immediately followed by an added line into one "changed" row,
    /// so the two panels stay lined up instead of drifting apart.
    /// </summary>
    private static List<DiffLineViewModel> PairAdjacent(List<DiffLineViewModel> rows)
    {
        var merged = new List<DiffLineViewModel>(rows.Count);

        for (var i = 0; i < rows.Count; i++)
        {
            var current = rows[i];

            if (current.State == nameof(DiffLineState.OnlyInSource)
                && i + 1 < rows.Count
                && rows[i + 1].State == nameof(DiffLineState.OnlyInTarget))
            {
                var next = rows[i + 1];
                merged.Add(new DiffLineViewModel
                {
                    SourceLine = current.SourceLine,
                    SourceText = current.SourceText,
                    TargetLine = next.TargetLine,
                    TargetText = next.TargetText,
                    State = nameof(DiffLineState.Changed)
                });
                i++;
                continue;
            }

            merged.Add(current);
        }

        return merged;
    }

    private static int[,] BuildLcsTable(string[] source, string[] target)
    {
        var table = new int[source.Length + 1, target.Length + 1];

        for (var i = source.Length - 1; i >= 0; i--)
        {
            for (var j = target.Length - 1; j >= 0; j--)
            {
                table[i, j] = string.Equals(source[i], target[j], StringComparison.Ordinal)
                    ? table[i + 1, j + 1] + 1
                    : Math.Max(table[i + 1, j], table[i, j + 1]);
            }
        }

        return table;
    }

    private static string[] SplitLines(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .Select(x => x.TrimEnd())
            .ToArray();

        // Blank lines at either end add noise without adding meaning. DacFx in particular
        // starts every object script with one.
        var start = 0;
        while (start < lines.Length && lines[start].Length == 0)
        {
            start++;
        }

        var end = lines.Length;
        while (end > start && lines[end - 1].Length == 0)
        {
            end--;
        }

        lines = lines[start..end];

        // Guard against a pathological definition blowing up the O(n*m) table.
        return lines.Length > MaxLines ? lines[..MaxLines] : lines;
    }
}
