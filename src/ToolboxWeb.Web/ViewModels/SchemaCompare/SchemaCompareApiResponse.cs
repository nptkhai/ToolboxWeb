namespace ToolboxWeb.Web.ViewModels.SchemaCompare;

public sealed class SchemaCompareApiResponse<T>
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public T? Data { get; init; }

    public static SchemaCompareApiResponse<T> Ok(T data, string message = "") => new()
    {
        Success = true,
        Data = data,
        Message = message
    };

    public static SchemaCompareApiResponse<T> Fail(string message) => new()
    {
        Success = false,
        Message = message
    };
}
