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
        var openTasks = (await _checklist.GetAsync(userId))
            .Where(x => x.Status == ChecklistStatus.Todo)
            .OrderBy(x => DueRank(x.DueDate, today))
            .ThenBy(x => x.DueDate ?? DateTime.MaxValue)
            .ThenByDescending(x => x.CreatedAt)
            .Take(8)
            .ToList();

        return new DashboardViewModel
        {
            RecentNotes = await _notes.GetAsync(userId, take: 6),
            OpenTasks = openTasks,
            Today = today,
            QuickLinks = await _quickLinks.GetAsync(userId, 8),
            PromptTemplates = await _prompts.GetAsync(userId, 6),
            RecentFocusSessions = await _focusSessions.GetRecentAsync(userId, 5),
            RecentActivity = activity
        };
    }

    // 0 overdue, 1 due today, 2 upcoming, 3 no due date.
    private static int DueRank(DateTime? dueDate, DateTime today)
    {
        if (dueDate is null)
        {
            return 3;
        }

        var due = dueDate.Value.Date;
        return due < today ? 0 : due == today ? 1 : 2;
    }
}
