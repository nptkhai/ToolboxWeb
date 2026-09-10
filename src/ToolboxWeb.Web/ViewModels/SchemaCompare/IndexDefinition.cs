namespace ToolboxWeb.Web.ViewModels.SchemaCompare;

public sealed class IndexColumnDefinition
{
    public required string Name { get; init; }
    public bool IsDescending { get; init; }

    public string Display => IsDescending ? $"{Name} DESC" : Name;
}

public sealed class IndexDefinition
{
    public required string Name { get; init; }
    public bool IsUnique { get; init; }
    public bool IsClustered { get; init; }
    public string? FilterDefinition { get; init; }
    public List<IndexColumnDefinition> KeyColumns { get; init; } = [];
    public List<string> IncludedColumns { get; init; } = [];

    /// <summary>Name-independent fingerprint, used when constraint names are ignored.</summary>
    public string Signature =>
        $"{(IsUnique ? "UNIQUE " : "")}{(IsClustered ? "CLUSTERED" : "NONCLUSTERED")}"
        + $" ({string.Join(", ", KeyColumns.Select(x => x.Display))})"
        + (IncludedColumns.Count > 0 ? $" INCLUDE ({string.Join(", ", IncludedColumns)})" : string.Empty)
        + (string.IsNullOrWhiteSpace(FilterDefinition) ? string.Empty : $" WHERE {FilterDefinition}");
}

public sealed class KeyConstraintDefinition
{
    public required string Name { get; init; }
    public bool IsPrimaryKey { get; init; }
    public bool IsClustered { get; init; }
    public bool IsSystemNamed { get; init; }
    public List<IndexColumnDefinition> Columns { get; init; } = [];

    public string Signature =>
        $"{(IsPrimaryKey ? "PRIMARY KEY" : "UNIQUE")} {(IsClustered ? "CLUSTERED" : "NONCLUSTERED")}"
        + $" ({string.Join(", ", Columns.Select(x => x.Display))})";
}
