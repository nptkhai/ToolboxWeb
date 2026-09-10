namespace ToolboxWeb.Web.ViewModels.SchemaCompare;

/// <summary>
/// A programmable object whose body lives in <c>sys.sql_modules</c>: view, procedure,
/// function or trigger.
/// </summary>
public sealed class ModuleDefinition
{
    public required string Schema { get; init; }
    public required string Name { get; init; }
    public required SchemaObjectType ObjectType { get; init; }
    public required string Definition { get; init; }

    /// <summary>Owning table for triggers, otherwise null.</summary>
    public string? ParentTable { get; init; }

    public string Key => $"{Schema}.{Name}";
    public string QuotedName => $"[{Schema}].[{Name}]";
}
