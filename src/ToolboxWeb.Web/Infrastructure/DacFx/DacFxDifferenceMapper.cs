using Microsoft.SqlServer.Dac.Compare;
using Microsoft.SqlServer.Dac.Model;
using ToolboxWeb.Web.ViewModels.SchemaCompare;

// The phase-1 engine has its own SchemaDifference; alias DacFx's to keep both usable.
using DacDifference = Microsoft.SqlServer.Dac.Compare.SchemaDifference;

namespace ToolboxWeb.Web.Infrastructure.DacFx;

/// <summary>
/// Turns the DacFx difference tree into what the grid needs.
/// <para>
/// Only top-level nodes become rows: those are real database objects. Child nodes are a
/// mixture of real members (a column, an index) and internal property flags such as
/// <c>LedgerType</c> or <c>IsVardecimalStorageFormatOn</c>, roughly thirty per table. The
/// property nodes are told apart cleanly by having neither a source nor a target object, so
/// no hand-maintained blocklist is needed.
/// </para>
/// </summary>
public static class DacFxDifferenceMapper
{
    public static DiffRowViewModel BuildRow(
        string id,
        DacDifference difference,
        bool mappedSame,
        bool included)
    {
        var identifier = difference.SourceObject?.Name ?? difference.TargetObject?.Name;
        var (schema, name) = SplitIdentifier(identifier);
        var display = string.IsNullOrEmpty(schema) ? name : $"{schema}.{name}";
        var action = difference.UpdateAction.ToString();
        var destructive = ContainsDelete(difference);

        return new DiffRowViewModel
        {
            Id = id,
            ObjectType = difference.Name ?? string.Empty,
            Schema = schema,
            Name = name,
            SourceName = difference.UpdateAction == SchemaUpdateAction.Delete ? string.Empty : display,
            TargetName = difference.UpdateAction == SchemaUpdateAction.Add ? string.Empty : display,
            Action = action,
            Included = !mappedSame && included,
            CanToggle = !mappedSame,
            ChildCount = CountMeaningfulChildren(difference),
            Destructive = destructive,
            MappedSame = mappedSame
        };
    }

    /// <summary>
    /// A changed table can contain deleted columns or constraints even though its top-level
    /// action is only <see cref="SchemaUpdateAction.Change"/>. Walk the actual DacFx tree so
    /// the UI does not mistake such a row for a non-destructive update.
    /// </summary>
    public static bool ContainsDelete(DacDifference difference)
    {
        if (difference.UpdateAction == SchemaUpdateAction.Delete)
        {
            return true;
        }

        foreach (var child in difference.Children)
        {
            // DacFx also marks dozens of scalar metadata properties as Delete when their
            // parent object is removed. For a changed object, only a deleted schema-model
            // object (column, constraint, index...) is a real deployment DROP.
            if ((child.UpdateAction == SchemaUpdateAction.Delete && child.TargetObject is not null)
                || ContainsDeletedObject(child))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsDeletedObject(DacDifference difference)
    {
        foreach (var child in difference.Children)
        {
            if ((child.UpdateAction == SchemaUpdateAction.Delete && child.TargetObject is not null)
                || ContainsDeletedObject(child))
            {
                return true;
            }
        }

        return false;
    }

    public static IReadOnlyList<DiffRowViewModel> BuildRows(DacFxComparison comparison)
    {
        var rows = new List<DiffRowViewModel>();
        var position = 0;

        foreach (var difference in comparison.Result.Differences)
        {
            var id = position.ToString();
            rows.Add(BuildRow(
                id,
                difference,
                comparison.MappedSameIds.Contains(id),
                comparison.IncludedDifferenceIds.Contains(id)));
            position++;
        }

        return rows
            .OrderBy(x => x.ObjectType, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Schema, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static DacFxCompareStatsViewModel BuildStats(IReadOnlyList<DiffRowViewModel> rows)
    {
        // Mapped-same rows are effectively identical, so they are left out of every count
        // except their own tally.
        var real = rows.Where(x => !x.MappedSame).ToList();

        var byType = real
            .GroupBy(x => x.ObjectType, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ObjectTypeCountViewModel
            {
                ObjectType = group.Key,
                Total = group.Count(),
                OnlyInSource = group.Count(x => x.Action == nameof(SchemaUpdateAction.Add)),
                Different = group.Count(x => x.Action == nameof(SchemaUpdateAction.Change)),
                OnlyInTarget = group.Count(x => x.Action == nameof(SchemaUpdateAction.Delete))
            })
            .OrderByDescending(x => x.Total)
            .ThenBy(x => x.ObjectType, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new DacFxCompareStatsViewModel
        {
            Total = real.Count,
            OnlyInSource = real.Count(x => x.Action == nameof(SchemaUpdateAction.Add)),
            Different = real.Count(x => x.Action == nameof(SchemaUpdateAction.Change)),
            OnlyInTarget = real.Count(x => x.Action == nameof(SchemaUpdateAction.Delete)),
            MappedSame = rows.Count(x => x.MappedSame),
            ByType = byType
        };
    }

    public static DiffDetailViewModel BuildDetail(
        string id,
        DacDifference difference,
        SchemaComparisonResult result,
        SqlNameMapper mapper)
    {
        // Map the source so the line diff highlights the genuine changes rather than the
        // EDU_ORG_DATA → EDU_FBU_DATA noise the user is not interested in.
        var sourceScript = mapper.Apply(SafeScript(() => result.GetDiffEntrySourceScript(difference)));
        var targetScript = SafeScript(() => result.GetDiffEntryTargetScript(difference));

        var lines = TextLineDiff.Build(sourceScript, targetScript);
        var identifier = difference.SourceObject?.Name ?? difference.TargetObject?.Name;
        var (schema, name) = SplitIdentifier(identifier);

        return new DiffDetailViewModel
        {
            Id = id,
            Title = string.IsNullOrEmpty(schema)
                ? $"{difference.Name} {name}"
                : $"{difference.Name} {schema}.{name}",
            Action = difference.UpdateAction.ToString(),
            ChangedLines = lines.Count(x => x.State != nameof(DiffLineState.Same)),
            Lines = lines
        };
    }

    private static int CountMeaningfulChildren(DacDifference difference)
    {
        return difference.Children.Count(x => x.SourceObject is not null || x.TargetObject is not null);
    }

    private static (string Schema, string Name) SplitIdentifier(ObjectIdentifier? identifier)
    {
        if (identifier is null || identifier.Parts.Count == 0)
        {
            return (string.Empty, string.Empty);
        }

        var parts = identifier.Parts;
        return parts.Count == 1
            ? (string.Empty, parts[0])
            : (parts[^2], parts[^1]);
    }

    /// <summary>
    /// DacFx throws for some node kinds instead of returning an empty script, so the detail
    /// panel degrades to showing one side rather than failing the request.
    /// </summary>
    private static string SafeScript(Func<string?> getter)
    {
        try
        {
            return getter() ?? string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }
}
