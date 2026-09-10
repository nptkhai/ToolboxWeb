namespace ToolboxWeb.Web.ViewModels.SchemaCompare;

/// <summary>Object families the user can include or exclude before comparing.</summary>
public sealed class CompareScopeOptions
{
    public bool Tables { get; set; } = true;
    public bool Views { get; set; } = true;
    public bool StoredProcedures { get; set; } = true;
    public bool Functions { get; set; } = true;
    public bool Triggers { get; set; } = true;
    public bool TableTypes { get; set; } = true;
    public bool Sequences { get; set; } = true;
    public bool Synonyms { get; set; } = true;

    /// <summary>Users, roles and permissions. Off by default: they normally differ on purpose between environments.</summary>
    public bool SecurityObjects { get; set; }

    /// <summary>MS_Description and friends.</summary>
    public bool ExtendedProperties { get; set; }

    public bool FullText { get; set; } = true;
}

public sealed class DacFxCompareRequest
{
    public SqlConnectionInputModel Source { get; set; } = new();
    public SqlConnectionInputModel Target { get; set; } = new();
    public CompareScopeOptions Scope { get; set; } = new();
    public DacFxCompareOptions Options { get; set; } = new();

    /// <summary>
    /// Database-name substitutions applied to source definitions, e.g.
    /// <c>EDU_ORG_DATA → EDU_FBU_DATA</c>. Lets a definition copied from the origin database
    /// match and deploy against a school-coded target.
    /// </summary>
    public List<NameMappingPair> NameMappings { get; set; } = [];

    /// <summary>
    /// School code in the origin's database names, e.g. <c>ORG</c>. Together with
    /// <see cref="TargetCode"/> this rewrites every database at once — EDU_ORG,
    /// EDU_ORG_DATA, WEB_ORG_EOFFICE — without listing them individually.
    /// </summary>
    public string SourceCode { get; set; } = string.Empty;

    /// <summary>School code in the target's database names, e.g. <c>FBU</c>.</summary>
    public string TargetCode { get; set; } = string.Empty;
}

/// <summary>
/// One database-name substitution. <see cref="To"/> may list several alternatives separated
/// by <c>|</c> when one origin database serves more than one target, for example
/// <c>WEB_FBU_EOFFICE|WEB_FBU_SINHVIEN</c>.
/// </summary>
public sealed class CancelCompareRequest
{
    public string ComparisonId { get; set; } = string.Empty;
}

public sealed class NameMappingPair
{
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
}

public sealed class DacFxCompareOptions
{
    public bool IgnoreWhitespace { get; set; } = true;
    public bool IgnoreComments { get; set; }
    public bool IgnoreKeywordCasing { get; set; } = true;
    public bool IgnoreColumnOrder { get; set; } = true;
    public bool IgnoreIndexOptions { get; set; } = true;
    public bool IgnoreFillFactor { get; set; } = true;
    public bool IgnoreFilegroupPlacement { get; set; } = true;
    public bool IgnoreIdentitySeed { get; set; } = true;
    public bool IgnoreExtendedProperties { get; set; } = true;
}

/// <summary>One row of the compare grid, laid out source / status / target like SQL Delta.</summary>
public sealed class DiffRowViewModel
{
    public string Id { get; init; } = string.Empty;
    public string ObjectType { get; init; } = string.Empty;
    public string Schema { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string SourceName { get; init; } = string.Empty;
    public string TargetName { get; init; } = string.Empty;

    /// <summary>Add, Change or Delete, as reported by DacFx.</summary>
    public string Action { get; init; } = string.Empty;

    public bool Included { get; init; }

    /// <summary>False when DacFx refuses to let this node be excluded; the checkbox is then disabled.</summary>
    public bool CanToggle { get; init; }

    public int ChildCount { get; init; }

    /// <summary>
    /// True when this object or one of its child differences removes a target object, column,
    /// constraint, or other schema member. ToolboxWeb leaves these rows unchecked in its own
    /// lightweight selection; DacFx's expensive dependency graph is not mutated during Compare.
    /// </summary>
    public bool Destructive { get; init; }

    /// <summary>
    /// True when the only difference vanishes after database-name mapping and cosmetic
    /// normalization, i.e. the two objects are effectively the same. Hidden from the grid by
    /// default and left out of the sync script.
    /// </summary>
    public bool MappedSame { get; init; }
}

public sealed class ObjectTypeCountViewModel
{
    public string ObjectType { get; init; } = string.Empty;
    public int Total { get; init; }
    public int OnlyInSource { get; init; }
    public int Different { get; init; }
    public int OnlyInTarget { get; init; }
}

public sealed class DacFxCompareStatsViewModel
{
    public int Total { get; init; }
    public int OnlyInSource { get; init; }
    public int Different { get; init; }
    public int OnlyInTarget { get; init; }

    /// <summary>Objects hidden because they became equal after mapping/normalization.</summary>
    public int MappedSame { get; init; }

    public IReadOnlyList<ObjectTypeCountViewModel> ByType { get; init; } = [];
}

public sealed class DacFxCompareResultViewModel
{
    public string ComparisonId { get; init; } = string.Empty;
    public string SourceLabel { get; init; } = string.Empty;
    public string TargetLabel { get; init; } = string.Empty;
    public DateTime GeneratedAt { get; init; }
    public long ElapsedMs { get; init; }
    public bool IsEqual { get; init; }
    public DacFxCompareStatsViewModel Stats { get; init; } = new();
    public IReadOnlyList<DiffRowViewModel> Rows { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

public enum DiffLineState
{
    Same,
    Changed,
    OnlyInSource,
    OnlyInTarget
}

public sealed class DiffLineViewModel
{
    public int? SourceLine { get; init; }
    public string SourceText { get; init; } = string.Empty;
    public int? TargetLine { get; init; }
    public string TargetText { get; init; } = string.Empty;
    public string State { get; init; } = nameof(DiffLineState.Same);
}

public sealed class DiffDetailViewModel
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public int ChangedLines { get; init; }
    public IReadOnlyList<DiffLineViewModel> Lines { get; init; } = [];
}

public sealed class ProgressLineViewModel
{
    public string Time { get; init; } = string.Empty;
    public string Operation { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
}

public sealed class CompareProgressViewModel
{
    public string ComparisonId { get; init; } = string.Empty;
    public bool Running { get; init; }
    public bool Completed { get; init; }
    public string? Error { get; init; }
    public long ElapsedMs { get; init; }
    public IReadOnlyList<ProgressLineViewModel> Source { get; init; } = [];
    public IReadOnlyList<ProgressLineViewModel> Target { get; init; } = [];
    public DacFxCompareResultViewModel? Result { get; init; }
}

public sealed class ToggleIncludeRequest
{
    public string ComparisonId { get; set; } = string.Empty;
    public string[] Ids { get; set; } = [];
    public bool Include { get; set; }
}

public sealed class ToggleIncludeResultViewModel
{
    public IReadOnlyList<DiffRowViewModel> Rows { get; init; } = [];
    public IReadOnlyList<string> Blocked { get; init; } = [];
    public DacFxCompareStatsViewModel Stats { get; init; } = new();
}

public sealed class ScriptViewModel
{
    public string Script { get; init; } = string.Empty;
    public int IncludedCount { get; init; }
    public int ExcludedCount { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
