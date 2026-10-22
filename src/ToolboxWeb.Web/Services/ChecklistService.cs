using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using ToolboxWeb.Web;
using ToolboxWeb.Web.Data;
using ToolboxWeb.Web.Domain;
using ToolboxWeb.Web.Enums;
using ToolboxWeb.Web.Extensions;
using ToolboxWeb.Web.Infrastructure.Time;
using ToolboxWeb.Web.ViewModels;

namespace ToolboxWeb.Web.Services;

public interface IChecklistService
{
    Task<IReadOnlyList<ChecklistItem>> GetAsync(string userId, int take = 100);
    Task<ChecklistListResult> GetListAsync(string userId, ChecklistListQuery query, int take = 300);
    Task<int> CreateAsync(string userId, ChecklistItemFormViewModel model);
    Task<bool> UpdateAsync(string userId, ChecklistItemFormViewModel model);
    Task<bool> ToggleAsync(string userId, int id);

    /// <summary>Marks open items done; returns the ids that changed (for "Undo").</summary>
    Task<IReadOnlyList<int>> CompleteAsync(string userId, IEnumerable<int> ids);

    /// <summary>Sets the due date from a shortcut (today, tomorrow, next week, none).</summary>
    Task<bool> SetDueAsync(string userId, int id, string shortcut);

    /// <summary>Deletes items, keeping them a day so "Undo" can bring them back; returns the ids deleted.</summary>
    Task<IReadOnlyList<int>> DeleteAsync(string userId, IEnumerable<int> ids);

    /// <summary>Deletes every done item (same "Undo" window as <see cref="DeleteAsync"/>).</summary>
    Task<IReadOnlyList<int>> ClearDoneAsync(string userId);

    /// <summary>Reverses a complete or a delete for these ids; returns how many came back.</summary>
    Task<int> UndoAsync(string userId, string kind, IEnumerable<int> ids);
}

public class ChecklistService : IChecklistService
{
    // How long a deleted item can still be brought back with "Undo".
    private static readonly TimeSpan UndoWindow = TimeSpan.FromDays(1);
    private const int MaxBatch = 200;

    private readonly ApplicationDbContext _db;
    private readonly IActivityLogService _activityLog;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public ChecklistService(ApplicationDbContext db, IActivityLogService activityLog, IStringLocalizer<SharedResource> localizer)
    {
        _db = db;
        _activityLog = activityLog;
        _localizer = localizer;
    }

    private IQueryable<ChecklistItem> Live(string userId) =>
        _db.ChecklistItems.Where(x => x.UserId == userId && x.DeletedAt == null && x.Status != ChecklistStatus.Archived);

    public async Task<IReadOnlyList<ChecklistItem>> GetAsync(string userId, int take = 100)
    {
        return await Live(userId)
            .OrderBy(x => x.Status)
            .ThenBy(x => x.DueDate ?? DateTime.MaxValue)
            .ThenByDescending(x => x.CreatedAt)
            .Take(take)
            .ToListAsync();
    }

    /// <summary>
    /// The list page: items for one filter chip and search text, grouped by when they are due,
    /// sorted inside each group; the count behind every chip for the same search; and this
    /// week's progress. Overdue means not done and due before today in the app time zone.
    /// </summary>
    public async Task<ChecklistListResult> GetListAsync(string userId, ChecklistListQuery query, int take = 300)
    {
        await PurgeDeletedAsync(userId);

        var today = AppClock.Today;
        var tomorrow = today.AddDays(1);
        var filter = ChecklistFilters.Normalize(query.Filter);
        var sort = ChecklistSorts.Normalize(query.Sort) ?? ChecklistSorts.Due;
        var term = query.Search?.Trim();

        var items = Live(userId);
        var hasAny = await items.AnyAsync();

        if (!string.IsNullOrEmpty(term))
        {
            var pattern = $"%{term}%";
            items = items.Where(x => EF.Functions.Like(x.Title, pattern) || (x.Description != null && EF.Functions.Like(x.Description, pattern)));
        }

        var counts = new ChecklistCounts
        {
            All = await items.CountAsync(),
            Todo = await items.CountAsync(x => x.Status == ChecklistStatus.Todo),
            Today = await items.CountAsync(x => x.Status == ChecklistStatus.Todo && x.DueDate >= today && x.DueDate < tomorrow),
            Overdue = await items.CountAsync(x => x.Status == ChecklistStatus.Todo && x.DueDate != null && x.DueDate < today),
            Done = await items.CountAsync(x => x.Status == ChecklistStatus.Done)
        };

        items = filter switch
        {
            ChecklistFilters.Todo => items.Where(x => x.Status == ChecklistStatus.Todo),
            ChecklistFilters.Today => items.Where(x => x.Status == ChecklistStatus.Todo && x.DueDate >= today && x.DueDate < tomorrow),
            ChecklistFilters.Overdue => items.Where(x => x.Status == ChecklistStatus.Todo && x.DueDate != null && x.DueDate < today),
            ChecklistFilters.Done => items.Where(x => x.Status == ChecklistStatus.Done),
            _ => items
        };

        var list = await items.OrderByDescending(x => x.CreatedAt).Take(take).ToListAsync();
        var groups = list
            .GroupBy(x => GroupOf(x, today))
            .OrderBy(x => x.Key)
            .Select(group => new ChecklistGroupViewModel { Group = group.Key, Items = Sort(group, group.Key, sort) })
            .ToList();

        return new ChecklistListResult(groups, counts, await WeekProgressAsync(userId, today), hasAny, new ChecklistListQuery(filter, term, sort));
    }

    public async Task<int> CreateAsync(string userId, ChecklistItemFormViewModel model)
    {
        var item = new ChecklistItem
        {
            UserId = userId,
            Title = model.Title.Trim(),
            Description = Clean(model.Description),
            DueDate = model.DueDate?.Date,
            Priority = Enum.IsDefined(model.Priority) ? model.Priority : ChecklistPriority.Normal,
            CreatedAt = DateTime.UtcNow
        };

        _db.ChecklistItems.Add(item);
        await _db.SaveChangesAsync();
        await _activityLog.LogAsync(userId, ActivityActionType.Create, ActivityEntityType.ChecklistItem, item.Id.ToString(), _localizer["ActivityLog.TaskCreatedSummary", item.Title].Value);
        return item.Id;
    }

    public async Task<bool> UpdateAsync(string userId, ChecklistItemFormViewModel model)
    {
        var item = await Live(userId).FirstOrDefaultAsync(x => x.Id == model.Id);
        if (item is null)
        {
            return false;
        }

        item.Title = model.Title.Trim();
        item.Description = Clean(model.Description);
        item.DueDate = model.DueDate?.Date;
        item.Priority = Enum.IsDefined(model.Priority) ? model.Priority : ChecklistPriority.Normal;

        await _db.SaveChangesAsync();
        await _activityLog.LogAsync(userId, ActivityActionType.Update, ActivityEntityType.ChecklistItem, item.Id.ToString(), _localizer["ActivityLog.TaskUpdatedSummary", item.Title].Value);
        return true;
    }

    public async Task<bool> ToggleAsync(string userId, int id)
    {
        var item = await Live(userId).FirstOrDefaultAsync(x => x.Id == id);
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

    public async Task<IReadOnlyList<int>> CompleteAsync(string userId, IEnumerable<int> ids)
    {
        var wanted = ids.Distinct().Take(MaxBatch).ToList();
        var items = await Live(userId).Where(x => wanted.Contains(x.Id) && x.Status == ChecklistStatus.Todo).ToListAsync();
        var now = DateTime.UtcNow;
        foreach (var item in items)
        {
            item.Status = ChecklistStatus.Done;
            item.CompletedAt = now;
        }

        await _db.SaveChangesAsync();
        foreach (var item in items)
        {
            await _activityLog.LogAsync(userId, ActivityActionType.Complete, ActivityEntityType.ChecklistItem, item.Id.ToString(), _localizer["ActivityLog.TaskCompletedSummary", item.Title].Value);
        }

        return items.Select(x => x.Id).ToList();
    }

    public async Task<bool> SetDueAsync(string userId, int id, string shortcut)
    {
        var item = await Live(userId).FirstOrDefaultAsync(x => x.Id == id);
        if (item is null)
        {
            return false;
        }

        var today = AppClock.Today;
        // "Next week" is the coming Monday, never today.
        var toMonday = (8 - (int)today.DayOfWeek) % 7;
        DateTime? due = shortcut switch
        {
            ChecklistDueShortcuts.Today => today,
            ChecklistDueShortcuts.Tomorrow => today.AddDays(1),
            ChecklistDueShortcuts.NextWeek => today.AddDays(toMonday == 0 ? 7 : toMonday),
            ChecklistDueShortcuts.None => null,
            _ => item.DueDate
        };

        if (due == item.DueDate)
        {
            return true;
        }

        item.DueDate = due;
        await _db.SaveChangesAsync();
        await _activityLog.LogAsync(userId, ActivityActionType.Update, ActivityEntityType.ChecklistItem, item.Id.ToString(),
            _localizer["ActivityLog.TaskDueChangedSummary", item.Title, due.ToDisplayDate()].Value);
        return true;
    }

    public async Task<IReadOnlyList<int>> DeleteAsync(string userId, IEnumerable<int> ids)
    {
        var wanted = ids.Distinct().Take(MaxBatch).ToList();
        var items = await Live(userId).Where(x => wanted.Contains(x.Id)).ToListAsync();
        return await SoftDeleteAsync(userId, items);
    }

    public async Task<IReadOnlyList<int>> ClearDoneAsync(string userId)
    {
        var items = await Live(userId).Where(x => x.Status == ChecklistStatus.Done).Take(MaxBatch).ToListAsync();
        return await SoftDeleteAsync(userId, items);
    }

    public async Task<int> UndoAsync(string userId, string kind, IEnumerable<int> ids)
    {
        var wanted = ids.Distinct().Take(MaxBatch).ToList();
        var mine = _db.ChecklistItems.Where(x => x.UserId == userId && wanted.Contains(x.Id));

        List<ChecklistItem> items;
        if (kind == ChecklistUndoKinds.Delete)
        {
            items = await mine.Where(x => x.DeletedAt != null).ToListAsync();
            items.ForEach(x => x.DeletedAt = null);
        }
        else if (kind == ChecklistUndoKinds.Complete)
        {
            items = await mine.Where(x => x.DeletedAt == null && x.Status == ChecklistStatus.Done).ToListAsync();
            items.ForEach(x =>
            {
                x.Status = ChecklistStatus.Todo;
                x.CompletedAt = null;
            });
        }
        else
        {
            return 0;
        }

        await _db.SaveChangesAsync();
        foreach (var item in items)
        {
            await _activityLog.LogAsync(userId, ActivityActionType.Update, ActivityEntityType.ChecklistItem, item.Id.ToString(), _localizer["ActivityLog.TaskRestoredSummary", item.Title].Value);
        }

        return items.Count;
    }

    private async Task<IReadOnlyList<int>> SoftDeleteAsync(string userId, List<ChecklistItem> items)
    {
        var now = DateTime.UtcNow;
        items.ForEach(x => x.DeletedAt = now);
        await _db.SaveChangesAsync();
        foreach (var item in items)
        {
            await _activityLog.LogAsync(userId, ActivityActionType.Delete, ActivityEntityType.ChecklistItem, item.Id.ToString(), _localizer["ActivityLog.TaskDeletedSummary", item.Title].Value);
        }

        return items.Select(x => x.Id).ToList();
    }

    // Deleted items past the undo window go for good.
    private Task<int> PurgeDeletedAsync(string userId)
    {
        var cutoff = DateTime.UtcNow - UndoWindow;
        return _db.ChecklistItems.Where(x => x.UserId == userId && x.DeletedAt != null && x.DeletedAt < cutoff).ExecuteDeleteAsync();
    }

    private async Task<ChecklistWeekProgress> WeekProgressAsync(string userId, DateTime today)
    {
        var weekStart = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var nextWeek = weekStart.AddDays(7);
        var weekStartUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(weekStart, DateTimeKind.Unspecified), AppClock.TimeZone);

        var done = await Live(userId).CountAsync(x => x.Status == ChecklistStatus.Done && x.CompletedAt >= weekStartUtc);
        var open = await Live(userId).CountAsync(x => x.Status == ChecklistStatus.Todo && x.DueDate != null && x.DueDate < nextWeek);
        return new ChecklistWeekProgress { Done = done, Planned = done + open };
    }

    private static ChecklistGroup GroupOf(ChecklistItem item, DateTime today)
    {
        if (item.Status == ChecklistStatus.Done)
        {
            return ChecklistGroup.Done;
        }

        if (item.DueDate is null)
        {
            return ChecklistGroup.NoDueDate;
        }

        var due = item.DueDate.Value.Date;
        return due < today ? ChecklistGroup.Overdue : due == today ? ChecklistGroup.Today : ChecklistGroup.Upcoming;
    }

    private static IReadOnlyList<ChecklistItem> Sort(IEnumerable<ChecklistItem> items, ChecklistGroup group, string sort)
    {
        if (group == ChecklistGroup.Done)
        {
            return items.OrderByDescending(x => x.CompletedAt ?? x.CreatedAt).ToList();
        }

        return sort switch
        {
            ChecklistSorts.Created => items.OrderByDescending(x => x.CreatedAt).ToList(),
            ChecklistSorts.Priority => items.OrderByDescending(x => x.Priority).ThenBy(x => x.DueDate ?? DateTime.MaxValue).ThenByDescending(x => x.CreatedAt).ToList(),
            _ => items.OrderBy(x => x.DueDate ?? DateTime.MaxValue).ThenByDescending(x => x.Priority).ThenByDescending(x => x.CreatedAt).ToList()
        };
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
