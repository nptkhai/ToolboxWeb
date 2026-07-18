namespace ToolboxWeb.Web.ViewModels;

public class FileAttachmentControlViewModel
{
    public string InputName { get; init; } = string.Empty;
    public string RemovedInputName { get; init; } = string.Empty;
    public string TriggerText { get; init; } = string.Empty;
    public string HeaderText { get; init; } = string.Empty;
    public string ClearAllText { get; init; } = string.Empty;
    public string RemoveText { get; init; } = string.Empty;
    public string EmptyText { get; init; } = string.Empty;
    public string? HelperText { get; init; }
    public string Accept { get; init; } = ".pdf,.doc,.docx,.xls,.xlsx,.ppt,.pptx,.txt,.csv,.jpg,.jpeg,.png,.gif,.webp";
    public string? InvalidTypeMessage { get; init; }
    public IReadOnlyList<string> AllowedExtensions { get; init; } = [".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".csv", ".jpg", ".jpeg", ".png", ".gif", ".webp"];
    public string? AriaLabel { get; init; }
    public bool AllowMultiple { get; init; } = true;
    public string? RootCssClass { get; init; }
    public IReadOnlyList<FileAttachmentItemViewModel> InitialItems { get; init; } = [];
}
