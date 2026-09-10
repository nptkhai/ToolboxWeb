namespace ToolboxWeb.Web.ViewModels.SchemaCompare;

/// <summary>
/// A single difference inside an object, for example a column whose data type changed or an
/// index that only exists on one side.
/// </summary>
public sealed class SchemaPropertyDifference
{
    /// <summary>Sub-object family: Column, Index, ForeignKey, CheckConstraint, PrimaryKey, Definition.</summary>
    public required string Category { get; init; }

    /// <summary>Name of the sub-object the change applies to.</summary>
    public required string Member { get; init; }

    /// <summary>Attribute that differs, empty when the whole sub-object is added or removed.</summary>
    public string Property { get; init; } = string.Empty;

    public string? SourceValue { get; init; }
    public string? TargetValue { get; init; }
    public SchemaChangeType ChangeType { get; init; }
}
