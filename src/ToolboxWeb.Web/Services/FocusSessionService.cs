using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using ToolboxWeb.Web;
using ToolboxWeb.Web.Data;
using ToolboxWeb.Web.Domain;
using ToolboxWeb.Web.Enums;
using ToolboxWeb.Web.ViewModels;

namespace ToolboxWeb.Web.Services;

public interface IFocusSessionService
{
    Task<IReadOnlyList<FocusSession>> GetRecentAsync(string userId, int take = 10);
    Task<int> CompleteAsync(string userId, FocusSessionFormViewModel model);
}

public class FocusSessionService : IFocusSessionService
{
    private readonly ApplicationDbContext _db;
    private readonly IActivityLogService _activityLog;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public FocusSessionService(ApplicationDbContext db, IActivityLogService activityLog, IStringLocalizer<SharedResource> localizer)
    {
        _db = db;
        _activityLog = activityLog;
        _localizer = localizer;
    }

    public async Task<IReadOnlyList<FocusSession>> GetRecentAsync(string userId, int take = 10)
    {
        return await _db.FocusSessions
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CompletedAt)
            .Take(take)
            .ToListAsync();
    }

    public async Task<int> CompleteAsync(string userId, FocusSessionFormViewModel model)
    {
        var completedAt = DateTime.UtcNow;
        var session = new FocusSession
        {
            UserId = userId,
            DurationMinutes = model.DurationMinutes,
            StartedAt = completedAt.AddMinutes(-model.DurationMinutes),
            CompletedAt = completedAt,
            Note = model.Note?.Trim()
        };

        _db.FocusSessions.Add(session);
        await _db.SaveChangesAsync();
        await _activityLog.LogAsync(userId, ActivityActionType.Complete, ActivityEntityType.FocusSession, session.Id.ToString(), _localizer["ActivityLog.FocusSessionCompletedSummary", session.DurationMinutes].Value);
        return session.Id;
    }
}
