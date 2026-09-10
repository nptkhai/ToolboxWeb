namespace ToolboxWeb.Web.ViewModels.SchemaCompare;

public sealed class SchemaComparePageViewModel
{
    public SchemaCompareOptions Options { get; init; } = new();
    public bool HostAllowlistEnabled { get; init; }
}

public sealed class SqlServerProbeViewModel
{
    public string ServerVersion { get; init; } = string.Empty;
    public IReadOnlyList<string> Databases { get; init; } = [];
}
