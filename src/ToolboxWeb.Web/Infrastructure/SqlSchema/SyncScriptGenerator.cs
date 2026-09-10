using System.Globalization;
using System.Text;
using ToolboxWeb.Web.ViewModels.SchemaCompare;
using static ToolboxWeb.Web.Infrastructure.SqlSchema.SqlScriptSyntax;

namespace ToolboxWeb.Web.Infrastructure.SqlSchema;

public interface ISyncScriptGenerator
{
    string Generate(
        DatabaseSchema source,
        DatabaseSchema target,
        IReadOnlyList<SchemaDifference> differences,
        SchemaCompareOptions options);
}

/// <summary>
/// Builds a one-way (source to target) synchronisation script. The script is never executed
/// by the app: it is written for review and manual execution.
/// </summary>
public sealed class SyncScriptGenerator : ISyncScriptGenerator
{
    private const string GuardLine = "IF @@TRANCOUNT = 0 SET NOEXEC ON;";

    public string Generate(
        DatabaseSchema source,
        DatabaseSchema target,
        IReadOnlyList<SchemaDifference> differences,
        SchemaCompareOptions options)
    {
        var plan = new ScriptPlan();
        var sourceTables = source.Tables.ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);
        var targetTables = target.Tables.ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);
        var sourceModules = source.Modules.ToDictionary(ModuleKey, StringComparer.OrdinalIgnoreCase);
        var targetModules = target.Modules.ToDictionary(ModuleKey, StringComparer.OrdinalIgnoreCase);

        foreach (var difference in differences)
        {
            switch (difference.ObjectType)
            {
                case SchemaObjectType.Schema:
                    if (difference.ChangeType == SchemaChangeType.OnlyInSource)
                    {
                        plan.Schemas.Add(CreateSchema(difference.Name));
                    }

                    break;

                case SchemaObjectType.Table:
                    PlanTable(difference, sourceTables, targetTables, plan);
                    break;

                default:
                    PlanModule(difference, sourceModules, targetModules, plan);
                    break;
            }
        }

        PlanBlockingObjects(target, targetTables, plan);

        return Render(source, target, options, plan);
    }

    // ---------------------------------------------------------------- tables

    private static void PlanTable(
        SchemaDifference difference,
        IReadOnlyDictionary<string, TableDefinition> sourceTables,
        IReadOnlyDictionary<string, TableDefinition> targetTables,
        ScriptPlan plan)
    {
        var key = $"{difference.Schema}.{difference.Name}";

        switch (difference.ChangeType)
        {
            case SchemaChangeType.OnlyInSource when sourceTables.TryGetValue(key, out var newTable):
                PlanNewTable(newTable, plan);
                return;

            case SchemaChangeType.OnlyInTarget when targetTables.TryGetValue(key, out var goneTable):
                plan.Destructive.Add(new DestructiveStatement(
                    "TABLE", goneTable.QuotedName, DropTable(goneTable)));
                return;

            case SchemaChangeType.Different
                when sourceTables.TryGetValue(key, out var sourceTable)
                     && targetTables.TryGetValue(key, out var targetTable):
                PlanChangedTable(difference, sourceTable, targetTable, plan);
                return;
        }
    }

    private static void PlanNewTable(TableDefinition table, ScriptPlan plan)
    {
        plan.NewTables.Add(CreateTable(table));

        if (table.PrimaryKey is not null)
        {
            plan.Keys.Add(AddKeyConstraint(table, table.PrimaryKey));
        }

        foreach (var unique in table.UniqueConstraints)
        {
            plan.Keys.Add(AddKeyConstraint(table, unique));
        }

        foreach (var check in table.CheckConstraints)
        {
            plan.Checks.Add(AddCheckConstraint(table, check));
        }

        foreach (var index in table.Indexes)
        {
            plan.Indexes.Add(CreateIndex(table, index));
        }

        foreach (var foreignKey in table.ForeignKeys)
        {
            plan.ForeignKeys.Add(AddForeignKey(table, foreignKey));
        }
    }

    private static void PlanChangedTable(
        SchemaDifference difference,
        TableDefinition sourceTable,
        TableDefinition targetTable,
        ScriptPlan plan)
    {
        var state = plan.StateFor(targetTable);

        foreach (var detail in difference.Details)
        {
            switch (detail.Category)
            {
                case "Column":
                    PlanColumn(detail, sourceTable, targetTable, plan, state);
                    break;

                case "PrimaryKey":
                case "UniqueConstraint":
                    PlanKeyConstraint(detail, sourceTable, targetTable, plan, state);
                    break;

                case "CheckConstraint":
                    PlanCheckConstraint(detail, sourceTable, targetTable, plan, state);
                    break;

                case "Index":
                    PlanIndex(detail, sourceTable, targetTable, plan, state);
                    break;

                case "ForeignKey":
                    PlanForeignKey(detail, sourceTable, targetTable, plan, state);
                    break;
            }
        }
    }

    private static void PlanColumn(
        SchemaPropertyDifference detail,
        TableDefinition sourceTable,
        TableDefinition targetTable,
        ScriptPlan plan,
        TableState state)
    {
        var sourceColumn = FindColumn(sourceTable, detail.Member);
        var targetColumn = FindColumn(targetTable, detail.Member);

        switch (detail.ChangeType)
        {
            case SchemaChangeType.OnlyInSource when sourceColumn is not null:
                if (!sourceColumn.IsNullable
                    && string.IsNullOrEmpty(sourceColumn.DefaultDefinition)
                    && string.IsNullOrEmpty(sourceColumn.ComputedDefinition)
                    && !sourceColumn.IsIdentity)
                {
                    plan.Columns.Add(Warning(
                        $"Column {Quote(sourceColumn.Name)} is NOT NULL without a default. "
                        + "The statement below fails if the table already has rows."));
                }

                plan.Columns.Add(AddColumn(targetTable, sourceColumn));
                return;

            case SchemaChangeType.OnlyInTarget when targetColumn is not null:
                plan.Destructive.Add(new DestructiveStatement(
                    "COLUMN",
                    $"{targetTable.QuotedName}.{Quote(targetColumn.Name)}",
                    DropColumn(targetTable, targetColumn)));
                return;

            case SchemaChangeType.Different when sourceColumn is not null && targetColumn is not null:
                PlanChangedColumn(detail, sourceColumn, targetColumn, targetTable, plan, state);
                return;
        }
    }

    private static void PlanChangedColumn(
        SchemaPropertyDifference detail,
        ColumnDefinition sourceColumn,
        ColumnDefinition targetColumn,
        TableDefinition targetTable,
        ScriptPlan plan,
        TableState state)
    {
        switch (detail.Property)
        {
            case "Default":
            case "DefaultName":
                if (!state.HandledDefaults.Add(targetColumn.Name))
                {
                    return;
                }

                if (!string.IsNullOrEmpty(targetColumn.DefaultName))
                {
                    plan.Defaults.Add(DropConstraintIfExists(targetTable, targetColumn.DefaultName));
                }

                if (!string.IsNullOrEmpty(sourceColumn.DefaultDefinition))
                {
                    plan.Defaults.Add(AddDefault(targetTable, sourceColumn));
                }

                return;

            case "Identity":
            case "Computed":
                plan.Columns.Add(Warning(
                    $"Column {Qualified(targetTable)}.{Quote(targetColumn.Name)} differs by "
                    + $"{detail.Property.ToLowerInvariant()} ('{detail.TargetValue}' -> '{detail.SourceValue}'). "
                    + "SQL Server cannot change this with ALTER COLUMN; the table must be rebuilt manually."));
                return;

            default:
                if (!state.HandledColumnAlters.Add(targetColumn.Name))
                {
                    return;
                }

                state.AlteredColumns.Add(targetColumn.Name);
                plan.Columns.Add(AlterColumn(targetTable, sourceColumn));
                return;
        }
    }

    private static void PlanKeyConstraint(
        SchemaPropertyDifference detail,
        TableDefinition sourceTable,
        TableDefinition targetTable,
        ScriptPlan plan,
        TableState state)
    {
        var sourceKeys = AllKeyConstraints(sourceTable);
        var targetKeys = AllKeyConstraints(targetTable);
        var targetName = ResolveTargetName(detail);

        switch (detail.ChangeType)
        {
            case SchemaChangeType.OnlyInSource:
                var added = sourceKeys.FirstOrDefault(x => Matches(x.Name, detail.Member));
                if (added is not null)
                {
                    state.HandledKeys.Add(added.Name);
                    plan.Keys.Add(AddKeyConstraint(targetTable, added));
                }

                return;

            case SchemaChangeType.OnlyInTarget:
                var removed = targetKeys.FirstOrDefault(x => Matches(x.Name, detail.Member));
                if (removed is not null)
                {
                    state.HandledKeys.Add(removed.Name);
                    plan.Destructive.Add(new DestructiveStatement(
                        "CONSTRAINT",
                        $"{targetTable.QuotedName}.{Quote(removed.Name)}",
                        DropConstraint(targetTable, removed.Name)));
                }

                return;

            case SchemaChangeType.Different:
                var desired = sourceKeys.FirstOrDefault(x => Matches(x.Name, detail.Member));
                var current = targetKeys.FirstOrDefault(x => Matches(x.Name, targetName));
                if (desired is null || current is null || !state.HandledKeys.Add(current.Name))
                {
                    return;
                }

                plan.PreDrops.Add(DropConstraint(targetTable, current.Name));
                plan.Keys.Add(AddKeyConstraint(targetTable, desired));
                return;
        }
    }

    private static void PlanCheckConstraint(
        SchemaPropertyDifference detail,
        TableDefinition sourceTable,
        TableDefinition targetTable,
        ScriptPlan plan,
        TableState state)
    {
        var targetName = ResolveTargetName(detail);

        switch (detail.ChangeType)
        {
            case SchemaChangeType.OnlyInSource:
                var added = sourceTable.CheckConstraints.FirstOrDefault(x => Matches(x.Name, detail.Member));
                if (added is not null)
                {
                    plan.Checks.Add(AddCheckConstraint(targetTable, added));
                }

                return;

            case SchemaChangeType.OnlyInTarget:
                var removed = targetTable.CheckConstraints.FirstOrDefault(x => Matches(x.Name, detail.Member));
                if (removed is not null)
                {
                    plan.Destructive.Add(new DestructiveStatement(
                        "CONSTRAINT",
                        $"{targetTable.QuotedName}.{Quote(removed.Name)}",
                        DropConstraint(targetTable, removed.Name)));
                }

                return;

            case SchemaChangeType.Different:
                var desired = sourceTable.CheckConstraints.FirstOrDefault(x => Matches(x.Name, detail.Member));
                var current = targetTable.CheckConstraints.FirstOrDefault(x => Matches(x.Name, targetName));
                if (desired is null || current is null || !state.HandledChecks.Add(current.Name))
                {
                    return;
                }

                plan.Checks.Add(DropConstraintIfExists(targetTable, current.Name));
                plan.Checks.Add(AddCheckConstraint(targetTable, desired));
                return;
        }
    }

    private static void PlanIndex(
        SchemaPropertyDifference detail,
        TableDefinition sourceTable,
        TableDefinition targetTable,
        ScriptPlan plan,
        TableState state)
    {
        var targetName = ResolveTargetName(detail);

        switch (detail.ChangeType)
        {
            case SchemaChangeType.OnlyInSource:
                var added = sourceTable.Indexes.FirstOrDefault(x => Matches(x.Name, detail.Member));
                if (added is not null)
                {
                    state.HandledIndexes.Add(added.Name);
                    plan.Indexes.Add(CreateIndex(targetTable, added));
                }

                return;

            case SchemaChangeType.OnlyInTarget:
                var removed = targetTable.Indexes.FirstOrDefault(x => Matches(x.Name, detail.Member));
                if (removed is not null)
                {
                    state.HandledIndexes.Add(removed.Name);
                    plan.Destructive.Add(new DestructiveStatement(
                        "INDEX",
                        $"{Quote(removed.Name)} ON {targetTable.QuotedName}",
                        DropIndex(targetTable, removed)));
                }

                return;

            case SchemaChangeType.Different:
                var desired = sourceTable.Indexes.FirstOrDefault(x => Matches(x.Name, detail.Member));
                var current = targetTable.Indexes.FirstOrDefault(x => Matches(x.Name, targetName));
                if (desired is null || current is null || !state.HandledIndexes.Add(current.Name))
                {
                    return;
                }

                plan.PreDrops.Add(DropIndex(targetTable, current));
                plan.Indexes.Add(CreateIndex(targetTable, desired));
                return;
        }
    }

    private static void PlanForeignKey(
        SchemaPropertyDifference detail,
        TableDefinition sourceTable,
        TableDefinition targetTable,
        ScriptPlan plan,
        TableState state)
    {
        var targetName = ResolveTargetName(detail);

        switch (detail.ChangeType)
        {
            case SchemaChangeType.OnlyInSource:
                var added = sourceTable.ForeignKeys.FirstOrDefault(x => Matches(x.Name, detail.Member));
                if (added is not null)
                {
                    state.HandledForeignKeys.Add(added.Name);
                    plan.ForeignKeys.Add(AddForeignKey(targetTable, added));
                }

                return;

            case SchemaChangeType.OnlyInTarget:
                var removed = targetTable.ForeignKeys.FirstOrDefault(x => Matches(x.Name, detail.Member));
                if (removed is not null)
                {
                    state.HandledForeignKeys.Add(removed.Name);
                    plan.Destructive.Add(new DestructiveStatement(
                        "CONSTRAINT",
                        $"{targetTable.QuotedName}.{Quote(removed.Name)}",
                        DropConstraint(targetTable, removed.Name)));
                }

                return;

            case SchemaChangeType.Different:
                var desired = sourceTable.ForeignKeys.FirstOrDefault(x => Matches(x.Name, detail.Member));
                var current = targetTable.ForeignKeys.FirstOrDefault(x => Matches(x.Name, targetName));
                if (desired is null || current is null || !state.HandledForeignKeys.Add(current.Name))
                {
                    return;
                }

                plan.PreDrops.Add(DropConstraint(targetTable, current.Name));
                plan.ForeignKeys.Add(AddForeignKey(targetTable, desired));
                return;
        }
    }

    /// <summary>
    /// ALTER COLUMN fails while the column participates in an index, key or foreign key, so
    /// anything touching an altered column is dropped up front and recreated afterwards.
    /// Objects the diff already handles are skipped to avoid emitting them twice.
    /// </summary>
    private static void PlanBlockingObjects(
        DatabaseSchema target,
        IReadOnlyDictionary<string, TableDefinition> targetTables,
        ScriptPlan plan)
    {
        // Snapshot: StateFor below can add entries for other tables while we iterate.
        foreach (var (tableKey, state) in plan.States.ToList())
        {
            if (state.AlteredColumns.Count == 0 || !targetTables.TryGetValue(tableKey, out var targetTable))
            {
                continue;
            }

            foreach (var index in targetTable.Indexes)
            {
                var touched = index.KeyColumns.Any(x => state.AlteredColumns.Contains(x.Name))
                    || index.IncludedColumns.Any(state.AlteredColumns.Contains);

                if (!touched || !state.HandledIndexes.Add(index.Name))
                {
                    continue;
                }

                plan.PreDrops.Add(DropIndex(targetTable, index));
                plan.Indexes.Add(CreateIndex(targetTable, index));
            }

            foreach (var key in AllKeyConstraints(targetTable))
            {
                if (!key.Columns.Any(x => state.AlteredColumns.Contains(x.Name)) || !state.HandledKeys.Add(key.Name))
                {
                    continue;
                }

                plan.PreDrops.Add(DropConstraint(targetTable, key.Name));
                plan.Keys.Add(AddKeyConstraint(targetTable, key));
            }

            // Outgoing and incoming foreign keys both block the change.
            foreach (var owner in target.Tables)
            {
                foreach (var foreignKey in owner.ForeignKeys)
                {
                    var isOutgoing = ReferenceEquals(owner, targetTable)
                        && foreignKey.Columns.Any(state.AlteredColumns.Contains);

                    var isIncoming = Matches(foreignKey.ReferencedSchema, targetTable.Schema)
                        && Matches(foreignKey.ReferencedTable, targetTable.Name)
                        && foreignKey.ReferencedColumns.Any(state.AlteredColumns.Contains);

                    if (!isOutgoing && !isIncoming)
                    {
                        continue;
                    }

                    if (!plan.StateFor(owner).HandledForeignKeys.Add(foreignKey.Name))
                    {
                        continue;
                    }

                    plan.PreDrops.Add(DropConstraint(owner, foreignKey.Name));
                    plan.ForeignKeys.Add(AddForeignKey(owner, foreignKey));
                }
            }
        }
    }

    // --------------------------------------------------------------- modules

    private static void PlanModule(
        SchemaDifference difference,
        IReadOnlyDictionary<string, ModuleDefinition> sourceModules,
        IReadOnlyDictionary<string, ModuleDefinition> targetModules,
        ScriptPlan plan)
    {
        var key = $"{difference.ObjectType}:{difference.Schema}.{difference.Name}";

        if (difference.ChangeType == SchemaChangeType.OnlyInTarget)
        {
            if (targetModules.TryGetValue(key, out var goneModule))
            {
                plan.Destructive.Add(new DestructiveStatement(
                    ModuleKeyword(goneModule.ObjectType),
                    goneModule.QuotedName,
                    DropModule(goneModule)));
            }

            return;
        }

        if (difference.ChangeType is not (SchemaChangeType.OnlyInSource or SchemaChangeType.Different)
            || !sourceModules.TryGetValue(key, out var module))
        {
            return;
        }

        var rewritten = ToCreateOrAlter(module.Definition);
        if (rewritten is null)
        {
            plan.Modules.Add(Warning(
                $"Could not locate the CREATE keyword in {module.QuotedName}. "
                + "The original definition is emitted unchanged and may need manual editing.")
                + Nl + module.Definition);
            return;
        }

        plan.Modules.Add(rewritten);
    }

    /// <summary>
    /// Rewrites a module body to CREATE OR ALTER so permissions survive. Requires
    /// SQL Server 2016 SP1 or newer. Returns null when the keyword cannot be located.
    /// </summary>
    public static string? ToCreateOrAlter(string definition)
    {
        var index = FindCreateKeyword(definition);
        if (index < 0)
        {
            return null;
        }

        return string.Concat(
            definition.AsSpan(0, index),
            "CREATE OR ALTER",
            definition.AsSpan(index + "CREATE".Length));
    }

    /// <summary>
    /// Finds the first CREATE token that is not inside a comment or string literal, so a
    /// header comment mentioning "CREATE" does not confuse the rewrite.
    /// </summary>
    private static int FindCreateKeyword(string sql)
    {
        var i = 0;

        while (i < sql.Length)
        {
            if (sql[i] == '-' && i + 1 < sql.Length && sql[i + 1] == '-')
            {
                while (i < sql.Length && sql[i] != '\n')
                {
                    i++;
                }

                continue;
            }

            if (sql[i] == '/' && i + 1 < sql.Length && sql[i + 1] == '*')
            {
                var depth = 1;
                i += 2;
                while (i < sql.Length && depth > 0)
                {
                    if (sql[i] == '/' && i + 1 < sql.Length && sql[i + 1] == '*')
                    {
                        depth++;
                        i += 2;
                    }
                    else if (sql[i] == '*' && i + 1 < sql.Length && sql[i + 1] == '/')
                    {
                        depth--;
                        i += 2;
                    }
                    else
                    {
                        i++;
                    }
                }

                continue;
            }

            if (sql[i] == '\'')
            {
                i++;
                while (i < sql.Length)
                {
                    if (sql[i] != '\'')
                    {
                        i++;
                        continue;
                    }

                    if (i + 1 < sql.Length && sql[i + 1] == '\'')
                    {
                        i += 2;
                        continue;
                    }

                    i++;
                    break;
                }

                continue;
            }

            if ((sql[i] is 'C' or 'c') && IsWordAt(sql, i, "CREATE"))
            {
                return i;
            }

            i++;
        }

        return -1;
    }

    private static bool IsWordAt(string text, int index, string word)
    {
        if (index + word.Length > text.Length
            || string.Compare(text, index, word, 0, word.Length, StringComparison.OrdinalIgnoreCase) != 0)
        {
            return false;
        }

        if (index > 0 && (char.IsLetterOrDigit(text[index - 1]) || text[index - 1] == '_'))
        {
            return false;
        }

        var after = index + word.Length;
        return after >= text.Length || !(char.IsLetterOrDigit(text[after]) || text[after] == '_');
    }

    // -------------------------------------------------------------- rendering

    private static string Render(
        DatabaseSchema source,
        DatabaseSchema target,
        SchemaCompareOptions options,
        ScriptPlan plan)
    {
        var script = new StringBuilder();
        AppendHeader(script, source, target, options, plan);
        AppendDestructiveWarning(script, plan, options.IncludeDropStatements);

        // ANSI_NULLS and QUOTED_IDENTIFIER must be ON for persisted computed columns,
        // filtered indexes and indexed views.
        script.Append("SET ANSI_NULLS ON;").Append(Nl);
        script.Append("SET QUOTED_IDENTIFIER ON;").Append(Nl);
        script.Append("SET XACT_ABORT ON;").Append(Nl);
        script.Append("SET NOCOUNT ON;").Append(Nl);
        script.Append("GO").Append(Nl).Append(Nl);
        script.Append("BEGIN TRANSACTION;").Append(Nl);
        script.Append("GO").Append(Nl).Append(Nl);

        AppendSection(script, "1. Schemas", plan.Schemas);
        AppendSection(script, "2. Drop objects that block column changes", plan.PreDrops);
        AppendSection(script, "3. New tables", plan.NewTables);
        AppendSection(script, "4. Columns", plan.Columns);
        AppendSection(script, "5. Default constraints", plan.Defaults);
        AppendSection(script, "6. Check constraints", plan.Checks);
        AppendSection(script, "7. Primary keys and unique constraints", plan.Keys);
        AppendSection(script, "8. Indexes", plan.Indexes);
        AppendSection(script, "9. Foreign keys", plan.ForeignKeys);
        AppendModules(script, plan.Modules);
        AppendDestructiveSection(script, plan.Destructive, options.IncludeDropStatements);

        script.Append(GuardLine).Append(Nl);
        script.Append("IF @@TRANCOUNT > 0 COMMIT TRANSACTION;").Append(Nl);
        script.Append("GO").Append(Nl).Append(Nl);
        script.Append("SET NOEXEC OFF;").Append(Nl);

        return script.ToString();
    }

    private static void AppendHeader(
        StringBuilder script,
        DatabaseSchema source,
        DatabaseSchema target,
        SchemaCompareOptions options,
        ScriptPlan plan)
    {
        var enabled = new List<string>();
        if (options.IgnoreWhitespaceInDefinitions) enabled.Add("ignore whitespace");
        if (options.IgnoreCaseInDefinitions) enabled.Add("ignore case");
        if (options.IgnoreCollation) enabled.Add("ignore collation");
        if (options.IgnoreIdentitySeed) enabled.Add("ignore identity seed");
        if (options.IgnoreConstraintNames) enabled.Add("ignore constraint names");

        script.Append("/* ===========================================================").Append(Nl);
        script.Append("   ToolboxWeb - SQL Server schema sync script").Append(Nl);
        script.Append(CultureInfo.InvariantCulture, $"   Source    : {source.Label}").Append(Nl);
        script.Append(CultureInfo.InvariantCulture, $"   Target    : {target.Label}").Append(Nl);
        script.Append(CultureInfo.InvariantCulture,
            $"   Generated : {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}").Append(Nl);
        script.Append(CultureInfo.InvariantCulture,
            $"   Options   : {(enabled.Count == 0 ? "none" : string.Join(", ", enabled))}").Append(Nl);
        script.Append(Nl);
        script.Append("   Run this on the TARGET database. Review it first and take a backup.").Append(Nl);
        script.Append("   Requires SQL Server 2016 SP1 or newer for CREATE OR ALTER.").Append(Nl);

        script.Append("   =========================================================== */").Append(Nl).Append(Nl);
    }

    /// <summary>
    /// Lists every object the script removes, at the very top. Buried at the bottom of a
    /// 100k-line script this information is effectively invisible, and it is exactly what a
    /// reviewer needs to see before deciding to run anything.
    /// </summary>
    private static void AppendDestructiveWarning(
        StringBuilder script,
        ScriptPlan plan,
        bool includeDropStatements)
    {
        if (plan.Destructive.Count == 0)
        {
            script.Append("/* No object is removed by this script. */").Append(Nl).Append(Nl);
            return;
        }

        const int listLimit = 60;
        var dataLossKinds = new[] { "TABLE", "COLUMN" };

        script.Append("/* ***********************************************************").Append(Nl);
        script.Append(CultureInfo.InvariantCulture,
            $"   READ THIS FIRST - {plan.Destructive.Count} object(s) are removed by this script").Append(Nl);
        script.Append(Nl);
        script.Append(includeDropStatements
            ? "   STATUS: DROP statements are ENABLED and WILL RUN (section 11)."
            : "   STATUS: DROP statements are COMMENTED OUT. Nothing is removed unless").Append(Nl);

        if (!includeDropStatements)
        {
            script.Append("           you uncomment section 11 yourself.").Append(Nl);
        }

        script.Append(Nl);
        script.Append("   Summary by object type:").Append(Nl);

        foreach (var group in plan.Destructive
                     .GroupBy(x => x.Kind, StringComparer.OrdinalIgnoreCase)
                     .OrderByDescending(x => x.Count()))
        {
            var marker = dataLossKinds.Contains(group.Key, StringComparer.OrdinalIgnoreCase)
                ? "  <-- DATA LOSS"
                : string.Empty;

            script.Append(CultureInfo.InvariantCulture,
                $"     DROP {group.Key,-12} {group.Count(),6}{marker}").Append(Nl);
        }

        // Tables and columns destroy data, so name every one of them.
        foreach (var kind in dataLossKinds)
        {
            var items = plan.Destructive
                .Where(x => string.Equals(x.Kind, kind, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (items.Count == 0)
            {
                continue;
            }

            script.Append(Nl);
            script.Append(CultureInfo.InvariantCulture,
                $"   {kind}(s) that would be dropped - the data in them is lost:").Append(Nl);

            foreach (var item in items.Take(listLimit))
            {
                script.Append(CultureInfo.InvariantCulture, $"     {item.Target}").Append(Nl);
            }

            if (items.Count > listLimit)
            {
                script.Append(CultureInfo.InvariantCulture,
                    $"     ... and {items.Count - listLimit} more, see section 11").Append(Nl);
            }
        }

        script.Append(Nl);
        script.Append("   Indexes, constraints and routines are also removed; they can be").Append(Nl);
        script.Append("   rebuilt from the source database, so only counts are listed above.").Append(Nl);
        script.Append("   *********************************************************** */").Append(Nl).Append(Nl);
    }

    private static void AppendSection(StringBuilder script, string title, IReadOnlyList<string> statements)
    {
        if (statements.Count == 0)
        {
            return;
        }

        script.Append(CultureInfo.InvariantCulture, $"/* --- {title} --- */").Append(Nl);
        script.Append(GuardLine).Append(Nl);

        foreach (var statement in statements)
        {
            script.Append(statement).Append(Nl);
        }

        script.Append("GO").Append(Nl).Append(Nl);
    }

    private static void AppendModules(StringBuilder script, IReadOnlyList<string> modules)
    {
        if (modules.Count == 0)
        {
            return;
        }

        script.Append("/* --- 10. Views, procedures, functions and triggers --- */").Append(Nl).Append(Nl);

        // CREATE OR ALTER must be the only statement in its batch.
        foreach (var module in modules)
        {
            script.Append(GuardLine).Append(Nl);
            script.Append("GO").Append(Nl);
            script.Append(module.TrimEnd()).Append(Nl);
            script.Append("GO").Append(Nl).Append(Nl);
        }
    }

    private static void AppendDestructiveSection(
        StringBuilder script,
        IReadOnlyList<DestructiveStatement> statements,
        bool includeDropStatements)
    {
        if (statements.Count == 0)
        {
            return;
        }

        script.Append("/* --- 11. Objects that exist only in the target --- */").Append(Nl);

        if (!includeDropStatements)
        {
            script.Append("/* These statements DELETE objects and the data they hold.").Append(Nl);
            script.Append("   They are commented out. Re-run the compare with").Append(Nl);
            script.Append("   \"include DROP statements\" enabled, or uncomment them yourself. */").Append(Nl);
        }
        else
        {
            script.Append("/* WARNING: these statements DELETE objects and the data they hold. */").Append(Nl);
        }

        script.Append(GuardLine).Append(Nl);

        // Grouped by kind so the reviewer can uncomment one category at a time.
        foreach (var group in statements.GroupBy(x => x.Kind, StringComparer.OrdinalIgnoreCase))
        {
            script.Append(CultureInfo.InvariantCulture, $"-- {group.Key} ({group.Count()})").Append(Nl);
            foreach (var statement in group)
            {
                script.Append(includeDropStatements ? statement.Sql : Comment(statement.Sql)).Append(Nl);
            }
        }

        script.Append("GO").Append(Nl).Append(Nl);
    }

    private static string Comment(string statement) =>
        string.Join(Nl, statement.Split(Nl).Select(line => $"-- {line}"));

    private static string Warning(string message) => $"-- WARNING: {message}";

    // ---------------------------------------------------------------- helpers

    private static string ModuleKey(ModuleDefinition module) => $"{module.ObjectType}:{module.Key}";

    private static bool Matches(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Pairs matched by signature can carry different names; in that case the detail records
    /// the target name explicitly.
    /// </summary>
    private static string ResolveTargetName(SchemaPropertyDifference detail) =>
        detail.Property == "Name" && !string.IsNullOrEmpty(detail.TargetValue)
            ? detail.TargetValue
            : detail.Member;

    private static ColumnDefinition? FindColumn(TableDefinition table, string name) =>
        table.Columns.FirstOrDefault(x => Matches(x.Name, name));

    private static IEnumerable<KeyConstraintDefinition> AllKeyConstraints(TableDefinition table)
    {
        if (table.PrimaryKey is not null)
        {
            yield return table.PrimaryKey;
        }

        foreach (var unique in table.UniqueConstraints)
        {
            yield return unique;
        }
    }

    private sealed class TableState
    {
        public HashSet<string> AlteredColumns { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> HandledColumnAlters { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> HandledDefaults { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> HandledKeys { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> HandledChecks { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> HandledIndexes { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> HandledForeignKeys { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A statement that removes an object. Kept as structured data so the script can warn
    /// about it up front instead of only at the bottom.
    /// </summary>
    private sealed record DestructiveStatement(string Kind, string Target, string Sql);

    private sealed class ScriptPlan
    {
        public List<string> Schemas { get; } = [];
        public List<string> PreDrops { get; } = [];
        public List<string> NewTables { get; } = [];
        public List<string> Columns { get; } = [];
        public List<string> Defaults { get; } = [];
        public List<string> Checks { get; } = [];
        public List<string> Keys { get; } = [];
        public List<string> Indexes { get; } = [];
        public List<string> ForeignKeys { get; } = [];
        public List<string> Modules { get; } = [];
        public List<DestructiveStatement> Destructive { get; } = [];

        public Dictionary<string, TableState> States { get; } = new(StringComparer.OrdinalIgnoreCase);

        public TableState StateFor(TableDefinition table)
        {
            if (States.TryGetValue(table.Key, out var existing))
            {
                return existing;
            }

            var state = new TableState();
            States[table.Key] = state;
            return state;
        }
    }
}
