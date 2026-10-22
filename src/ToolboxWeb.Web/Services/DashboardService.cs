using Microsoft.Extensions.Localization;
using ToolboxWeb.Web;
using ToolboxWeb.Web.Data;
using ToolboxWeb.Web.Domain;
using ToolboxWeb.Web.Enums;
using ToolboxWeb.Web.Infrastructure.Time;
using ToolboxWeb.Web.ViewModels;

namespace ToolboxWeb.Web.Services;

public interface IDashboardService
{
    Task<DashboardViewModel> BuildAsync(string userId, string displayName, bool hasJiraSession);

    /// <summary>Adds a task from the dashboard's one-line form; "today" gives it today's date.</summary>
    Task CreateQuickTaskAsync(string userId, DashboardQuickTaskViewModel model);
}

public class DashboardService : IDashboardService
{
    // How many tasks each group shows; the rest are counted and reached through the to-do list.
    private const int TasksPerGroup = 5;

    private readonly ApplicationDbContext _db;
    private readonly INoteService _notes;
    private readonly IChecklistService _checklist;
    private readonly IQuickLinkService _quickLinks;
    private readonly IPromptTemplateService _prompts;
    private readonly IFocusSessionService _focusSessions;
    private readonly IActivityLogService _activity;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public DashboardService(
        ApplicationDbContext db,
        INoteService notes,
        IChecklistService checklist,
        IQuickLinkService quickLinks,
        IPromptTemplateService prompts,
        IFocusSessionService focusSessions,
        IActivityLogService activity,
        IStringLocalizer<SharedResource> localizer)
    {
        _db = db;
        _notes = notes;
        _checklist = checklist;
        _quickLinks = quickLinks;
        _prompts = prompts;
        _focusSessions = focusSessions;
        _activity = activity;
        _localizer = localizer;
    }

    public async Task<DashboardViewModel> BuildAsync(string userId, string displayName, bool hasJiraSession)
    {
        await SeedData.EnsureUserDefaultsAsync(_db, userId, _localizer);

        var now = AppClock.Now;
        var today = now.Date;
        var open = (await _checklist.GetAsync(userId, 1000))
            .Where(x => x.Status == ChecklistStatus.Todo)
            .ToList();

        // Overdue and today first, then what is coming, then what has no date at all.
        var groups = open
            .GroupBy(x => GroupOf(x.DueDate, today))
            .OrderBy(x => x.Key)
            .Select(group => new DashboardTaskGroupViewModel
            {
                Group = group.Key,
                Total = group.Count(),
                Items = group
                    .OrderBy(x => x.DueDate ?? DateTime.MaxValue)
                    .ThenByDescending(x => x.CreatedAt)
                    .Take(TasksPerGroup)
                    .ToList()
            })
            .ToList();

        var weekStart = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));

        return new DashboardViewModel
        {
            DisplayName = displayName,
            Now = now,
            Today = today,
            OverdueCount = groups.FirstOrDefault(x => x.Group == DashboardTaskGroup.Overdue)?.Total ?? 0,
            DueTodayCount = groups.FirstOrDefault(x => x.Group == DashboardTaskGroup.Today)?.Total ?? 0,
            OpenTaskCount = open.Count,
            TaskGroups = groups,
            FocusMinutesToday = await _focusSessions.GetMinutesSinceAsync(userId, ToUtc(today)),
            FocusMinutesWeek = await _focusSessions.GetMinutesSinceAsync(userId, ToUtc(weekStart)),
            RecentFocusSessions = await _focusSessions.GetRecentAsync(userId, 3),
            HasJiraSession = hasJiraSession,
            RecentNotes = await _notes.GetAsync(userId, take: 5),
            QuickLinks = await _quickLinks.GetAsync(userId, 8),
            PromptTemplates = await _prompts.GetAsync(userId, 5),
            RecentActivity = await _activity.GetRecentAsync(userId, 5)
        };
    }

    public async Task CreateQuickTaskAsync(string userId, DashboardQuickTaskViewModel model)
    {
        await _checklist.CreateAsync(userId, new ChecklistItemFormViewModel
        {
            Title = model.Title.Trim(),
            DueDate = string.Equals(model.Due, "today", StringComparison.OrdinalIgnoreCase) ? AppClock.Today : null
        });
    }

    private static DashboardTaskGroup GroupOf(DateTime? dueDate, DateTime today)
    {
        if (dueDate is null)
        {
            return DashboardTaskGroup.NoDueDate;
        }

        var due = dueDate.Value.Date;
        return due < today ? DashboardTaskGroup.Overdue
            : due == today ? DashboardTaskGroup.Today
            : DashboardTaskGroup.Upcoming;
    }

    // Midnight of an app-time date, as the UTC instant stored times are compared with.
    private static DateTime ToUtc(DateTime appDate)
    {
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(appDate, DateTimeKind.Unspecified), AppClock.TimeZone);
    }
}
