using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ToolboxWeb.Web.Infrastructure.DacFx;

/// <summary>
/// Rewrites the DacFx deployment script into a plain T-SQL sync script.
/// <para>
/// DacFx emits an SSDT-style script that only runs with SQLCMD mode switched on: it uses
/// <c>:setvar</c>, <c>:on error exit</c>, and <c>$(DatabaseName)</c>. Pasted into an ordinary
/// SSMS query window that fails immediately with "Incorrect syntax near ':'". Resolving the
/// variables and dropping the directives makes it run anywhere, and the resulting shape —
/// a PRINT then the statement then GO — is what SQL Delta produces anyway.
/// </para>
/// The data-loss guards DacFx generates are deliberately kept.
/// </summary>
public static partial class DacFxScriptPostProcessor
{
    private const string Nl = "\r\n";

    public sealed record Result(
        string Script,
        IReadOnlyList<string> Warnings,
        int SuppressedMappedModuleCount = 0,
        int SuppressedSelectionBatchCount = 0);

    public static Result Process(
        string rawScript,
        string targetDatabase,
        string sourceLabel,
        string targetLabel,
        IReadOnlySet<string>? mappedSameObjectNames = null,
        IReadOnlySet<string>? excludedObjectNames = null)
    {
        var variables = CollectVariables(rawScript);
        var body = RemoveDirectives(rawScript);
        body = SqlCmdGuardPattern().Replace(body, string.Empty);
        body = ResolveVariables(body, variables, targetDatabase);
        var suppression = RemoveMappedModuleBatches(body, mappedSameObjectNames);
        body = suppression.Script;
        var selectionSuppression = RemoveExcludedObjectBatches(body, excludedObjectNames);
        body = selectionSuppression.Script;
        body = CollapseBlankRuns(body);

        var warnings = CollectWarnings(body);
        var script = BuildHeader(
            targetDatabase,
            sourceLabel,
            targetLabel,
            warnings,
            selectionSuppression.BatchCount) + body.TrimStart('\r', '\n');

        return new Result(
            script,
            warnings,
            suppression.ModuleCount,
            selectionSuppression.BatchCount);
    }

    /// <summary>
    /// Removes module batches that catalog reconciliation proved already equal after database-
    /// name mapping. This is intentionally a single pass over the generated script: excluding
    /// the same objects through DacFx one at a time repeatedly rebuilds its dependency graph.
    /// </summary>
    private static (string Script, int ModuleCount) RemoveMappedModuleBatches(
        string script,
        IReadOnlySet<string>? mappedSameObjectNames)
    {
        if (mappedSameObjectNames is null || mappedSameObjectNames.Count == 0)
        {
            return (script, 0);
        }

        var output = new StringBuilder(script.Length);
        var removedModules = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var previousEnd = 0;

        foreach (Match match in GoTerminatedBatchPattern().Matches(script))
        {
            if (match.Index > previousEnd)
            {
                output.Append(script, previousEnd, match.Index - previousEnd);
            }

            var body = match.Groups["body"].Value;
            var key = GetBatchObjectKey(body, out var isModuleDefinition);
            if (key is null || !mappedSameObjectNames.Contains(key))
            {
                output.Append(match.Value);
            }
            else if (isModuleDefinition)
            {
                removedModules.Add(key);
            }

            previousEnd = match.Index + match.Length;
        }

        if (previousEnd < script.Length)
        {
            var tail = script[previousEnd..];
            var key = GetBatchObjectKey(tail, out var isModuleDefinition);
            if (key is null || !mappedSameObjectNames.Contains(key))
            {
                output.Append(tail);
            }
            else if (isModuleDefinition)
            {
                removedModules.Add(key);
            }
        }

        return (output.ToString(), removedModules.Count);
    }

    /// <summary>
    /// Applies the grid selection in one linear pass. DacFx Include/Exclude is deliberately
    /// avoided because every call may rebuild the complete dependency graph. Non-module
    /// batches mentioning an unchecked object are removed; module definitions are matched only
    /// by the object they define so a procedure is not removed merely for referencing a table.
    /// A table rebuild spans many GO batches, so the complete transaction is suppressed.
    /// </summary>
    private static (string Script, int BatchCount) RemoveExcludedObjectBatches(
        string script,
        IReadOnlySet<string>? excludedObjectNames)
    {
        if (excludedObjectNames is null || excludedObjectNames.Count == 0)
        {
            return (script, 0);
        }

        var excluded = excludedObjectNames
            .Select(NormalizeObjectName)
            .Where(x => x.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (excluded.Count == 0)
        {
            return (script, 0);
        }

        var output = new StringBuilder(script.Length);
        var previousEnd = 0;
        var removed = 0;
        var suppressingRebuild = false;

        foreach (Match match in GoTerminatedBatchPattern().Matches(script))
        {
            if (match.Index > previousEnd)
            {
                output.Append(script, previousEnd, match.Index - previousEnd);
            }

            var body = match.Groups["body"].Value;
            if (ShouldSuppressBatch(body, excluded, ref suppressingRebuild))
            {
                removed++;
            }
            else
            {
                output.Append(match.Value);
            }

            previousEnd = match.Index + match.Length;
        }

        if (previousEnd < script.Length)
        {
            var tail = script[previousEnd..];
            if (ShouldSuppressBatch(tail, excluded, ref suppressingRebuild))
            {
                removed++;
            }
            else
            {
                output.Append(tail);
            }
        }

        return (output.ToString(), removed);
    }

    private static bool ShouldSuppressBatch(
        string batch,
        IReadOnlySet<string> excluded,
        ref bool suppressingRebuild)
    {
        var ownKey = GetBatchObjectKey(batch, out var isModuleDefinition);
        if (isModuleDefinition)
        {
            return ownKey is not null && excluded.Contains(ownKey);
        }

        if (suppressingRebuild)
        {
            if (CommitTransactionPattern().IsMatch(batch))
            {
                suppressingRebuild = false;
            }
            return true;
        }

        var rebuild = RebuildPattern().Match(batch);
        if (rebuild.Success && excluded.Contains(NormalizeObjectName(rebuild.Groups["table"].Value)))
        {
            suppressingRebuild = !CommitTransactionPattern().IsMatch(batch);
            return true;
        }

        var searchable = NormalizeBatchForObjectSearch(batch);
        foreach (var key in excluded)
        {
            if (ContainsObjectKey(searchable, key))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeBatchForObjectSearch(string batch) =>
        DotWhitespacePattern().Replace(
            batch.Replace("[", string.Empty, StringComparison.Ordinal)
                .Replace("]", string.Empty, StringComparison.Ordinal)
                .ToLowerInvariant(),
            ".");

    private static bool ContainsObjectKey(string batch, string key)
    {
        var start = 0;
        while (start < batch.Length)
        {
            var index = batch.IndexOf(key, start, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return false;
            }

            var beforeOk = index == 0 || !IsIdentifierCharacter(batch[index - 1]);
            var end = index + key.Length;
            var afterOk = end == batch.Length || !IsIdentifierCharacter(batch[end]);
            if (beforeOk && afterOk)
            {
                return true;
            }
            start = index + 1;
        }

        return false;
    }

    private static bool IsIdentifierCharacter(char value) =>
        char.IsLetterOrDigit(value) || value is '_' or '#' or '$';

    private static string? GetBatchObjectKey(string batch, out bool isModuleDefinition)
    {
        var match = ModuleDefinitionPattern().Match(batch);
        isModuleDefinition = match.Success;

        if (!match.Success)
        {
            match = RefreshModulePattern().Match(batch);
        }

        if (!match.Success)
        {
            match = ModulePrintPattern().Match(batch);
        }

        return match.Success ? NormalizeObjectName(match.Groups["name"].Value) : null;
    }

    private static string NormalizeObjectName(string name) =>
        name.Replace("[", string.Empty, StringComparison.Ordinal)
            .Replace("]", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("\t", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();

    private static Dictionary<string, string> CollectVariables(string script)
    {
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in SetVarPattern().Matches(script))
        {
            variables[match.Groups["name"].Value] = match.Groups["value"].Value;
        }

        return variables;
    }

    private static string RemoveDirectives(string script)
    {
        var output = new StringBuilder(script.Length);

        foreach (var line in script.Split('\n'))
        {
            var trimmed = line.TrimStart();
            var isDirective =
                trimmed.StartsWith(":setvar", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith(":on error", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith(":r ", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith(":connect", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith(":out ", StringComparison.OrdinalIgnoreCase);

            if (!isDirective)
            {
                output.Append(line).Append('\n');
            }
        }

        return output.ToString();
    }

    private static string ResolveVariables(
        string script,
        Dictionary<string, string> variables,
        string targetDatabase)
    {
        // DatabaseName is the one that matters; make sure it is right even if the header
        // did not declare it.
        variables["DatabaseName"] = targetDatabase;

        foreach (var (name, value) in variables)
        {
            script = script.Replace($"$({name})", value, StringComparison.OrdinalIgnoreCase);
        }

        return script;
    }

    private static string CollapseBlankRuns(string script)
    {
        var normalized = script.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        normalized = BlankRunPattern().Replace(normalized, "\n\n");
        return normalized.Replace("\n", Nl, StringComparison.Ordinal);
    }

    /// <summary>
    /// Pulls out the data-loss notices DacFx wrote into the body so they can be repeated at
    /// the very top, where a reviewer actually sees them.
    /// </summary>
    private static List<string> CollectWarnings(string script)
    {
        var warnings = new List<string>();

        foreach (Match match in GoTerminatedBatchPattern().Matches(script))
        {
            CollectBatchWarnings(match.Groups["body"].Value, warnings);
        }

        return warnings;
    }

    private static void CollectBatchWarnings(string batch, List<string> warnings)
    {
        // DROP TABLE inside a procedure body is application logic (usually a local #temp
        // table), not a schema deployment action. Counting it in REVIEW FIRST made the header
        // report hundreds of false destructive items on large databases.
        if (ModuleDefinitionPattern().IsMatch(batch))
        {
            return;
        }

        foreach (Match match in DataLossGuardPattern().Matches(batch))
        {
            var table = match.Groups["table"].Value;
            var text = $"Data-loss guard on {table}: the script stops if that table has rows.";
            if (!warnings.Contains(text, StringComparer.Ordinal))
            {
                warnings.Add(text);
            }
        }

        foreach (Match match in RebuildPattern().Matches(batch))
        {
            var text = $"Table rebuild via temp table: {match.Groups["table"].Value}";
            if (!warnings.Contains(text, StringComparer.Ordinal))
            {
                warnings.Add(text);
            }
        }

        foreach (Match match in DropPattern().Matches(batch))
        {
            var text = $"DROP {match.Groups["kind"].Value.ToUpperInvariant()} {match.Groups["name"].Value}";
            if (!warnings.Contains(text, StringComparer.Ordinal))
            {
                warnings.Add(text);
            }
        }

    }

    private static string BuildHeader(
        string targetDatabase,
        string sourceLabel,
        string targetLabel,
        IReadOnlyList<string> warnings,
        int suppressedSelectionBatchCount)
    {
        var header = new StringBuilder();
        header.Append("-- Synchronization script for ").Append(targetLabel).Append(Nl);
        header.Append("-- Source: ").Append(sourceLabel).Append(Nl);
        header.Append(CultureInfo.InvariantCulture,
            $"-- Generated by ToolboxWeb on {DateTime.Now:yyyy-MM-dd HH:mm:ss}").Append(Nl);
        header.Append("-- Please backup ").Append(targetDatabase).Append(" before executing this script").Append(Nl);
        if (suppressedSelectionBatchCount > 0)
        {
            header.Append(CultureInfo.InvariantCulture,
                $"-- {suppressedSelectionBatchCount} deployment batch(es) for unchecked objects were omitted.").Append(Nl);
        }
        header.Append("--").Append(Nl);

        if (warnings.Count > 0)
        {
            header.Append("-- ============================================================").Append(Nl);
            header.Append(CultureInfo.InvariantCulture,
                $"-- REVIEW FIRST: {warnings.Count} item(s) below remove or rebuild data").Append(Nl);

            foreach (var warning in warnings.Take(60))
            {
                header.Append("--   ").Append(warning).Append(Nl);
            }

            if (warnings.Count > 60)
            {
                header.Append(CultureInfo.InvariantCulture, $"--   ... and {warnings.Count - 60} more").Append(Nl);
            }

            header.Append("-- ============================================================").Append(Nl);
        }
        else
        {
            header.Append("-- No object is dropped and no table is rebuilt by this script.").Append(Nl);
        }

        header.Append(Nl);
        header.Append("USE [").Append(targetDatabase).Append(']').Append(Nl);
        header.Append("GO").Append(Nl).Append(Nl);
        return header.ToString();
    }

    [GeneratedRegex(@"(?im)^[ \t]*:setvar[ \t]+(?<name>\w+)[ \t]+""?(?<value>[^""\r\n]*?)""?[ \t]*\r?$")]
    private static partial Regex SetVarPattern();

    [GeneratedRegex(@"(?is)IF\s+N'\$\(__IsSqlCmdEnabled\)'\s+NOT\s+LIKE\s+N'True'\s*BEGIN.*?END")]
    private static partial Regex SqlCmdGuardPattern();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex BlankRunPattern();

    [GeneratedRegex(@"(?ims)(?<body>.*?)(?:^[ \t]*GO[ \t]*;?[ \t]*(?:\r?\n|$))")]
    private static partial Regex GoTerminatedBatchPattern();

    [GeneratedRegex(@"(?im)^[ \t]*(?:CREATE|ALTER)[ \t]+(?:OR[ \t]+ALTER[ \t]+)?(?:PROC(?:EDURE)?|VIEW|FUNCTION|TRIGGER)[ \t]+(?<name>(?:\[[^\]]+\]|[\w#]+)(?:[ \t]*\.[ \t]*(?:\[[^\]]+\]|[\w#]+))?)")]
    private static partial Regex ModuleDefinitionPattern();

    [GeneratedRegex(@"(?im)\bsp_refreshsqlmodule[ \t]+N?'(?<name>(?:\[[^\]]+\]|[\w#]+)(?:[ \t]*\.[ \t]*(?:\[[^\]]+\]|[\w#]+))?)'")]
    private static partial Regex RefreshModulePattern();

    [GeneratedRegex(@"(?im)^[ \t]*PRINT[ \t]+N?'(?:Altering|Creating|Refreshing)[ \t]+(?:Procedure|View|Function|Trigger)[ \t]+(?<name>(?:\[[^\]]+\]|[\w#]+)(?:[ \t]*\.[ \t]*(?:\[[^\]]+\]|[\w#]+))?)")]
    private static partial Regex ModulePrintPattern();

    [GeneratedRegex(@"(?im)^\s*IF\s+EXISTS\s*\(\s*select\s+top\s+1\s+1\s+from\s+(?<table>\[[^\r\n]+?\])\s*\)")]
    private static partial Regex DataLossGuardPattern();

    [GeneratedRegex(@"(?im)^\s*PRINT\s+N'Starting rebuilding table (?<table>[^']+?)\.\.\.'")]
    private static partial Regex RebuildPattern();

    [GeneratedRegex(@"(?im)^\s*COMMIT\s+TRANSACTION\s*;?")]
    private static partial Regex CommitTransactionPattern();

    [GeneratedRegex(@"\s*\.\s*")]
    private static partial Regex DotWhitespacePattern();

    [GeneratedRegex(@"(?im)^\s*DROP\s+(?<kind>TABLE|VIEW|PROCEDURE|FUNCTION|TRIGGER|INDEX)\s+(?<name>\[[^\r\n;]+)")]
    private static partial Regex DropPattern();
}
