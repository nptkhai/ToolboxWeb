using System.Text.RegularExpressions;
using ToolboxWeb.Web.ViewModels.SchemaCompare;

namespace ToolboxWeb.Web.Infrastructure.SqlSchema;

public interface ISchemaComparer
{
    IReadOnlyList<SchemaDifference> Compare(
        DatabaseSchema source,
        DatabaseSchema target,
        SchemaCompareOptions options);
}

/// <summary>
/// Pure comparison over two schema snapshots. No I/O, so it is cheap to unit test.
/// </summary>
public sealed partial class SchemaComparer : ISchemaComparer
{
    private const string CategoryColumn = "Column";
    private const string CategoryPrimaryKey = "PrimaryKey";
    private const string CategoryUnique = "UniqueConstraint";
    private const string CategoryForeignKey = "ForeignKey";
    private const string CategoryCheck = "CheckConstraint";
    private const string CategoryIndex = "Index";
    private const string CategoryDefinition = "Definition";

    public IReadOnlyList<SchemaDifference> Compare(
        DatabaseSchema source,
        DatabaseSchema target,
        SchemaCompareOptions options)
    {
        var results = new List<SchemaDifference>();

        CompareSchemas(source, target, results);

        if (options.IncludeTables || options.IncludeKeysAndIndexes)
        {
            CompareTables(source, target, options, results);
        }

        if (options.IncludeProgrammability)
        {
            CompareModules(source, target, options, results);
        }

        return results
            .OrderBy(x => x.ObjectType)
            .ThenBy(x => x.Schema, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void CompareSchemas(DatabaseSchema source, DatabaseSchema target, List<SchemaDifference> results)
    {
        var targetSchemas = new HashSet<string>(target.Schemas, StringComparer.OrdinalIgnoreCase);
        var sourceSchemas = new HashSet<string>(source.Schemas, StringComparer.OrdinalIgnoreCase);

        foreach (var name in source.Schemas)
        {
            results.Add(new SchemaDifference
            {
                ObjectType = SchemaObjectType.Schema,
                Schema = name,
                Name = name,
                ChangeType = targetSchemas.Contains(name) ? SchemaChangeType.Same : SchemaChangeType.OnlyInSource
            });
        }

        foreach (var name in target.Schemas.Where(x => !sourceSchemas.Contains(x)))
        {
            results.Add(new SchemaDifference
            {
                ObjectType = SchemaObjectType.Schema,
                Schema = name,
                Name = name,
                ChangeType = SchemaChangeType.OnlyInTarget
            });
        }
    }

    private static void CompareTables(
        DatabaseSchema source,
        DatabaseSchema target,
        SchemaCompareOptions options,
        List<SchemaDifference> results)
    {
        var targetByKey = target.Tables.ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);
        var matchedTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var sourceTable in source.Tables)
        {
            if (!targetByKey.TryGetValue(sourceTable.Key, out var targetTable))
            {
                results.Add(NewDifference(SchemaObjectType.Table, sourceTable.Schema, sourceTable.Name, SchemaChangeType.OnlyInSource));
                continue;
            }

            matchedTargets.Add(sourceTable.Key);

            var difference = NewDifference(SchemaObjectType.Table, sourceTable.Schema, sourceTable.Name, SchemaChangeType.Same);
            CompareTableMembers(sourceTable, targetTable, options, difference);
            difference.ChangeType = difference.Details.Count > 0 ? SchemaChangeType.Different : SchemaChangeType.Same;
            results.Add(difference);
        }

        foreach (var targetTable in target.Tables.Where(x => !matchedTargets.Contains(x.Key)))
        {
            results.Add(NewDifference(SchemaObjectType.Table, targetTable.Schema, targetTable.Name, SchemaChangeType.OnlyInTarget));
        }
    }

    private static void CompareTableMembers(
        TableDefinition source,
        TableDefinition target,
        SchemaCompareOptions options,
        SchemaDifference difference)
    {
        if (options.IncludeTables)
        {
            CompareColumns(source, target, options, difference);
        }

        if (!options.IncludeKeysAndIndexes)
        {
            return;
        }

        ComparePrimaryKeys(source, target, options, difference);
        CompareUniqueConstraints(source, target, options, difference);
        CompareCheckConstraints(source, target, options, difference);
        CompareIndexes(source, target, options, difference);
        CompareForeignKeys(source, target, options, difference);
    }

    private static void CompareColumns(
        TableDefinition source,
        TableDefinition target,
        SchemaCompareOptions options,
        SchemaDifference difference)
    {
        MatchMembers(
            source.Columns,
            target.Columns,
            x => x.Name,
            _ => false,
            x => x.Name,
            ignoreNames: false,
            onPair: (sourceColumn, targetColumn) => CompareColumnPair(sourceColumn, targetColumn, options, difference),
            onSourceOnly: column => difference.Details.Add(Missing(CategoryColumn, column.Name, DescribeColumn(column), SchemaChangeType.OnlyInSource)),
            onTargetOnly: column => difference.Details.Add(Missing(CategoryColumn, column.Name, DescribeColumn(column), SchemaChangeType.OnlyInTarget)));
    }

    private static void CompareColumnPair(
        ColumnDefinition source,
        ColumnDefinition target,
        SchemaCompareOptions options,
        SchemaDifference difference)
    {
        AddIfDifferent(difference, CategoryColumn, source.Name, "DataType", source.TypeDisplay, target.TypeDisplay);

        AddIfDifferent(difference, CategoryColumn, source.Name, "Nullable",
            source.IsNullable ? "NULL" : "NOT NULL",
            target.IsNullable ? "NULL" : "NOT NULL");

        if (!options.IgnoreCollation)
        {
            AddIfDifferent(difference, CategoryColumn, source.Name, "Collation", source.Collation, target.Collation);
        }

        AddIfDifferent(difference, CategoryColumn, source.Name, "Identity",
            DescribeIdentity(source, options),
            DescribeIdentity(target, options));

        AddIfDifferent(difference, CategoryColumn, source.Name, "Computed",
            DescribeComputed(source, options),
            DescribeComputed(target, options));

        AddIfDifferent(difference, CategoryColumn, source.Name, "Default",
            NormalizeDefinition(source.DefaultDefinition, options),
            NormalizeDefinition(target.DefaultDefinition, options));

        var compareDefaultNames = !options.IgnoreConstraintNames
            && !source.DefaultIsSystemNamed
            && !target.DefaultIsSystemNamed
            && !string.IsNullOrEmpty(source.DefaultName)
            && !string.IsNullOrEmpty(target.DefaultName);

        if (compareDefaultNames)
        {
            AddIfDifferent(difference, CategoryColumn, source.Name, "DefaultName",
                source.DefaultName, target.DefaultName, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static void ComparePrimaryKeys(
        TableDefinition source,
        TableDefinition target,
        SchemaCompareOptions options,
        SchemaDifference difference)
    {
        var sourceKeys = source.PrimaryKey is null
            ? Array.Empty<KeyConstraintDefinition>()
            : new[] { source.PrimaryKey };
        var targetKeys = target.PrimaryKey is null
            ? Array.Empty<KeyConstraintDefinition>()
            : new[] { target.PrimaryKey };

        CompareKeyConstraints(sourceKeys, targetKeys, options, difference, CategoryPrimaryKey);
    }

    private static void CompareUniqueConstraints(
        TableDefinition source,
        TableDefinition target,
        SchemaCompareOptions options,
        SchemaDifference difference)
    {
        CompareKeyConstraints(source.UniqueConstraints, target.UniqueConstraints, options, difference, CategoryUnique);
    }

    private static void CompareKeyConstraints(
        IReadOnlyList<KeyConstraintDefinition> source,
        IReadOnlyList<KeyConstraintDefinition> target,
        SchemaCompareOptions options,
        SchemaDifference difference,
        string category)
    {
        MatchMembers(
            source,
            target,
            x => x.Name,
            x => x.IsSystemNamed,
            x => x.Signature,
            options.IgnoreConstraintNames,
            onPair: (sourceKey, targetKey) =>
            {
                AddIfDifferent(difference, category, sourceKey.Name, "Definition", sourceKey.Signature, targetKey.Signature);
                if (ShouldCompareNames(options, sourceKey.IsSystemNamed, targetKey.IsSystemNamed))
                {
                    AddIfDifferent(difference, category, sourceKey.Name, "Name",
                        sourceKey.Name, targetKey.Name, StringComparison.OrdinalIgnoreCase);
                }
            },
            onSourceOnly: key => difference.Details.Add(Missing(category, key.Name, key.Signature, SchemaChangeType.OnlyInSource)),
            onTargetOnly: key => difference.Details.Add(Missing(category, key.Name, key.Signature, SchemaChangeType.OnlyInTarget)));
    }

    private static void CompareCheckConstraints(
        TableDefinition source,
        TableDefinition target,
        SchemaCompareOptions options,
        SchemaDifference difference)
    {
        MatchMembers(
            source.CheckConstraints,
            target.CheckConstraints,
            x => x.Name,
            x => x.IsSystemNamed,
            x => NormalizeDefinition(x.Definition, options),
            options.IgnoreConstraintNames,
            onPair: (sourceCheck, targetCheck) =>
            {
                AddIfDifferent(difference, CategoryCheck, sourceCheck.Name, "Definition",
                    NormalizeDefinition(sourceCheck.Definition, options),
                    NormalizeDefinition(targetCheck.Definition, options));

                AddIfDifferent(difference, CategoryCheck, sourceCheck.Name, "Enabled",
                    sourceCheck.IsDisabled ? "DISABLED" : "ENABLED",
                    targetCheck.IsDisabled ? "DISABLED" : "ENABLED");

                if (ShouldCompareNames(options, sourceCheck.IsSystemNamed, targetCheck.IsSystemNamed))
                {
                    AddIfDifferent(difference, CategoryCheck, sourceCheck.Name, "Name",
                        sourceCheck.Name, targetCheck.Name, StringComparison.OrdinalIgnoreCase);
                }
            },
            onSourceOnly: check => difference.Details.Add(Missing(CategoryCheck, check.Name, check.Definition, SchemaChangeType.OnlyInSource)),
            onTargetOnly: check => difference.Details.Add(Missing(CategoryCheck, check.Name, check.Definition, SchemaChangeType.OnlyInTarget)));
    }

    private static void CompareIndexes(
        TableDefinition source,
        TableDefinition target,
        SchemaCompareOptions options,
        SchemaDifference difference)
    {
        MatchMembers(
            source.Indexes,
            target.Indexes,
            x => x.Name,
            _ => false,
            x => x.Signature,
            options.IgnoreConstraintNames,
            onPair: (sourceIndex, targetIndex) =>
                AddIfDifferent(difference, CategoryIndex, sourceIndex.Name, "Definition", sourceIndex.Signature, targetIndex.Signature),
            onSourceOnly: index => difference.Details.Add(Missing(CategoryIndex, index.Name, index.Signature, SchemaChangeType.OnlyInSource)),
            onTargetOnly: index => difference.Details.Add(Missing(CategoryIndex, index.Name, index.Signature, SchemaChangeType.OnlyInTarget)));
    }

    private static void CompareForeignKeys(
        TableDefinition source,
        TableDefinition target,
        SchemaCompareOptions options,
        SchemaDifference difference)
    {
        MatchMembers(
            source.ForeignKeys,
            target.ForeignKeys,
            x => x.Name,
            x => x.IsSystemNamed,
            x => x.Signature,
            options.IgnoreConstraintNames,
            onPair: (sourceKey, targetKey) =>
            {
                AddIfDifferent(difference, CategoryForeignKey, sourceKey.Name, "Definition", sourceKey.Signature, targetKey.Signature);
                if (ShouldCompareNames(options, sourceKey.IsSystemNamed, targetKey.IsSystemNamed))
                {
                    AddIfDifferent(difference, CategoryForeignKey, sourceKey.Name, "Name",
                        sourceKey.Name, targetKey.Name, StringComparison.OrdinalIgnoreCase);
                }
            },
            onSourceOnly: key => difference.Details.Add(Missing(CategoryForeignKey, key.Name, key.Signature, SchemaChangeType.OnlyInSource)),
            onTargetOnly: key => difference.Details.Add(Missing(CategoryForeignKey, key.Name, key.Signature, SchemaChangeType.OnlyInTarget)));
    }

    private static void CompareModules(
        DatabaseSchema source,
        DatabaseSchema target,
        SchemaCompareOptions options,
        List<SchemaDifference> results)
    {
        var targetByKey = target.Modules.ToDictionary(x => $"{x.ObjectType}:{x.Key}", StringComparer.OrdinalIgnoreCase);
        var matched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var sourceModule in source.Modules)
        {
            var key = $"{sourceModule.ObjectType}:{sourceModule.Key}";
            if (!targetByKey.TryGetValue(key, out var targetModule))
            {
                results.Add(NewDifference(sourceModule.ObjectType, sourceModule.Schema, sourceModule.Name, SchemaChangeType.OnlyInSource));
                continue;
            }

            matched.Add(key);

            var difference = NewDifference(sourceModule.ObjectType, sourceModule.Schema, sourceModule.Name, SchemaChangeType.Same);
            AddIfDifferent(difference, CategoryDefinition, sourceModule.Name, "Body",
                NormalizeDefinition(sourceModule.Definition, options),
                NormalizeDefinition(targetModule.Definition, options));

            difference.ChangeType = difference.Details.Count > 0 ? SchemaChangeType.Different : SchemaChangeType.Same;
            results.Add(difference);
        }

        foreach (var targetModule in target.Modules.Where(x => !matched.Contains($"{x.ObjectType}:{x.Key}")))
        {
            results.Add(NewDifference(targetModule.ObjectType, targetModule.Schema, targetModule.Name, SchemaChangeType.OnlyInTarget));
        }
    }

    /// <summary>
    /// Pairs members by explicit name first, then falls back to matching the remainder by
    /// signature. That keeps a system-named constraint on one side matched with an
    /// explicitly named equivalent on the other instead of reporting both as missing.
    /// </summary>
    private static void MatchMembers<T>(
        IReadOnlyList<T> sourceItems,
        IReadOnlyList<T> targetItems,
        Func<T, string> nameOf,
        Func<T, bool> isSystemNamed,
        Func<T, string> signatureOf,
        bool ignoreNames,
        Action<T, T> onPair,
        Action<T> onSourceOnly,
        Action<T> onTargetOnly)
    {
        var pendingSource = sourceItems.ToList();
        var pendingTarget = targetItems.ToList();

        if (!ignoreNames)
        {
            for (var i = pendingSource.Count - 1; i >= 0; i--)
            {
                var sourceItem = pendingSource[i];
                if (isSystemNamed(sourceItem))
                {
                    continue;
                }

                var matchIndex = pendingTarget.FindIndex(candidate =>
                    !isSystemNamed(candidate)
                    && string.Equals(nameOf(candidate), nameOf(sourceItem), StringComparison.OrdinalIgnoreCase));

                if (matchIndex < 0)
                {
                    continue;
                }

                onPair(sourceItem, pendingTarget[matchIndex]);
                pendingTarget.RemoveAt(matchIndex);
                pendingSource.RemoveAt(i);
            }
        }

        for (var i = pendingSource.Count - 1; i >= 0; i--)
        {
            var sourceItem = pendingSource[i];
            var signature = signatureOf(sourceItem);

            var matchIndex = pendingTarget.FindIndex(candidate =>
                string.Equals(signatureOf(candidate), signature, StringComparison.OrdinalIgnoreCase));

            if (matchIndex < 0)
            {
                continue;
            }

            onPair(sourceItem, pendingTarget[matchIndex]);
            pendingTarget.RemoveAt(matchIndex);
            pendingSource.RemoveAt(i);
        }

        foreach (var leftover in pendingSource)
        {
            onSourceOnly(leftover);
        }

        foreach (var leftover in pendingTarget)
        {
            onTargetOnly(leftover);
        }
    }

    private static bool ShouldCompareNames(SchemaCompareOptions options, bool sourceIsSystemNamed, bool targetIsSystemNamed)
    {
        return !options.IgnoreConstraintNames && !sourceIsSystemNamed && !targetIsSystemNamed;
    }

    private static SchemaDifference NewDifference(
        SchemaObjectType objectType,
        string schema,
        string name,
        SchemaChangeType changeType)
    {
        return new SchemaDifference
        {
            ObjectType = objectType,
            Schema = schema,
            Name = name,
            ChangeType = changeType
        };
    }

    private static SchemaPropertyDifference Missing(
        string category,
        string member,
        string? value,
        SchemaChangeType changeType)
    {
        return new SchemaPropertyDifference
        {
            Category = category,
            Member = member,
            ChangeType = changeType,
            SourceValue = changeType == SchemaChangeType.OnlyInSource ? value : null,
            TargetValue = changeType == SchemaChangeType.OnlyInTarget ? value : null
        };
    }

    private static void AddIfDifferent(
        SchemaDifference difference,
        string category,
        string member,
        string property,
        string? sourceValue,
        string? targetValue,
        StringComparison comparison = StringComparison.Ordinal)
    {
        var left = sourceValue ?? string.Empty;
        var right = targetValue ?? string.Empty;

        if (string.Equals(left, right, comparison))
        {
            return;
        }

        difference.Details.Add(new SchemaPropertyDifference
        {
            Category = category,
            Member = member,
            Property = property,
            SourceValue = left,
            TargetValue = right,
            ChangeType = SchemaChangeType.Different
        });
    }

    private static string DescribeColumn(ColumnDefinition column)
    {
        var nullability = column.IsNullable ? "NULL" : "NOT NULL";
        return column.ComputedDefinition is null
            ? $"{column.TypeDisplay} {nullability}"
            : $"AS {column.ComputedDefinition}";
    }

    private static string DescribeIdentity(ColumnDefinition column, SchemaCompareOptions options)
    {
        if (!column.IsIdentity)
        {
            return string.Empty;
        }

        return options.IgnoreIdentitySeed
            ? "IDENTITY"
            : $"IDENTITY({column.IdentitySeed ?? "1"},{column.IdentityIncrement ?? "1"})";
    }

    private static string DescribeComputed(ColumnDefinition column, SchemaCompareOptions options)
    {
        if (string.IsNullOrEmpty(column.ComputedDefinition))
        {
            return string.Empty;
        }

        var body = NormalizeDefinition(column.ComputedDefinition, options);
        return column.IsComputedPersisted ? $"{body} PERSISTED" : body;
    }

    /// <summary>
    /// Applies the noise-reduction options to a definition body.
    /// </summary>
    public static string NormalizeDefinition(string? definition, SchemaCompareOptions options)
    {
        if (string.IsNullOrEmpty(definition))
        {
            return string.Empty;
        }

        var value = definition;

        if (options.IgnoreWhitespaceInDefinitions)
        {
            value = NormalizeTokens(value, options.IgnoreCommentsInDefinitions);
        }
        else if (options.IgnoreCommentsInDefinitions)
        {
            value = NormalizeTokens(value, stripComments: true);
        }

        if (options.IgnoreCaseInDefinitions)
        {
            value = value.ToLowerInvariant();
        }

        return value;
    }

    /// <summary>
    /// Rewrites a definition as its token stream joined by single spaces.
    /// <para>
    /// Collapsing runs of whitespace is not enough: <c>@a INT, @b INT</c> and
    /// <c>@a INT ,@b INT</c> are the same code but stay different strings, which made every
    /// reformatted routine report as changed. Splitting into tokens removes the position of
    /// whitespace around punctuation as well.
    /// </para>
    /// String literals and quoted identifiers are copied verbatim, because whitespace really
    /// does matter inside them.
    /// </summary>
    public static string NormalizeTokens(string sql, bool stripComments)
    {
        var tokens = new List<string>();
        var index = 0;

        while (index < sql.Length)
        {
            var current = sql[index];

            if (char.IsWhiteSpace(current))
            {
                index++;
                continue;
            }

            // Line comment.
            if (current == '-' && index + 1 < sql.Length && sql[index + 1] == '-')
            {
                var start = index;
                while (index < sql.Length && sql[index] != '\n')
                {
                    index++;
                }

                if (!stripComments)
                {
                    tokens.Add(CollapseInner(sql[start..index]));
                }

                continue;
            }

            // Block comment, which SQL Server allows to nest.
            if (current == '/' && index + 1 < sql.Length && sql[index + 1] == '*')
            {
                var start = index;
                var depth = 1;
                index += 2;
                while (index < sql.Length && depth > 0)
                {
                    if (sql[index] == '/' && index + 1 < sql.Length && sql[index + 1] == '*')
                    {
                        depth++;
                        index += 2;
                    }
                    else if (sql[index] == '*' && index + 1 < sql.Length && sql[index + 1] == '/')
                    {
                        depth--;
                        index += 2;
                    }
                    else
                    {
                        index++;
                    }
                }

                if (!stripComments)
                {
                    tokens.Add(CollapseInner(sql[start..index]));
                }

                continue;
            }

            if (current is '\'' or '"')
            {
                tokens.Add(ReadDelimited(sql, ref index, current, current));
                continue;
            }

            if (current == '[')
            {
                tokens.Add(ReadDelimited(sql, ref index, '[', ']'));
                continue;
            }

            if (IsWordChar(current))
            {
                var start = index;
                while (index < sql.Length && IsWordChar(sql[index]))
                {
                    index++;
                }

                tokens.Add(sql[start..index]);
                continue;
            }

            tokens.Add(current.ToString());
            index++;
        }

        return string.Join(' ', tokens);
    }

    // @ # $ and _ are all legal inside T-SQL identifiers and variable names.
    private static bool IsWordChar(char value) =>
        char.IsLetterOrDigit(value) || value is '_' or '@' or '#' or '$';

    private static string ReadDelimited(string sql, ref int index, char open, char close)
    {
        var start = index;
        index++;

        while (index < sql.Length)
        {
            if (sql[index] != close)
            {
                index++;
                continue;
            }

            // A doubled closing character is an escape, not the end.
            if (index + 1 < sql.Length && sql[index + 1] == close)
            {
                index += 2;
                continue;
            }

            index++;
            break;
        }

        return sql[start..index];
    }

    private static string CollapseInner(string text) => WhitespaceRuns().Replace(text, " ").Trim();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRuns();
}
