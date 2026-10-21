namespace ToolboxWeb.Web.ViewModels;

/// <summary>
/// Calm empty state for a list or data area: icon + one sentence + an optional primary action.
/// Rendered by Views/Shared/_EmptyState.cshtml. Pass already-localized text.
/// </summary>
public sealed record EmptyStateViewModel(string Message, string Icon = "inbox")
{
    public string? ActionText { get; init; }

    public string? ActionHref { get; init; }

    /// <summary>Selector of a Bootstrap modal the action opens instead of following <see cref="ActionHref"/>.</summary>
    public string? ActionTarget { get; init; }
}
