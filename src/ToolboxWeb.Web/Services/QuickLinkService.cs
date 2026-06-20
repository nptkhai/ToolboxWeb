using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using ToolboxWeb.Web;
using ToolboxWeb.Web.Data;
using ToolboxWeb.Web.Domain;
using ToolboxWeb.Web.Enums;
using ToolboxWeb.Web.ViewModels;

namespace ToolboxWeb.Web.Services;

public interface IQuickLinkService
{
    Task<IReadOnlyList<QuickLink>> GetAsync(string userId, int take = 100);
    Task<int> CreateAsync(string userId, QuickLinkFormViewModel model);
    Task<bool> DeleteAsync(string userId, int id);
}

public class QuickLinkService : IQuickLinkService
{
    private readonly ApplicationDbContext _db;
    private readonly IActivityLogService _activityLog;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public QuickLinkService(ApplicationDbContext db, IActivityLogService activityLog, IStringLocalizer<SharedResource> localizer)
    {
        _db = db;
        _activityLog = activityLog;
        _localizer = localizer;
    }

    public async Task<IReadOnlyList<QuickLink>> GetAsync(string userId, int take = 100)
    {
        return await _db.QuickLinks
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.GroupName)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Title)
            .Take(take)
            .ToListAsync();
    }

    public async Task<int> CreateAsync(string userId, QuickLinkFormViewModel model)
    {
        var link = new QuickLink
        {
            UserId = userId,
            Title = model.Title.Trim(),
            Url = model.Url.Trim(),
            GroupName = model.GroupName?.Trim(),
            SortOrder = model.SortOrder,
            CreatedAt = DateTime.UtcNow
        };

        _db.QuickLinks.Add(link);
        await _db.SaveChangesAsync();
        await _activityLog.LogAsync(userId, ActivityActionType.Create, ActivityEntityType.QuickLink, link.Id.ToString(), _localizer["ActivityLog.QuickLinkCreatedSummary", link.Title].Value);
        return link.Id;
    }

    public async Task<bool> DeleteAsync(string userId, int id)
    {
        var link = await _db.QuickLinks.FirstOrDefaultAsync(x => x.UserId == userId && x.Id == id);
        if (link is null)
        {
            return false;
        }

        _db.QuickLinks.Remove(link);
        await _db.SaveChangesAsync();
        await _activityLog.LogAsync(userId, ActivityActionType.Delete, ActivityEntityType.QuickLink, link.Id.ToString(), _localizer["ActivityLog.QuickLinkDeletedSummary", link.Title].Value);
        return true;
    }
}
