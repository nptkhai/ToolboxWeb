namespace ToolboxWeb.Web.ViewModels;

public class FileAttachmentItemViewModel
{
    public string? Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public long Size { get; init; }
    public string? Type { get; init; }
    public string? Url { get; init; }
}
