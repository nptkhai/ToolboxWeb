namespace ToolboxWeb.Web.ViewModels.SchemaCompare;

public sealed class TableDefinition
{
    public required string Schema { get; init; }
    public required string Name { get; init; }

    public List<ColumnDefinition> Columns { get; } = [];
    public KeyConstraintDefinition? PrimaryKey { get; set; }
    public List<KeyConstraintDefinition> UniqueConstraints { get; } = [];
    public List<ForeignKeyDefinition> ForeignKeys { get; } = [];
    public List<CheckConstraintDefinition> CheckConstraints { get; } = [];
    public List<IndexDefinition> Indexes { get; } = [];

    public string Key => $"{Schema}.{Name}";
    public string QuotedName => $"[{Schema}].[{Name}]";
}
