using System.Globalization;
using System.Text;
using ToolboxWeb.Web.ViewModels.SqlProfiler;

namespace ToolboxWeb.Web.Infrastructure.SqlProfiler;

/// <summary>
/// Splits an <c>exec dbo.Proc @a=1, @b=N'x'</c> statement into its parameters.
/// <para>
/// Extended Events reports the statement as one string and never reports parameter types,
/// so the type column is inferred from how each value is written. That is good enough for
/// reading, and the raw statement is always kept so nothing is lost.
/// </para>
/// </summary>
public static class ProfilerEventParser
{
    public static string ExtractObjectName(string statement, string fallback)
    {
        if (string.IsNullOrWhiteSpace(statement))
        {
            return fallback;
        }

        var trimmed = statement.TrimStart();
        foreach (var keyword in new[] { "execute ", "exec " })
        {
            if (!trimmed.StartsWith(keyword, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var rest = trimmed[keyword.Length..].TrimStart();
            var end = rest.IndexOfAny([' ', '\t', '\r', '\n', '@', ';']);
            var name = (end < 0 ? rest : rest[..end]).Trim();
            return name.Length == 0 ? fallback : name;
        }

        return fallback;
    }

    public static IReadOnlyList<ProfilerParameterViewModel> ExtractParameters(string statement)
    {
        if (string.IsNullOrWhiteSpace(statement))
        {
            return [];
        }

        var start = IndexOfFirstParameter(statement);
        if (start < 0)
        {
            return [];
        }

        var parameters = new List<ProfilerParameterViewModel>();

        foreach (var chunk in SplitTopLevel(statement[start..]))
        {
            var separator = chunk.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var name = chunk[..separator].Trim();
            var value = chunk[(separator + 1)..].Trim();

            if (!name.StartsWith('@'))
            {
                continue;
            }

            parameters.Add(new ProfilerParameterViewModel
            {
                Name = name,
                Value = Unquote(value),
                Kind = GuessKind(value)
            });
        }

        return parameters;
    }

    /// <summary>Rebuilds the call with values replaced, for the "hide parameter values" toggle.</summary>
    public static string MaskStatement(string statement, IReadOnlyList<ProfilerParameterViewModel> parameters)
    {
        if (parameters.Count == 0 || string.IsNullOrWhiteSpace(statement))
        {
            return statement;
        }

        var start = IndexOfFirstParameter(statement);
        if (start < 0)
        {
            return statement;
        }

        var builder = new StringBuilder(statement[..start]);
        builder.Append(string.Join(", ", parameters.Select(x => $"{x.Name}=<{x.Kind}>")));
        return builder.ToString();
    }

    private static int IndexOfFirstParameter(string statement)
    {
        var inString = false;

        for (var i = 0; i < statement.Length; i++)
        {
            var current = statement[i];

            if (current == '\'')
            {
                inString = !inString;
                continue;
            }

            if (!inString && current == '@')
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Splits on commas that are outside string literals and brackets.</summary>
    private static IEnumerable<string> SplitTopLevel(string text)
    {
        var builder = new StringBuilder();
        var inString = false;
        var depth = 0;

        for (var i = 0; i < text.Length; i++)
        {
            var current = text[i];

            if (current == '\'')
            {
                // A doubled quote is an escaped quote, not the end of the literal.
                if (inString && i + 1 < text.Length && text[i + 1] == '\'')
                {
                    builder.Append("''");
                    i++;
                    continue;
                }

                inString = !inString;
                builder.Append(current);
                continue;
            }

            if (!inString)
            {
                if (current == '(')
                {
                    depth++;
                }
                else if (current == ')')
                {
                    depth--;
                }
                else if (current == ',' && depth == 0)
                {
                    yield return builder.ToString();
                    builder.Clear();
                    continue;
                }
            }

            builder.Append(current);
        }

        if (builder.Length > 0)
        {
            yield return builder.ToString();
        }
    }

    private static string Unquote(string value)
    {
        var trimmed = value.Trim();

        if (trimmed.StartsWith("N'", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[1..];
        }

        if (trimmed.Length >= 2 && trimmed[0] == '\'' && trimmed[^1] == '\'')
        {
            return trimmed[1..^1].Replace("''", "'", StringComparison.Ordinal);
        }

        return trimmed;
    }

    private static string GuessKind(string value)
    {
        var trimmed = value.Trim();

        if (trimmed.Length == 0)
        {
            return "unknown";
        }

        if (string.Equals(trimmed, "NULL", StringComparison.OrdinalIgnoreCase))
        {
            return "null";
        }

        if (trimmed.StartsWith("N'", StringComparison.OrdinalIgnoreCase))
        {
            return "nvarchar";
        }

        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return "binary";
        }

        if (trimmed[0] == '\'')
        {
            var inner = Unquote(trimmed);
            return DateTime.TryParse(inner, CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
                ? "datetime"
                : "varchar";
        }

        if (long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
        {
            // SQL Server renders bit parameters as 0 or 1; they are indistinguishable from int here.
            return trimmed is "0" or "1" ? "int/bit" : "int";
        }

        return decimal.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out _)
            ? "decimal"
            : "unknown";
    }
}
