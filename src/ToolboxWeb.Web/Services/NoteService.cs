using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using ToolboxWeb.Web;
using ToolboxWeb.Web.Data;
using ToolboxWeb.Web.Domain;
using ToolboxWeb.Web.Enums;
using ToolboxWeb.Web.ViewModels;

namespace ToolboxWeb.Web.Services;

public interface INoteService
{
    Task<IReadOnlyList<Note>> GetAsync(string userId, string? search = null, int take = 100);
    Task<Note?> GetByIdAsync(string userId, int id);
    Task<int> CreateAsync(string userId, NoteFormViewModel model);
    Task<bool> UpdateAsync(string userId, NoteFormViewModel model);
    Task<bool> DeleteAsync(string userId, int id);
}

public class NoteService : INoteService
{
    private readonly ApplicationDbContext _db;
    private readonly IActivityLogService _activityLog;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public NoteService(ApplicationDbContext db, IActivityLogService activityLog, IStringLocalizer<SharedResource> localizer)
    {
        _db = db;
        _activityLog = activityLog;
        _localizer = localizer;
    }

    public async Task<IReadOnlyList<Note>> GetAsync(string userId, string? search = null, int take = 100)
    {
        var query = _db.Notes.Where(x => x.UserId == userId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x => x.Title.Contains(search) || x.Content.Contains(search) || (x.Tags != null && x.Tags.Contains(search)));
        }

        return await query
            .OrderByDescending(x => x.IsPinned)
            .ThenByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .Take(take)
            .ToListAsync();
    }

    public Task<Note?> GetByIdAsync(string userId, int id)
    {
        return _db.Notes.FirstOrDefaultAsync(x => x.UserId == userId && x.Id == id);
    }

    public async Task<int> CreateAsync(string userId, NoteFormViewModel model)
    {
        var note = new Note
        {
            UserId = userId,
            Title = model.Title.Trim(),
            Content = model.Content.Trim(),
            Tags = model.Tags?.Trim(),
            IsPinned = model.IsPinned,
            CreatedAt = DateTime.UtcNow
        };

        _db.Notes.Add(note);
        await _db.SaveChangesAsync();
        await _activityLog.LogAsync(userId, ActivityActionType.Create, ActivityEntityType.Note, note.Id.ToString(), _localizer["ActivityLog.NoteCreatedSummary", note.Title].Value);
        return note.Id;
    }

    public async Task<bool> UpdateAsync(string userId, NoteFormViewModel model)
    {
        if (model.Id is null)
        {
            return false;
        }

        var note = await GetByIdAsync(userId, model.Id.Value);
        if (note is null)
        {
            return false;
        }

        note.Title = model.Title.Trim();
        note.Content = model.Content.Trim();
        note.Tags = model.Tags?.Trim();
        note.IsPinned = model.IsPinned;
        note.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await _activityLog.LogAsync(userId, ActivityActionType.Update, ActivityEntityType.Note, note.Id.ToString(), _localizer["ActivityLog.NoteUpdatedSummary", note.Title].Value);
        return true;
    }

    public async Task<bool> DeleteAsync(string userId, int id)
    {
        var note = await GetByIdAsync(userId, id);
        if (note is null)
        {
            return false;
        }

        _db.Notes.Remove(note);
        await _db.SaveChangesAsync();
        await _activityLog.LogAsync(userId, ActivityActionType.Delete, ActivityEntityType.Note, id.ToString(), _localizer["ActivityLog.NoteDeletedSummary", note.Title].Value);
        return true;
    }
}
