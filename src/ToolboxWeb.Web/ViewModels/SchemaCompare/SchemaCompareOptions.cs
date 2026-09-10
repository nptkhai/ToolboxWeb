namespace ToolboxWeb.Web.ViewModels.SchemaCompare;

public sealed class SchemaCompareOptions
{
    // Object groups to read and compare.
    public bool IncludeTables { get; set; } = true;
    public bool IncludeKeysAndIndexes { get; set; } = true;
    public bool IncludeProgrammability { get; set; } = true;

    // Noise reduction. Without whitespace normalization nearly every routine reports as
    // different, so it defaults on.
    public bool IgnoreWhitespaceInDefinitions { get; set; } = true;
    public bool IgnoreCaseInDefinitions { get; set; }

    /// <summary>
    /// Drops comments before comparing routine bodies. Off by default: a stale comment in
    /// the target is a real difference, even though it changes no behaviour.
    /// </summary>
    public bool IgnoreCommentsInDefinitions { get; set; }
    public bool IgnoreCollation { get; set; }
    public bool IgnoreIdentitySeed { get; set; } = true;

    /// <summary>
    /// Compare constraint and index contents only, ignoring auto-generated names such as
    /// DF__Table__Col__1A2B3C4D.
    /// </summary>
    public bool IgnoreConstraintNames { get; set; }

    /// <summary>
    /// When false the generated script keeps every destructive statement commented out.
    /// </summary>
    public bool IncludeDropStatements { get; set; }
}
