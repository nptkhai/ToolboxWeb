using Microsoft.Data.SqlClient;
using ToolboxWeb.Web.ViewModels.SchemaCompare;

namespace ToolboxWeb.Web.Infrastructure.SqlSchema;

public interface ISqlSchemaReader
{
    Task<SqlServerProbeViewModel> ProbeAsync(SqlConnectionInputModel input, CancellationToken cancellationToken = default);

    Task<DatabaseSchema> ReadAsync(
        SqlConnectionInputModel input,
        SchemaCompareOptions options,
        CancellationToken cancellationToken = default);
}

public sealed class SqlSchemaReader : ISqlSchemaReader
{
    private readonly ISqlConnectionStringFactory _connectionStrings;

    public SqlSchemaReader(ISqlConnectionStringFactory connectionStrings)
    {
        _connectionStrings = connectionStrings;
    }

    public async Task<SqlServerProbeViewModel> ProbeAsync(
        SqlConnectionInputModel input,
        CancellationToken cancellationToken = default)
    {
        var connectionString = _connectionStrings.Create(input, requireDatabase: false);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var version = string.Empty;
        await using (var command = new SqlCommand(SqlSchemaQueries.ServerProbe, connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
            {
                version = $"{reader.GetString(0)} ({reader.GetString(1)})";
            }
        }

        var databases = new List<string>();
        await using (var command = new SqlCommand(SqlSchemaQueries.DatabaseList, connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                databases.Add(reader.GetString(0));
            }
        }

        return new SqlServerProbeViewModel
        {
            ServerVersion = version,
            Databases = databases
        };
    }

    public async Task<DatabaseSchema> ReadAsync(
        SqlConnectionInputModel input,
        SchemaCompareOptions options,
        CancellationToken cancellationToken = default)
    {
        var connectionString = _connectionStrings.Create(input, requireDatabase: true);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var schema = new DatabaseSchema
        {
            Server = input.Server.Trim(),
            Database = input.Database.Trim(),
            ServerVersion = connection.ServerVersion ?? string.Empty
        };

        await ReadSchemasAsync(connection, schema, cancellationToken);

        // Tables are the anchor for keys and indexes, so read them whenever either group is on.
        var tablesByKey = new Dictionary<string, TableDefinition>(StringComparer.OrdinalIgnoreCase);
        if (options.IncludeTables || options.IncludeKeysAndIndexes)
        {
            await ReadTablesAsync(connection, schema, tablesByKey, cancellationToken);
        }

        if (options.IncludeKeysAndIndexes)
        {
            await ReadKeyConstraintsAsync(connection, tablesByKey, cancellationToken);
            await ReadForeignKeysAsync(connection, tablesByKey, cancellationToken);
            await ReadCheckConstraintsAsync(connection, tablesByKey, cancellationToken);
            await ReadIndexesAsync(connection, tablesByKey, cancellationToken);
        }

        if (options.IncludeProgrammability)
        {
            await ReadModulesAsync(connection, schema, cancellationToken);
        }

        return schema;
    }

    private static async Task ReadSchemasAsync(
        SqlConnection connection,
        DatabaseSchema schema,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(SqlSchemaQueries.Schemas, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            schema.Schemas.Add(reader.GetString(0));
        }
    }

    private static async Task ReadTablesAsync(
        SqlConnection connection,
        DatabaseSchema schema,
        Dictionary<string, TableDefinition> tablesByKey,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(SqlSchemaQueries.TableColumns, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var table = ResolveTable(reader, schema, tablesByKey);

            table.Columns.Add(new ColumnDefinition
            {
                Name = reader.GetString(reader.GetOrdinal("ColumnName")),
                Ordinal = reader.GetInt32(reader.GetOrdinal("Ordinal")),
                DataType = reader.GetString(reader.GetOrdinal("DataType")),
                MaxLength = reader.GetInt16(reader.GetOrdinal("MaxLength")),
                Precision = reader.GetByte(reader.GetOrdinal("ColumnPrecision")),
                Scale = reader.GetByte(reader.GetOrdinal("ColumnScale")),
                IsNullable = reader.GetBoolean(reader.GetOrdinal("IsNullable")),
                Collation = GetNullableString(reader, "Collation"),
                IsIdentity = reader.GetBoolean(reader.GetOrdinal("IsIdentity")),
                IdentitySeed = GetNullableString(reader, "IdentitySeed"),
                IdentityIncrement = GetNullableString(reader, "IdentityIncrement"),
                ComputedDefinition = GetNullableString(reader, "ComputedDefinition"),
                IsComputedPersisted = GetNullableBoolean(reader, "IsComputedPersisted") ?? false,
                DefaultName = GetNullableString(reader, "DefaultName"),
                DefaultDefinition = GetNullableString(reader, "DefaultDefinition"),
                DefaultIsSystemNamed = GetNullableBoolean(reader, "DefaultIsSystemNamed") ?? false
            });
        }
    }

    private static async Task ReadKeyConstraintsAsync(
        SqlConnection connection,
        Dictionary<string, TableDefinition> tablesByKey,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(SqlSchemaQueries.KeyConstraints, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        // Rows arrive grouped by constraint and ordered by key ordinal.
        KeyConstraintDefinition? current = null;
        var currentKey = string.Empty;

        while (await reader.ReadAsync(cancellationToken))
        {
            if (!TryResolveExistingTable(reader, tablesByKey, out var table))
            {
                continue;
            }

            var constraintName = reader.GetString(reader.GetOrdinal("ConstraintName"));
            var rowKey = $"{table.Key}|{constraintName}";

            if (current is null || !string.Equals(currentKey, rowKey, StringComparison.Ordinal))
            {
                var isPrimaryKey = reader.GetString(reader.GetOrdinal("ConstraintType")).Trim()
                    .Equals("PK", StringComparison.OrdinalIgnoreCase);

                current = new KeyConstraintDefinition
                {
                    Name = constraintName,
                    IsPrimaryKey = isPrimaryKey,
                    IsClustered = reader.GetString(reader.GetOrdinal("IndexType"))
                        .Equals("CLUSTERED", StringComparison.OrdinalIgnoreCase),
                    IsSystemNamed = reader.GetBoolean(reader.GetOrdinal("IsSystemNamed"))
                };
                currentKey = rowKey;

                if (isPrimaryKey)
                {
                    table.PrimaryKey = current;
                }
                else
                {
                    table.UniqueConstraints.Add(current);
                }
            }

            current.Columns.Add(new IndexColumnDefinition
            {
                Name = reader.GetString(reader.GetOrdinal("ColumnName")),
                IsDescending = reader.GetBoolean(reader.GetOrdinal("IsDescending"))
            });
        }
    }

    private static async Task ReadForeignKeysAsync(
        SqlConnection connection,
        Dictionary<string, TableDefinition> tablesByKey,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(SqlSchemaQueries.ForeignKeys, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        ForeignKeyDefinition? current = null;
        var currentKey = string.Empty;

        while (await reader.ReadAsync(cancellationToken))
        {
            if (!TryResolveExistingTable(reader, tablesByKey, out var table))
            {
                continue;
            }

            var foreignKeyName = reader.GetString(reader.GetOrdinal("ForeignKeyName"));
            var rowKey = $"{table.Key}|{foreignKeyName}";

            if (current is null || !string.Equals(currentKey, rowKey, StringComparison.Ordinal))
            {
                current = new ForeignKeyDefinition
                {
                    Name = foreignKeyName,
                    IsSystemNamed = reader.GetBoolean(reader.GetOrdinal("IsSystemNamed")),
                    ReferencedSchema = reader.GetString(reader.GetOrdinal("ReferencedSchema")),
                    ReferencedTable = reader.GetString(reader.GetOrdinal("ReferencedTable")),
                    DeleteAction = reader.GetString(reader.GetOrdinal("DeleteAction")),
                    UpdateAction = reader.GetString(reader.GetOrdinal("UpdateAction")),
                    IsDisabled = reader.GetBoolean(reader.GetOrdinal("IsDisabled")),
                    IsNotForReplication = reader.GetBoolean(reader.GetOrdinal("IsNotForReplication"))
                };
                currentKey = rowKey;
                table.ForeignKeys.Add(current);
            }

            current.Columns.Add(reader.GetString(reader.GetOrdinal("ColumnName")));
            current.ReferencedColumns.Add(reader.GetString(reader.GetOrdinal("ReferencedColumn")));
        }
    }

    private static async Task ReadCheckConstraintsAsync(
        SqlConnection connection,
        Dictionary<string, TableDefinition> tablesByKey,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(SqlSchemaQueries.CheckConstraints, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            if (!TryResolveExistingTable(reader, tablesByKey, out var table))
            {
                continue;
            }

            table.CheckConstraints.Add(new CheckConstraintDefinition
            {
                Name = reader.GetString(reader.GetOrdinal("ConstraintName")),
                IsSystemNamed = reader.GetBoolean(reader.GetOrdinal("IsSystemNamed")),
                Definition = GetNullableString(reader, "ConstraintDefinition") ?? string.Empty,
                IsDisabled = reader.GetBoolean(reader.GetOrdinal("IsDisabled")),
                IsNotForReplication = reader.GetBoolean(reader.GetOrdinal("IsNotForReplication"))
            });
        }
    }

    private static async Task ReadIndexesAsync(
        SqlConnection connection,
        Dictionary<string, TableDefinition> tablesByKey,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(SqlSchemaQueries.Indexes, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        IndexDefinition? current = null;
        var currentKey = string.Empty;

        while (await reader.ReadAsync(cancellationToken))
        {
            if (!TryResolveExistingTable(reader, tablesByKey, out var table))
            {
                continue;
            }

            var indexName = reader.GetString(reader.GetOrdinal("IndexName"));
            var rowKey = $"{table.Key}|{indexName}";

            if (current is null || !string.Equals(currentKey, rowKey, StringComparison.Ordinal))
            {
                current = new IndexDefinition
                {
                    Name = indexName,
                    IsUnique = reader.GetBoolean(reader.GetOrdinal("IsUnique")),
                    IsClustered = reader.GetString(reader.GetOrdinal("IndexType"))
                        .Equals("CLUSTERED", StringComparison.OrdinalIgnoreCase),
                    FilterDefinition = GetNullableString(reader, "FilterDefinition")
                };
                currentKey = rowKey;
                table.Indexes.Add(current);
            }

            var columnName = reader.GetString(reader.GetOrdinal("ColumnName"));
            if (reader.GetBoolean(reader.GetOrdinal("IsIncluded")))
            {
                current.IncludedColumns.Add(columnName);
            }
            else
            {
                current.KeyColumns.Add(new IndexColumnDefinition
                {
                    Name = columnName,
                    IsDescending = reader.GetBoolean(reader.GetOrdinal("IsDescending"))
                });
            }
        }
    }

    private static async Task ReadModulesAsync(
        SqlConnection connection,
        DatabaseSchema schema,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(SqlSchemaQueries.Modules, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var rawType = reader.GetString(reader.GetOrdinal("ObjectType")).Trim();
            var objectType = MapObjectType(rawType);
            if (objectType is null)
            {
                continue;
            }

            var parentSchema = GetNullableString(reader, "ParentSchema");
            var parentName = GetNullableString(reader, "ParentName");

            schema.Modules.Add(new ModuleDefinition
            {
                Schema = reader.GetString(reader.GetOrdinal("SchemaName")),
                Name = reader.GetString(reader.GetOrdinal("ObjectName")),
                ObjectType = objectType.Value,
                Definition = GetNullableString(reader, "ModuleDefinition") ?? string.Empty,
                ParentTable = string.IsNullOrEmpty(parentName) ? null : $"{parentSchema}.{parentName}"
            });
        }
    }

    private static SchemaObjectType? MapObjectType(string sqlType) => sqlType.ToUpperInvariant() switch
    {
        "V" => SchemaObjectType.View,
        "P" => SchemaObjectType.StoredProcedure,
        "FN" => SchemaObjectType.ScalarFunction,
        "IF" or "TF" => SchemaObjectType.TableValuedFunction,
        "TR" => SchemaObjectType.Trigger,
        _ => null
    };

    private static TableDefinition ResolveTable(
        SqlDataReader reader,
        DatabaseSchema schema,
        Dictionary<string, TableDefinition> tablesByKey)
    {
        var schemaName = reader.GetString(reader.GetOrdinal("SchemaName"));
        var tableName = reader.GetString(reader.GetOrdinal("TableName"));
        var key = $"{schemaName}.{tableName}";

        if (tablesByKey.TryGetValue(key, out var existing))
        {
            return existing;
        }

        var table = new TableDefinition
        {
            Schema = schemaName,
            Name = tableName
        };

        tablesByKey[key] = table;
        schema.Tables.Add(table);
        return table;
    }

    private static bool TryResolveExistingTable(
        SqlDataReader reader,
        Dictionary<string, TableDefinition> tablesByKey,
        out TableDefinition table)
    {
        var schemaName = reader.GetString(reader.GetOrdinal("SchemaName"));
        var tableName = reader.GetString(reader.GetOrdinal("TableName"));
        return tablesByKey.TryGetValue($"{schemaName}.{tableName}", out table!);
    }

    private static string? GetNullableString(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetValue(ordinal)?.ToString();
    }

    private static bool? GetNullableBoolean(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetBoolean(ordinal);
    }
}
