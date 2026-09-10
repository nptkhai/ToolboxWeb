using System.Globalization;
using System.Text;
using ToolboxWeb.Web.ViewModels.SchemaCompare;

namespace ToolboxWeb.Web.Infrastructure.SqlSchema;

/// <summary>
/// Renders schema model objects back into T-SQL fragments.
/// </summary>
internal static class SqlScriptSyntax
{
    /// <summary>Scripts are downloaded as .sql and opened in SSMS, so they are always CRLF.</summary>
    public const string Nl = "\r\n";

    public static string Quote(string name) => $"[{name.Replace("]", "]]", StringComparison.Ordinal)}]";

    public static string Qualified(string schema, string name) => $"{Quote(schema)}.{Quote(name)}";

    public static string Qualified(TableDefinition table) => Qualified(table.Schema, table.Name);

    public static string ColumnList(IEnumerable<IndexColumnDefinition> columns) =>
        string.Join(", ", columns.Select(x => $"{Quote(x.Name)} {(x.IsDescending ? "DESC" : "ASC")}"));

    public static string ColumnList(IEnumerable<string> columns) =>
        string.Join(", ", columns.Select(Quote));

    /// <summary>
    /// Column definition as it appears inside CREATE TABLE or ALTER TABLE ADD.
    /// Defaults are inlined only where SQL Server accepts them.
    /// </summary>
    public static string ColumnClause(ColumnDefinition column, bool includeDefault)
    {
        if (!string.IsNullOrEmpty(column.ComputedDefinition))
        {
            var persisted = column.IsComputedPersisted ? " PERSISTED" : string.Empty;
            return $"{Quote(column.Name)} AS {column.ComputedDefinition}{persisted}";
        }

        var builder = new StringBuilder($"{Quote(column.Name)} {column.TypeDisplay}");

        if (!string.IsNullOrEmpty(column.Collation))
        {
            builder.Append(CultureInfo.InvariantCulture, $" COLLATE {column.Collation}");
        }

        if (column.IsIdentity)
        {
            builder.Append(CultureInfo.InvariantCulture,
                $" IDENTITY({column.IdentitySeed ?? "1"},{column.IdentityIncrement ?? "1"})");
        }

        builder.Append(column.IsNullable ? " NULL" : " NOT NULL");

        if (includeDefault && !string.IsNullOrEmpty(column.DefaultDefinition))
        {
            if (!column.DefaultIsSystemNamed && !string.IsNullOrEmpty(column.DefaultName))
            {
                builder.Append(CultureInfo.InvariantCulture, $" CONSTRAINT {Quote(column.DefaultName)}");
            }

            builder.Append(CultureInfo.InvariantCulture, $" DEFAULT {column.DefaultDefinition}");
        }

        return builder.ToString();
    }

    /// <summary>ALTER COLUMN cannot carry IDENTITY or DEFAULT, so this variant omits them.</summary>
    public static string AlterColumnClause(ColumnDefinition column)
    {
        var builder = new StringBuilder($"{Quote(column.Name)} {column.TypeDisplay}");

        if (!string.IsNullOrEmpty(column.Collation))
        {
            builder.Append(CultureInfo.InvariantCulture, $" COLLATE {column.Collation}");
        }

        builder.Append(column.IsNullable ? " NULL" : " NOT NULL");
        return builder.ToString();
    }

    public static string CreateTable(TableDefinition table)
    {
        var columns = table.Columns
            .OrderBy(x => x.Ordinal)
            .Select(x => $"    {ColumnClause(x, includeDefault: true)}");

        return $"CREATE TABLE {Qualified(table)} ({Nl}{string.Join($",{Nl}", columns)}{Nl});";
    }

    public static string AddColumn(TableDefinition table, ColumnDefinition column) =>
        $"ALTER TABLE {Qualified(table)} ADD {ColumnClause(column, includeDefault: true)};";

    public static string AlterColumn(TableDefinition table, ColumnDefinition column) =>
        $"ALTER TABLE {Qualified(table)} ALTER COLUMN {AlterColumnClause(column)};";

    public static string DropColumn(TableDefinition table, ColumnDefinition column) =>
        $"ALTER TABLE {Qualified(table)} DROP COLUMN {Quote(column.Name)};";

    public static string AddDefault(TableDefinition table, ColumnDefinition column)
    {
        var named = !column.DefaultIsSystemNamed && !string.IsNullOrEmpty(column.DefaultName)
            ? $"CONSTRAINT {Quote(column.DefaultName)} "
            : string.Empty;

        return $"ALTER TABLE {Qualified(table)} ADD {named}DEFAULT {column.DefaultDefinition} FOR {Quote(column.Name)};";
    }

    public static string DropConstraintIfExists(TableDefinition table, string constraintName) =>
        $"IF EXISTS (SELECT 1 FROM sys.objects WHERE name = N'{Escape(constraintName)}'"
        + $" AND parent_object_id = OBJECT_ID(N'{Escape(Qualified(table))}')){Nl}"
        + $"    ALTER TABLE {Qualified(table)} DROP CONSTRAINT {Quote(constraintName)};";

    public static string DropConstraint(TableDefinition table, string constraintName) =>
        $"ALTER TABLE {Qualified(table)} DROP CONSTRAINT {Quote(constraintName)};";

    public static string AddKeyConstraint(TableDefinition table, KeyConstraintDefinition key)
    {
        var kind = key.IsPrimaryKey ? "PRIMARY KEY" : "UNIQUE";
        var clustering = key.IsClustered ? "CLUSTERED" : "NONCLUSTERED";
        return $"ALTER TABLE {Qualified(table)} ADD CONSTRAINT {Quote(key.Name)} "
            + $"{kind} {clustering} ({ColumnList(key.Columns)});";
    }

    public static string AddCheckConstraint(TableDefinition table, CheckConstraintDefinition check)
    {
        var enforcement = check.IsDisabled ? "WITH NOCHECK" : "WITH CHECK";
        var replication = check.IsNotForReplication ? " NOT FOR REPLICATION" : string.Empty;
        return $"ALTER TABLE {Qualified(table)} {enforcement} ADD CONSTRAINT {Quote(check.Name)} "
            + $"CHECK{replication} {check.Definition};";
    }

    public static string CreateIndex(TableDefinition table, IndexDefinition index)
    {
        var unique = index.IsUnique ? "UNIQUE " : string.Empty;
        var clustering = index.IsClustered ? "CLUSTERED" : "NONCLUSTERED";
        var included = index.IncludedColumns.Count > 0
            ? $" INCLUDE ({ColumnList(index.IncludedColumns)})"
            : string.Empty;
        var filter = string.IsNullOrWhiteSpace(index.FilterDefinition)
            ? string.Empty
            : $" WHERE {index.FilterDefinition}";

        return $"CREATE {unique}{clustering} INDEX {Quote(index.Name)} ON {Qualified(table)} "
            + $"({ColumnList(index.KeyColumns)}){included}{filter};";
    }

    public static string DropIndex(TableDefinition table, IndexDefinition index) =>
        $"DROP INDEX {Quote(index.Name)} ON {Qualified(table)};";

    public static string AddForeignKey(TableDefinition table, ForeignKeyDefinition foreignKey)
    {
        var enforcement = foreignKey.IsDisabled ? "WITH NOCHECK" : "WITH CHECK";
        var builder = new StringBuilder(
            $"ALTER TABLE {Qualified(table)} {enforcement} ADD CONSTRAINT {Quote(foreignKey.Name)} "
            + $"FOREIGN KEY ({ColumnList(foreignKey.Columns)}) "
            + $"REFERENCES {Qualified(foreignKey.ReferencedSchema, foreignKey.ReferencedTable)} "
            + $"({ColumnList(foreignKey.ReferencedColumns)})");

        if (!IsNoAction(foreignKey.DeleteAction))
        {
            builder.Append(CultureInfo.InvariantCulture, $" ON DELETE {FormatAction(foreignKey.DeleteAction)}");
        }

        if (!IsNoAction(foreignKey.UpdateAction))
        {
            builder.Append(CultureInfo.InvariantCulture, $" ON UPDATE {FormatAction(foreignKey.UpdateAction)}");
        }

        if (foreignKey.IsNotForReplication)
        {
            builder.Append(" NOT FOR REPLICATION");
        }

        builder.Append(';');
        return builder.ToString();
    }

    public static string CreateSchema(string schemaName) =>
        $"IF SCHEMA_ID(N'{Escape(schemaName)}') IS NULL EXEC(N'CREATE SCHEMA {Quote(schemaName)}');";

    public static string DropTable(TableDefinition table) =>
        $"DROP TABLE {Qualified(table)};";

    public static string DropModule(ModuleDefinition module) =>
        $"DROP {ModuleKeyword(module.ObjectType)} {module.QuotedName};";

    public static string ModuleKeyword(SchemaObjectType objectType) => objectType switch
    {
        SchemaObjectType.View => "VIEW",
        SchemaObjectType.StoredProcedure => "PROCEDURE",
        SchemaObjectType.ScalarFunction or SchemaObjectType.TableValuedFunction => "FUNCTION",
        SchemaObjectType.Trigger => "TRIGGER",
        _ => "OBJECT"
    };

    private static bool IsNoAction(string action) =>
        string.IsNullOrWhiteSpace(action) || action.Equals("NO_ACTION", StringComparison.OrdinalIgnoreCase);

    private static string FormatAction(string action) =>
        action.Replace('_', ' ').ToUpperInvariant();

    private static string Escape(string literal) => literal.Replace("'", "''", StringComparison.Ordinal);
}
