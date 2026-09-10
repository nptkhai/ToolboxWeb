namespace ToolboxWeb.Web.ViewModels.SchemaCompare;

public enum SchemaChangeType
{
    Same,
    OnlyInSource,
    OnlyInTarget,
    Different
}

/// <summary>
/// One row in the compare grid: a top-level object plus the property-level changes found
/// inside it.
/// </summary>
public sealed class SchemaDifference
{
    public required SchemaObjectType ObjectType { get; init; }
    public required string Schema { get; init; }
    public required string Name { get; init; }
    public SchemaChangeType ChangeType { get; set; }
    public List<SchemaPropertyDifference> Details { get; } = [];

    public string Key => $"{ObjectType}:{Schema}.{Name}";
}
