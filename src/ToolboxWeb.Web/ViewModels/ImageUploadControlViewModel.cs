namespace ToolboxWeb.Web.ViewModels;

public class ImageUploadControlViewModel
{
    public string InputName { get; init; } = string.Empty;
    public string RemovedInputName { get; init; } = string.Empty;
    public string TriggerText { get; init; } = string.Empty;
    public string RemoveText { get; init; } = string.Empty;
    public string EmptyTitle { get; init; } = string.Empty;
    public string EmptyNote { get; init; } = string.Empty;
    public string? HelperText { get; init; }
    public string Accept { get; init; } = ".jpg,.jpeg,.png,.gif,.webp";
    public string? InvalidTypeMessage { get; init; }
    public IReadOnlyList<string> AllowedExtensions { get; init; } = [".jpg", ".jpeg", ".png", ".gif", ".webp"];
    public string? AriaLabel { get; init; }
    public string? RootCssClass { get; init; }
    public string? CurrentItemId { get; init; }
    public string? CurrentImageUrl { get; init; }
    public string? CurrentFileName { get; init; }
    public long? CurrentFileSize { get; init; }
}
