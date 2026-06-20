namespace ToolboxWeb.Web.Shared;

public class OperationResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }

    public static OperationResult Ok(string? message = null) => new() { Success = true, Message = message };
    public static OperationResult Fail(string message) => new() { Success = false, Message = message };
}
