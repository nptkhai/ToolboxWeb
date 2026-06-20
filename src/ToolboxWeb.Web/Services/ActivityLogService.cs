using ToolboxWeb.Web.Data;
using ToolboxWeb.Web.Domain;
using ToolboxWeb.Web.Enums;

namespace ToolboxWeb.Web.Services;

public interface IActivityLogService
{
    Task LogAsync(string userId, ActivityActionType actionType, ActivityEntityType entityType, string? entityId, string summary);
}

public class ActivityLogService : IActivityLogService
{
    private readonly ApplicationDbContext _db;

    public ActivityLogService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task LogAsync(string userId, ActivityActionType actionType, ActivityEntityType entityType, string? entityId, string summary)
    {
        _db.ActivityLogs.Add(new ActivityLog
        {
            UserId = userId,
            ActionType = actionType,
            EntityType = entityType,
            EntityId = entityId,
            Summary = summary,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
    }
}
