using ToolboxWeb.Web.Domain;
using ToolboxWeb.Web.Enums;
using ToolboxWeb.Web.ViewModels;

namespace ToolboxWeb.Web.Services;

/// <summary>
/// The activity log of the web copy (App:Mode=Web), which stores nothing: entries are dropped,
/// so shared services that log (writing due dates to Jira) run unchanged.
/// </summary>
public sealed class NoOpActivityLogService : IActivityLogService
{
    public Task LogAsync(string userId, ActivityActionType actionType, ActivityEntityType entityType, string? entityId, string summary) => Task.CompletedTask;

    public Task<IReadOnlyList<ActivityLog>> GetRecentAsync(string userId, int take) => Task.FromResult<IReadOnlyList<ActivityLog>>([]);

    public Task<ActivityPageViewModel> GetPageAsync(string userId, ActivityEntityType? entityType, int page) => Task.FromResult(new ActivityPageViewModel());
}
