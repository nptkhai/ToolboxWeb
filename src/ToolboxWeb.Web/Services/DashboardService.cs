using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using ToolboxWeb.Web;
using ToolboxWeb.Web.Data;
using ToolboxWeb.Web.Enums;
using ToolboxWeb.Web.Extensions;
using ToolboxWeb.Web.ViewModels;

namespace ToolboxWeb.Web.Services;

public interface IDashboardService
{
    Task<DashboardViewModel> BuildAsync(string userId);
}

public class DashboardService : IDashboardService
{
    private readonly ApplicationDbContext _db;
    private readonly INoteService _notes;
    private readonly IChecklistService _checklist;
    private readonly IQuickLinkService _quickLinks;
    private readonly IPromptTemplateService _prompts;
    private readonly IFocusSessionService _focusSessions;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public DashboardService(
        ApplicationDbContext db,
        INoteService notes,
        IChecklistService checklist,
        IQuickLinkService quickLinks,
        IPromptTemplateService prompts,
        IFocusSessionService focusSessions,
        IStringLocalizer<SharedResource> localizer)
    {
        _db = db;
        _notes = notes;
        _checklist = checklist;
        _quickLinks = quickLinks;
        _prompts = prompts;
        _focusSessions = focusSessions;
        _localizer = localizer;
    }

    public async Task<DashboardViewModel> BuildAsync(string userId)
    {
        await SeedData.EnsureUserDefaultsAsync(_db, userId, _localizer);

        var activity = await _db.ActivityLogs
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(12)
            .ToListAsync();

        var today = DateTimeExtensions.VietnamToday();
        var checklist = (await _checklist.GetAsync(userId, 20))
            .Where(x => x.Status == ChecklistStatus.Todo || x.DueDate?.Date == today)
            .Take(8)
            .ToList();

        return new DashboardViewModel
        {
            RecentNotes = await _notes.GetAsync(userId, take: 6),
            TodayChecklist = checklist,
            QuickLinks = await _quickLinks.GetAsync(userId, 8),
            PromptTemplates = await _prompts.GetAsync(userId, 6),
            RecentFocusSessions = await _focusSessions.GetRecentAsync(userId, 5),
            RecentActivity = activity
        };
    }
}
