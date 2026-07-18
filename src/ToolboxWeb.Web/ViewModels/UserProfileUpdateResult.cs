namespace ToolboxWeb.Web.ViewModels;

public class UserProfileUpdateResult
{
    public bool Succeeded { get; init; }
    public bool Changed { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];

    public static UserProfileUpdateResult Success(bool changed)
    {
        return new UserProfileUpdateResult
        {
            Succeeded = true,
            Changed = changed
        };
    }

    public static UserProfileUpdateResult Failure(params string[] errors)
    {
        return new UserProfileUpdateResult
        {
            Succeeded = false,
            Errors = errors
        };
    }
}
