using Microsoft.EntityFrameworkCore;
using ToolboxWeb.Web.Data;
using ToolboxWeb.Web.Domain;
using ToolboxWeb.Web.Enums;
using ToolboxWeb.Web.ViewModels;

namespace ToolboxWeb.Web.Services;

public interface IActivityLogService
{
    Task LogAsync(string userId, ActivityActionType actionType, ActivityEntityType entityType, string? entityId, string summary);

    Task<IReadOnlyList<ActivityLog>> GetRecentAsync(string userId, int take);

    /// <summary>One page of the user's activity, newest first, optionally for one kind of item.</summary>
    Task<ActivityPageViewModel> GetPageAsync(string userId, ActivityEntityType? entityType, int page);
}

public class ActivityLogService : IActivityLogService
{
    public const int PageSize = 30;

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

    public async Task<IReadOnlyList<ActivityLog>> GetRecentAsync(string userId, int take)
    {
        return await _db.ActivityLogs.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(take)
            .ToListAsync();
    }

    public async Task<ActivityPageViewModel> GetPageAsync(string userId, ActivityEntityType? entityType, int page)
    {
        var mine = _db.ActivityLogs.AsNoTracking().Where(x => x.UserId == userId);

        // The filter only offers the kinds this user actually has.
        var entityTypes = await mine.Select(x => x.EntityType).Distinct().ToListAsync();
        var filtered = entityType is null ? mine : mine.Where(x => x.EntityType == entityType);

        var total = await filtered.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        var current = Math.Clamp(page, 1, totalPages);
        var items = await filtered
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Skip((current - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        return new ActivityPageViewModel
        {
            Items = items,
            EntityType = entityType,
            EntityTypes = entityTypes.OrderBy(x => x).ToList(),
            Page = current,
            TotalPages = totalPages,
            Total = total
        };
    }
}
