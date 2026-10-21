using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using ToolboxWeb.Web;
using ToolboxWeb.Web.Data;
using ToolboxWeb.Web.Domain;
using ToolboxWeb.Web.Enums;
using ToolboxWeb.Web.Infrastructure.Time;
using ToolboxWeb.Web.ViewModels;

namespace ToolboxWeb.Web.Services;

public interface IChecklistService
{
    Task<IReadOnlyList<ChecklistItem>> GetAsync(string userId, int take = 100);
    Task<ChecklistListResult> GetListAsync(string userId, string? filter, string? search, int take = 200);
    Task<int> CreateAsync(string userId, ChecklistItemFormViewModel model);
    Task<bool> ToggleAsync(string userId, int id);
    Task<bool> DeleteAsync(string userId, int id);
}

public class ChecklistService : IChecklistService
{
    private readonly ApplicationDbContext _db;
    private readonly IActivityLogService _activityLog;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public ChecklistService(ApplicationDbContext db, IActivityLogService activityLog, IStringLocalizer<SharedResource> localizer)
    {
        _db = db;
        _activityLog = activityLog;
        _localizer = localizer;
    }

    public async Task<IReadOnlyList<ChecklistItem>> GetAsync(string userId, int take = 100)
    {
        return await _db.ChecklistItems
            .Where(x => x.UserId == userId && x.Status != ChecklistStatus.Archived)
            .OrderBy(x => x.Status)
            .ThenBy(x => x.DueDate ?? DateTime.MaxValue)
            .ThenByDescending(x => x.CreatedAt)
            .Take(take)
            .ToListAsync();
    }

    /// <summary>
    /// The list page: items for one filter chip (all, todo, overdue, done) and search text, plus
    /// the count behind every chip for the same search. Overdue means not done and due before today
    /// in the app time zone.
    /// </summary>
    public async Task<ChecklistListResult> GetListAsync(string userId, string? filter, string? search, int take = 200)
    {
        var today = AppClock.Today;
        var activeFilter = ChecklistFilters.Normalize(filter);
        var term = search?.Trim();

        var query = _db.ChecklistItems.Where(x => x.UserId == userId && x.Status != ChecklistStatus.Archived);
        var hasAny = await query.AnyAsync();

        if (!string.IsNullOrEmpty(term))
        {
            var pattern = $"%{term}%";
            query = query.Where(x => EF.Functions.Like(x.Title, pattern) || (x.Description != null && EF.Functions.Like(x.Description, pattern)));
        }

        var counts = new ChecklistCounts
        {
            All = await query.CountAsync(),
            Todo = await query.CountAsync(x => x.Status == ChecklistStatus.Todo),
            Overdue = await query.CountAsync(x => x.Status == ChecklistStatus.Todo && x.DueDate != null && x.DueDate < today),
            Done = await query.CountAsync(x => x.Status == ChecklistStatus.Done)
        };

        query = activeFilter switch
        {
            ChecklistFilters.Todo => query.Where(x => x.Status == ChecklistStatus.Todo),
            ChecklistFilters.Overdue => query.Where(x => x.Status == ChecklistStatus.Todo && x.DueDate != null && x.DueDate < today),
            ChecklistFilters.Done => query.Where(x => x.Status == ChecklistStatus.Done),
            _ => query
        };

        var items = await query
            .OrderBy(x => x.Status)
            .ThenBy(x => x.DueDate ?? DateTime.MaxValue)
            .ThenByDescending(x => x.CreatedAt)
            .Take(take)
            .ToListAsync();

        return new ChecklistListResult(items, counts, hasAny, activeFilter, term);
    }

    public async Task<int> CreateAsync(string userId, ChecklistItemFormViewModel model)
    {
        var item = new ChecklistItem
        {
            UserId = userId,
            Title = model.Title.Trim(),
            Description = model.Description?.Trim(),
            DueDate = model.DueDate,
            CreatedAt = DateTime.UtcNow
        };

        _db.ChecklistItems.Add(item);
        await _db.SaveChangesAsync();
        await _activityLog.LogAsync(userId, ActivityActionType.Create, ActivityEntityType.ChecklistItem, item.Id.ToString(), _localizer["ActivityLog.TaskCreatedSummary", item.Title].Value);
        return item.Id;
    }

    public async Task<bool> ToggleAsync(string userId, int id)
    {
        var item = await _db.ChecklistItems.FirstOrDefaultAsync(x => x.UserId == userId && x.Id == id);
        if (item is null)
        {
            return false;
        }

        item.Status = item.Status == ChecklistStatus.Done ? ChecklistStatus.Todo : ChecklistStatus.Done;
        item.CompletedAt = item.Status == ChecklistStatus.Done ? DateTime.UtcNow : null;

        await _db.SaveChangesAsync();
        await _activityLog.LogAsync(userId, ActivityActionType.Complete, ActivityEntityType.ChecklistItem, item.Id.ToString(), _localizer["ActivityLog.TaskToggledSummary", item.Title].Value);
        return true;
    }

    public async Task<bool> DeleteAsync(string userId, int id)
    {
        var item = await _db.ChecklistItems.FirstOrDefaultAsync(x => x.UserId == userId && x.Id == id);
        if (item is null)
        {
            return false;
        }

        _db.ChecklistItems.Remove(item);
        await _db.SaveChangesAsync();
        await _activityLog.LogAsync(userId, ActivityActionType.Delete, ActivityEntityType.ChecklistItem, item.Id.ToString(), _localizer["ActivityLog.TaskDeletedSummary", item.Title].Value);
        return true;
    }
}
