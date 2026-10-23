using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using ToolboxWeb.Web;
using ToolboxWeb.Web.Data;
using ToolboxWeb.Web.Domain;
using ToolboxWeb.Web.Enums;
using ToolboxWeb.Web.Infrastructure.Time;
using ToolboxWeb.Web.ViewModels;

namespace ToolboxWeb.Web.Services;

public interface IFocusSessionService
{
    Task<IReadOnlyList<FocusSession>> GetRecentAsync(string userId, int take = 10);

    /// <summary>Saves a finished session; returns null when the same session (same start) was already saved.</summary>
    Task<int?> CompleteAsync(string userId, FocusSessionFormViewModel model);

    /// <summary>Minutes of focus sessions completed on or after <paramref name="fromUtc"/>.</summary>
    Task<int> GetMinutesSinceAsync(string userId, DateTime fromUtc);

    /// <summary>Today, this week, the last seven days and the recent sessions grouped by day.</summary>
    Task<FocusPageStats> GetStatsAsync(string userId, int historyDays = 14);

    Task<bool> UpdateNoteAsync(string userId, int id, string? note);
    Task<bool> DeleteAsync(string userId, int id);
}

/// <summary>Minutes of focus on one day of the app time zone.</summary>
public sealed record FocusDayMinutes(DateTime Date, int Minutes);

public sealed record FocusHistoryDay(DateTime Date, int Minutes, IReadOnlyList<FocusSession> Sessions);

public sealed record FocusPageStats(int TodayMinutes, int WeekMinutes, IReadOnlyList<FocusDayMinutes> LastSevenDays, IReadOnlyList<FocusHistoryDay> History);

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

    public async Task<int> GetMinutesSinceAsync(string userId, DateTime fromUtc)
    {
        return await _db.FocusSessions
            .Where(x => x.UserId == userId && x.CompletedAt >= fromUtc)
            .SumAsync(x => x.DurationMinutes);
    }

    public async Task<FocusPageStats> GetStatsAsync(string userId, int historyDays = 14)
    {
        var today = AppClock.Today;
        var weekStart = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var from = new[] { today.AddDays(-(historyDays - 1)), today.AddDays(-6), weekStart }.Min();

        var sessions = await _db.FocusSessions.AsNoTracking()
            .Include(x => x.ChecklistItem)
            .Where(x => x.UserId == userId && x.CompletedAt >= ToUtc(from))
            .OrderByDescending(x => x.CompletedAt)
            .ToListAsync();

        var byDay = sessions.GroupBy(x => x.CompletedAt.ToAppTime().Date).ToDictionary(x => x.Key, x => x.ToList());
        int MinutesOn(DateTime day) => byDay.TryGetValue(day, out var list) ? list.Sum(x => x.DurationMinutes) : 0;

        return new FocusPageStats(
            MinutesOn(today),
            sessions.Where(x => x.CompletedAt.ToAppTime().Date >= weekStart).Sum(x => x.DurationMinutes),
            Enumerable.Range(0, 7).Select(i => today.AddDays(i - 6)).Select(day => new FocusDayMinutes(day, MinutesOn(day))).ToList(),
            byDay.Where(x => x.Key >= today.AddDays(-(historyDays - 1)))
                .OrderByDescending(x => x.Key)
                .Select(x => new FocusHistoryDay(x.Key, x.Value.Sum(s => s.DurationMinutes), x.Value))
                .ToList());
    }

    public async Task<int?> CompleteAsync(string userId, FocusSessionFormViewModel model)
    {
        var now = DateTime.UtcNow;
        // The browser's times are trusted only when they make sense: not in the future, the end
        // after the start. Otherwise the session counts as ending now.
        var completedAt = FromBrowserTime(model.EndedAtMs) is { } ended && ended <= now.AddMinutes(1)
            ? (ended > now ? now : ended)
            : now;
        var startedAt = FromBrowserTime(model.StartedAtMs) is { } started && started <= completedAt
            ? started
            : completedAt.AddMinutes(-model.DurationMinutes);

        // The timer runs in every open tab; when it ends, each tab may send the same session.
        if (model.StartedAtMs is not null
            && await _db.FocusSessions.AnyAsync(x => x.UserId == userId
                && x.StartedAt >= startedAt.AddSeconds(-1) && x.StartedAt <= startedAt.AddSeconds(1)))
        {
            return null;
        }

        int? todoId = null;
        if (model.ChecklistItemId is int id
            && await _db.ChecklistItems.AnyAsync(x => x.UserId == userId && x.Id == id && x.DeletedAt == null))
        {
            todoId = id;
        }

        var session = new FocusSession
        {
            UserId = userId,
            DurationMinutes = model.DurationMinutes,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            Note = string.IsNullOrWhiteSpace(model.Note) ? null : model.Note.Trim(),
            ChecklistItemId = todoId
        };

        _db.FocusSessions.Add(session);
        await _db.SaveChangesAsync();
        await _activityLog.LogAsync(userId, ActivityActionType.Complete, ActivityEntityType.FocusSession, session.Id.ToString(), _localizer["ActivityLog.FocusSessionCompletedSummary", session.DurationMinutes].Value);
        return session.Id;
    }

    public async Task<bool> UpdateNoteAsync(string userId, int id, string? note)
    {
        var session = await _db.FocusSessions.FirstOrDefaultAsync(x => x.UserId == userId && x.Id == id);
        if (session is null)
        {
            return false;
        }

        session.Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        await _db.SaveChangesAsync();
        await _activityLog.LogAsync(userId, ActivityActionType.Update, ActivityEntityType.FocusSession, session.Id.ToString(), _localizer["ActivityLog.FocusSessionUpdatedSummary", session.DurationMinutes].Value);
        return true;
    }

    public async Task<bool> DeleteAsync(string userId, int id)
    {
        var session = await _db.FocusSessions.FirstOrDefaultAsync(x => x.UserId == userId && x.Id == id);
        if (session is null)
        {
            return false;
        }

        _db.FocusSessions.Remove(session);
        await _db.SaveChangesAsync();
        await _activityLog.LogAsync(userId, ActivityActionType.Delete, ActivityEntityType.FocusSession, session.Id.ToString(), _localizer["ActivityLog.FocusSessionDeletedSummary", session.DurationMinutes].Value);
        return true;
    }

    private static DateTime? FromBrowserTime(long? ms)
    {
        // Year 2000 to 9999: a value outside that is not a real time.
        return ms is long value && value is > 946_684_800_000 and < 253_402_300_799_000
            ? DateTimeOffset.FromUnixTimeMilliseconds(value).UtcDateTime
            : null;
    }

    private static DateTime ToUtc(DateTime appDate)
    {
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(appDate, DateTimeKind.Unspecified), AppClock.TimeZone);
    }
}
