namespace ToolboxWeb.Web.ViewModels.SchemaCompare;

public sealed class ForeignKeyDefinition
{
    public required string Name { get; init; }
    public bool IsSystemNamed { get; init; }
    public required string ReferencedSchema { get; init; }
    public required string ReferencedTable { get; init; }
    public List<string> Columns { get; init; } = [];
    public List<string> ReferencedColumns { get; init; } = [];
    public string DeleteAction { get; init; } = "NO_ACTION";
    public string UpdateAction { get; init; } = "NO_ACTION";
    public bool IsDisabled { get; init; }
    public bool IsNotForReplication { get; init; }

    public string Signature =>
        $"({string.Join(", ", Columns)}) REFERENCES [{ReferencedSchema}].[{ReferencedTable}]"
        + $" ({string.Join(", ", ReferencedColumns)})"
        + $" ON DELETE {DeleteAction} ON UPDATE {UpdateAction}";
}

public sealed class CheckConstraintDefinition
{
    public required string Name { get; init; }
    public bool IsSystemNamed { get; init; }
    public required string Definition { get; init; }
    public bool IsDisabled { get; init; }
    public bool IsNotForReplication { get; init; }
}
