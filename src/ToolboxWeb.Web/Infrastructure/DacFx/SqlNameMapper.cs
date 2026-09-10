using System.Text.RegularExpressions;
using ToolboxWeb.Web.ViewModels.SchemaCompare;

namespace ToolboxWeb.Web.Infrastructure.DacFx;

/// <summary>
/// Rewrites database names inside T-SQL so definitions copied from an origin database match
/// and deploy against a school-coded target: <c>EDU_ORG_DATA</c> becomes
/// <c>EDU_FBU_DATA</c>, <c>WEB_ORG_EOFFICE</c> becomes <c>WEB_FBU_EOFFICE</c>, and so on.
/// <para>
/// Two mechanisms, both deliberately conservative:
/// </para>
/// <list type="bullet">
/// <item><b>Explicit pairs</b> rewrite one exact database name wherever it appears as an
/// identifier. Boundaries are enforced, so <c>EDU_ORG</c> never matches inside
/// <c>EDU_ORG_DATA</c>, a column named <c>Organization</c>, or the word ORG in a comment.</item>
/// <item><b>Code substitution</b> (ORG → FBU) applies only inside a <em>database-name
/// position</em>: the first part of a qualified name such as <c>db.schema.object</c>, or the
/// target of <c>USE</c>. That covers every database at once without having to list them, and
/// cannot touch ordinary code because nothing else occupies that position.</item>
/// </list>
/// <para>
/// One origin database can map to several targets (<c>WEB_ORG_EOFFICE</c> serves both
/// <c>WEB_FBU_EOFFICE</c> and <c>WEB_FBU_SINHVIEN</c>). Equivalence accepts any alternative;
/// rewriting prefers the one matching the database actually being compared against.
/// </para>
/// </summary>
public sealed partial class SqlNameMapper
{
    private sealed record Rule(string From, IReadOnlyList<string> To, Regex Pattern);

    private readonly List<Rule> _rules;
    private readonly string? _codeFrom;
    private readonly string? _codeTo;
    private readonly string _preferredTarget;

    private SqlNameMapper(List<Rule> rules, string? codeFrom, string? codeTo, string preferredTarget)
    {
        _rules = rules;
        _codeFrom = codeFrom;
        _codeTo = codeTo;
        _preferredTarget = preferredTarget;
    }

    public bool HasMappings => _rules.Count > 0 || _codeFrom is not null;

    /// <summary>
    /// Fast pre-filter used before asking DacFx to script the target object. If the source
    /// definition contains none of the configured source names, mapping cannot make that
    /// difference disappear.
    /// </summary>
    public bool CouldAffectSource(string? sql)
    {
        if (string.IsNullOrWhiteSpace(sql) || !HasMappings)
        {
            return false;
        }

        foreach (var rule in _rules)
        {
            if (sql.Contains(rule.From, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return _codeFrom is not null
            && sql.Contains(_codeFrom, StringComparison.OrdinalIgnoreCase);
    }

    public static SqlNameMapper From(
        IEnumerable<NameMappingPair>? pairs,
        string? sourceCode = null,
        string? targetCode = null,
        string? preferredTarget = null)
    {
        var rules = new List<Rule>();

        foreach (var pair in pairs ?? [])
        {
            var from = Clean(pair.From);

            // "A|B" lets one origin database stand in for several targets.
            var targets = (pair.To ?? string.Empty)
                .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(Clean)
                .Where(x => x.Length > 0)
                .ToList();

            if (from.Length == 0 || targets.Count == 0)
            {
                continue;
            }

            rules.Add(new Rule(from, targets, IdentifierPattern(from)));
        }

        // Longest source first, so an overlapping shorter name cannot claim part of it.
        rules.Sort((a, b) => b.From.Length.CompareTo(a.From.Length));

        var code = Clean(sourceCode);
        var toCode = Clean(targetCode);
        var codeUsable = code.Length > 0 && toCode.Length > 0
            && !string.Equals(code, toCode, StringComparison.OrdinalIgnoreCase);

        return new SqlNameMapper(
            rules,
            codeUsable ? code : null,
            codeUsable ? toCode : null,
            Clean(preferredTarget));
    }

    /// <summary>Rewrites source SQL so it refers to the target's databases.</summary>
    public string Apply(string? sql)
    {
        if (string.IsNullOrEmpty(sql) || !HasMappings)
        {
            return sql ?? string.Empty;
        }

        var result = sql;

        foreach (var rule in _rules)
        {
            var target = ChooseTarget(rule);
            result = rule.Pattern.Replace(result, match => match.Groups[1].Value + target + match.Groups[2].Value);
        }

        if (_codeFrom is not null && _codeTo is not null)
        {
            result = RewriteDatabasePositions(result, db => SwapCode(db, _codeFrom, _codeTo));
        }

        return result;
    }

    /// <summary>
    /// True when the two definitions are the same code once database names and cosmetic
    /// formatting are set aside.
    /// <para>
    /// Both sides are reduced to a shared canonical form rather than mapping one onto the
    /// other, so every alternative target is accepted without trying combinations.
    /// </para>
    /// </summary>
    public bool AreEquivalent(string? sourceScript, string? targetScript)
    {
        if (string.IsNullOrWhiteSpace(sourceScript) || string.IsNullOrWhiteSpace(targetScript))
        {
            return false;
        }

        return string.Equals(
            Canonicalize(NeutralizeNames(sourceScript, isSource: true)),
            Canonicalize(NeutralizeNames(targetScript, isSource: false)),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Replaces every mapped database name — the origin name on one side, any of its target
    /// alternatives on the other — with the same placeholder, so the two sides can be
    /// compared directly.
    /// </summary>
    private string NeutralizeNames(string sql, bool isSource)
    {
        var result = sql;

        for (var i = 0; i < _rules.Count; i++)
        {
            var placeholder = $"«db{i}»";
            var rule = _rules[i];

            if (isSource)
            {
                result = rule.Pattern.Replace(result, placeholder);
                continue;
            }

            foreach (var target in rule.To)
            {
                result = IdentifierPattern(target).Replace(result, placeholder);
            }
        }

        if (_codeFrom is not null && _codeTo is not null)
        {
            var code = isSource ? _codeFrom : _codeTo;
            result = RewriteDatabasePositions(result, db => SwapCode(db, code, "«code»"));
        }

        return result;
    }

    /// <summary>
    /// Applies <paramref name="rewrite"/> to database names only: the first part of a
    /// qualified name, or the operand of <c>USE</c>. Nothing else in T-SQL sits in that
    /// position, which is what makes a bare token substitution safe here.
    /// </summary>
    private static string RewriteDatabasePositions(string sql, Func<string, string> rewrite)
    {
        var result = QualifiedNamePattern().Replace(sql, match => Rebuild(match, rewrite));
        return UseStatementPattern().Replace(result, match => Rebuild(match, rewrite));
    }

    private static string Rebuild(Match match, Func<string, string> rewrite)
    {
        var bracketed = match.Groups["db"];
        var bare = match.Groups["db2"];
        var original = bracketed.Success ? bracketed.Value : bare.Value;
        var replacement = rewrite(original);

        if (string.Equals(original, replacement, StringComparison.Ordinal))
        {
            return match.Value;
        }

        var group = bracketed.Success ? bracketed : bare;
        var start = group.Index - match.Index;
        return match.Value[..start] + replacement + match.Value[(start + group.Length)..];
    }

    /// <summary>
    /// Swaps a whole underscore-delimited segment, so ORG → FBU turns EDU_ORG_DATA into
    /// EDU_FBU_DATA but leaves ORGANIZATION and REORG alone.
    /// </summary>
    private static string SwapCode(string databaseName, string from, string to)
    {
        var segments = databaseName.Split('_');
        var changed = false;

        for (var i = 0; i < segments.Length; i++)
        {
            if (string.Equals(segments[i], from, StringComparison.OrdinalIgnoreCase))
            {
                segments[i] = to;
                changed = true;
            }
        }

        return changed ? string.Join('_', segments) : databaseName;
    }

    private string ChooseTarget(Rule rule)
    {
        // With several alternatives, the database actually being compared against wins.
        var match = rule.To.FirstOrDefault(x => string.Equals(x, _preferredTarget, StringComparison.OrdinalIgnoreCase));
        return match ?? rule.To[0];
    }

    private static Regex IdentifierPattern(string name) => new(
        $@"(?<![\w@#$])(\[?){Regex.Escape(name)}(\]?)(?![\w@#$])",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static string Clean(string? value) => value?.Trim().Trim('[', ']') ?? string.Empty;

    /// <summary>
    /// Collapses what carries no meaning: letter case, brackets, an explicit <c>dbo.</c>
    /// qualifier, and runs of whitespace. Applied identically to both sides.
    /// </summary>
    private static string Canonicalize(string sql)
    {
        var value = sql.ToLowerInvariant();
        value = value.Replace("[", string.Empty, StringComparison.Ordinal)
            .Replace("]", string.Empty, StringComparison.Ordinal);
        value = DboQualifier().Replace(value, string.Empty);
        value = Whitespace().Replace(value, " ").Trim();
        return value;
    }

    // The database part of "db.schema.object" / "db..object", bracketed or not. The
    // lookbehind rejects a part that is itself preceded by a dot, so only the first part of
    // a qualified name can match.
    [GeneratedRegex(
        @"(?<![\w@#$.])(?:\[(?<db>[^\]\r\n]+)\]|(?<db2>[\w@#$]+))\s*\.\s*(?:\[[^\]\r\n]*\]|[\w@#$]*)\s*\.",
        RegexOptions.IgnoreCase)]
    private static partial Regex QualifiedNamePattern();

    [GeneratedRegex(
        @"(?<![\w@#$.])USE\s+(?:\[(?<db>[^\]\r\n]+)\]|(?<db2>[\w@#$]+))",
        RegexOptions.IgnoreCase)]
    private static partial Regex UseStatementPattern();

    [GeneratedRegex(@"\bdbo\s*\.\s*")]
    private static partial Regex DboQualifier();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
