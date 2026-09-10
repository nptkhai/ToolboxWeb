namespace ToolboxWeb.Web.ViewModels.SchemaCompare;

public enum SchemaObjectType
{
    Schema,
    Table,
    View,
    StoredProcedure,
    ScalarFunction,
    TableValuedFunction,
    Trigger
}

public sealed class DatabaseSchema
{
    public required string Server { get; init; }
    public required string Database { get; init; }
    public string ServerVersion { get; init; } = string.Empty;

    public List<string> Schemas { get; } = [];
    public List<TableDefinition> Tables { get; } = [];
    public List<ModuleDefinition> Modules { get; } = [];

    public string Label => $"{Server} / {Database}";
}
