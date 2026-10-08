using System.Globalization;
using System.Text;

namespace ToolboxWeb.Web.Services;

/// <summary>
/// Accent- and case-insensitive matching for note search.
/// <para>
/// EF Core on SQLite turns <c>string.Contains</c> into <c>instr()</c>, which is case-sensitive
/// and treats "chu" and "chú" as different text, so typing "ghi chu" never found "Ghi chú".
/// Notes are personal and few, so the service filters them in memory with this instead.
/// </para>
/// </summary>
public static class NoteSearch
{
    /// <summary>Splits a query into normalised terms; every term must match (AND).</summary>
    public static IReadOnlyList<string> Terms(string? query)
    {
        return Normalize(query)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    public static bool Matches(IReadOnlyList<string> terms, params string?[] fields)
    {
        if (terms.Count == 0)
        {
            return true;
        }

        var haystack = Normalize(string.Join('\n', fields.Where(x => !string.IsNullOrEmpty(x))));
        return terms.All(term => haystack.Contains(term, StringComparison.Ordinal));
    }

    /// <summary>Lower-cases and strips diacritics, including the Vietnamese đ, which is a letter and not a combining mark.</summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(ch switch
            {
                'đ' => 'd',
                'Đ' => 'D',
                _ => ch
            });
        }

        return builder.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }
}
