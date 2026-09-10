namespace ToolboxWeb.Web.ViewModels.SchemaCompare;

public sealed class SchemaCompareRequest
{
    public SqlConnectionInputModel Source { get; set; } = new();
    public SqlConnectionInputModel Target { get; set; } = new();
    public SchemaCompareOptions Options { get; set; } = new();
}
