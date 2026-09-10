namespace ToolboxWeb.Web.Infrastructure.SqlSchema;

/// <summary>
/// Read-only catalog queries. Everything here is a SELECT against <c>sys.*</c>; this module
/// never issues DDL or DML against a user's server.
/// </summary>
internal static class SqlSchemaQueries
{
    public const string ServerProbe = """
        SELECT CONVERT(nvarchar(200), SERVERPROPERTY('ProductVersion')) AS ProductVersion,
               CONVERT(nvarchar(200), SERVERPROPERTY('Edition')) AS Edition;
        """;

    public const string DatabaseList = """
        SELECT name
        FROM sys.databases
        WHERE state = 0
          AND HAS_DBACCESS(name) = 1
          AND name <> N'tempdb'
        ORDER BY name;
        """;

    public const string Schemas = """
        SELECT s.name AS SchemaName
        FROM sys.schemas s
        WHERE s.name NOT IN (N'sys', N'INFORMATION_SCHEMA', N'guest')
          AND s.name NOT LIKE N'db\_%' ESCAPE N'\'
        ORDER BY s.name;
        """;

    public const string TableColumns = """
        SELECT s.name  AS SchemaName,
               t.name  AS TableName,
               c.column_id      AS Ordinal,
               c.name           AS ColumnName,
               ty.name          AS DataType,
               c.max_length     AS MaxLength,
               c.precision      AS ColumnPrecision,
               c.scale          AS ColumnScale,
               c.is_nullable    AS IsNullable,
               c.collation_name AS Collation,
               c.is_identity    AS IsIdentity,
               CONVERT(nvarchar(64), ic.seed_value)      AS IdentitySeed,
               CONVERT(nvarchar(64), ic.increment_value) AS IdentityIncrement,
               cc.definition    AS ComputedDefinition,
               cc.is_persisted  AS IsComputedPersisted,
               dc.name          AS DefaultName,
               dc.definition    AS DefaultDefinition,
               dc.is_system_named AS DefaultIsSystemNamed
        FROM sys.tables t
        JOIN sys.schemas s ON s.schema_id = t.schema_id
        JOIN sys.columns c ON c.object_id = t.object_id
        JOIN sys.types ty ON ty.user_type_id = c.user_type_id
        LEFT JOIN sys.identity_columns ic
               ON ic.object_id = c.object_id AND ic.column_id = c.column_id
        LEFT JOIN sys.computed_columns cc
               ON cc.object_id = c.object_id AND cc.column_id = c.column_id
        LEFT JOIN sys.default_constraints dc
               ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
        WHERE t.is_ms_shipped = 0
        ORDER BY s.name, t.name, c.column_id;
        """;

    public const string KeyConstraints = """
        SELECT s.name  AS SchemaName,
               t.name  AS TableName,
               kc.name AS ConstraintName,
               kc.type AS ConstraintType,
               kc.is_system_named AS IsSystemNamed,
               i.type_desc AS IndexType,
               col.name AS ColumnName,
               ic.key_ordinal AS KeyOrdinal,
               ic.is_descending_key AS IsDescending
        FROM sys.key_constraints kc
        JOIN sys.tables t  ON t.object_id = kc.parent_object_id
        JOIN sys.schemas s ON s.schema_id = t.schema_id
        JOIN sys.indexes i ON i.object_id = kc.parent_object_id AND i.index_id = kc.unique_index_id
        JOIN sys.index_columns ic
             ON ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 0
        JOIN sys.columns col ON col.object_id = ic.object_id AND col.column_id = ic.column_id
        WHERE t.is_ms_shipped = 0
        ORDER BY s.name, t.name, kc.name, ic.key_ordinal;
        """;

    public const string ForeignKeys = """
        SELECT s.name  AS SchemaName,
               t.name  AS TableName,
               fk.name AS ForeignKeyName,
               fk.is_system_named AS IsSystemNamed,
               rs.name AS ReferencedSchema,
               rt.name AS ReferencedTable,
               pc.name AS ColumnName,
               rc.name AS ReferencedColumn,
               fk.delete_referential_action_desc AS DeleteAction,
               fk.update_referential_action_desc AS UpdateAction,
               fk.is_disabled AS IsDisabled,
               fk.is_not_for_replication AS IsNotForReplication
        FROM sys.foreign_keys fk
        JOIN sys.tables t   ON t.object_id = fk.parent_object_id
        JOIN sys.schemas s  ON s.schema_id = t.schema_id
        JOIN sys.tables rt  ON rt.object_id = fk.referenced_object_id
        JOIN sys.schemas rs ON rs.schema_id = rt.schema_id
        JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
        JOIN sys.columns pc ON pc.object_id = fkc.parent_object_id AND pc.column_id = fkc.parent_column_id
        JOIN sys.columns rc ON rc.object_id = fkc.referenced_object_id AND rc.column_id = fkc.referenced_column_id
        WHERE t.is_ms_shipped = 0
        ORDER BY s.name, t.name, fk.name, fkc.constraint_column_id;
        """;

    public const string CheckConstraints = """
        SELECT s.name  AS SchemaName,
               t.name  AS TableName,
               cc.name AS ConstraintName,
               cc.is_system_named AS IsSystemNamed,
               cc.definition AS ConstraintDefinition,
               cc.is_disabled AS IsDisabled,
               cc.is_not_for_replication AS IsNotForReplication
        FROM sys.check_constraints cc
        JOIN sys.tables t  ON t.object_id = cc.parent_object_id
        JOIN sys.schemas s ON s.schema_id = t.schema_id
        WHERE t.is_ms_shipped = 0
        ORDER BY s.name, t.name, cc.name;
        """;

    public const string Indexes = """
        SELECT s.name AS SchemaName,
               t.name AS TableName,
               i.name AS IndexName,
               i.is_unique AS IsUnique,
               i.type_desc AS IndexType,
               i.filter_definition AS FilterDefinition,
               col.name AS ColumnName,
               ic.is_included_column AS IsIncluded,
               ic.key_ordinal AS KeyOrdinal,
               ic.index_column_id AS IndexColumnId,
               ic.is_descending_key AS IsDescending
        FROM sys.indexes i
        JOIN sys.tables t  ON t.object_id = i.object_id
        JOIN sys.schemas s ON s.schema_id = t.schema_id
        JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
        JOIN sys.columns col ON col.object_id = ic.object_id AND col.column_id = ic.column_id
        WHERE t.is_ms_shipped = 0
          AND i.type IN (1, 2)
          AND i.is_hypothetical = 0
          AND i.is_primary_key = 0
          AND i.is_unique_constraint = 0
          AND i.name IS NOT NULL
        ORDER BY s.name, t.name, i.name, ic.is_included_column, ic.key_ordinal, ic.index_column_id;
        """;

    public const string Modules = """
        SELECT s.name AS SchemaName,
               o.name AS ObjectName,
               o.type AS ObjectType,
               m.definition AS ModuleDefinition,
               OBJECT_SCHEMA_NAME(o.parent_object_id) AS ParentSchema,
               OBJECT_NAME(o.parent_object_id) AS ParentName
        FROM sys.objects o
        JOIN sys.schemas s ON s.schema_id = o.schema_id
        JOIN sys.sql_modules m ON m.object_id = o.object_id
        WHERE o.is_ms_shipped = 0
          AND o.type IN ('V', 'P', 'FN', 'IF', 'TF', 'TR')
        ORDER BY s.name, o.name;
        """;

    /// <summary>
    /// Lightweight module snapshot used after DacFx comparison to remove differences caused
    /// only by mapped database names. Reading all definitions in one catalog query is several
    /// orders of magnitude cheaper than asking DacFx to script every difference node.
    /// </summary>
    public const string ModuleDefinitionsForMapping = """
        SELECT s.name AS SchemaName,
               o.name AS ObjectName,
               m.definition AS ModuleDefinition,
               m.uses_ansi_nulls AS UsesAnsiNulls,
               m.uses_quoted_identifier AS UsesQuotedIdentifier,
               m.is_schema_bound AS IsSchemaBound,
               m.uses_database_collation AS UsesDatabaseCollation,
               m.is_recompiled AS IsRecompiled,
               m.null_on_null_input AS NullOnNullInput,
               m.uses_native_compilation AS UsesNativeCompilation,
               CASE
                   WHEN m.execute_as_principal_id = -2 THEN N'OWNER'
                   WHEN m.execute_as_principal_id IS NULL THEN NULL
                   ELSE USER_NAME(m.execute_as_principal_id)
               END AS ExecuteAsPrincipal
        FROM sys.objects o
        JOIN sys.schemas s ON s.schema_id = o.schema_id
        JOIN sys.sql_modules m ON m.object_id = o.object_id
        WHERE o.is_ms_shipped = 0
          AND o.type IN ('V', 'P', 'FN', 'IF', 'TF', 'TR');
        """;
}
