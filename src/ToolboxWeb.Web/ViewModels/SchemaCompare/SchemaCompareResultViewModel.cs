namespace ToolboxWeb.Web.ViewModels.SchemaCompare;

public sealed class SchemaCompareStatsViewModel
{
    public int Total { get; init; }
    public int OnlyInSource { get; init; }
    public int OnlyInTarget { get; init; }
    public int Different { get; init; }
    public int Same { get; init; }
}

public sealed class SchemaDifferenceDetailViewModel
{
    public string Category { get; init; } = string.Empty;
    public string Member { get; init; } = string.Empty;
    public string Property { get; init; } = string.Empty;
    public string SourceValue { get; init; } = string.Empty;
    public string TargetValue { get; init; } = string.Empty;
    public string ChangeType { get; init; } = string.Empty;
}

public sealed class SchemaDifferenceRowViewModel
{
    public int Index { get; init; }
    public string ObjectType { get; init; } = string.Empty;
    public string Schema { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string ChangeType { get; init; } = string.Empty;
    public int DetailCount { get; init; }
    public IReadOnlyList<SchemaDifferenceDetailViewModel> Details { get; init; } = [];
}

public sealed class SchemaCompareResultViewModel
{
    public string ResultId { get; init; } = string.Empty;
    public string SourceLabel { get; init; } = string.Empty;
    public string TargetLabel { get; init; } = string.Empty;
    public DateTime GeneratedAt { get; init; }
    public SchemaCompareStatsViewModel Stats { get; init; } = new();
    public IReadOnlyList<SchemaDifferenceRowViewModel> Rows { get; init; } = [];
    public string Script { get; init; } = string.Empty;
}

/// <summary>What the result store keeps after a compare. Deliberately holds no credentials.</summary>
public sealed class SchemaCompareStoredResult
{
    public required string ResultId { get; init; }
    public required string SessionId { get; init; }
    public required string Script { get; init; }
    public required string SourceLabel { get; init; }
    public required string TargetLabel { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime ExpiresAt { get; init; }
}
