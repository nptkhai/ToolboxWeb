using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using ToolboxWeb.Web;
using ToolboxWeb.Web.Data;
using ToolboxWeb.Web.Domain;
using ToolboxWeb.Web.Enums;
using ToolboxWeb.Web.Extensions;
using ToolboxWeb.Web.ViewModels;

namespace ToolboxWeb.Web.Services;

public interface ITabulatorTaskService
{
    Task<IReadOnlyList<TabulatorTaskDto>> GetAsync(string userId);
    Task<TabulatorTaskBatchSaveResult> SaveAsync(string userId, TabulatorTaskBatchSaveRequest request);
}

public class TabulatorTaskService : ITabulatorTaskService
{
    private readonly ApplicationDbContext _db;
    private readonly IActivityLogService _activityLog;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public TabulatorTaskService(
        ApplicationDbContext db,
        IActivityLogService activityLog,
        IStringLocalizer<SharedResource> localizer)
    {
        _db = db;
        _activityLog = activityLog;
        _localizer = localizer;
    }

    public async Task<IReadOnlyList<TabulatorTaskDto>> GetAsync(string userId)
    {
        return await BuildDtosAsync(userId);
    }

    public async Task<TabulatorTaskBatchSaveResult> SaveAsync(string userId, TabulatorTaskBatchSaveRequest request)
    {
        request ??= new TabulatorTaskBatchSaveRequest();

        var errors = ValidateRequest(request);
        if (errors.Count > 0)
        {
            return new TabulatorTaskBatchSaveResult
            {
                Success = false,
                Message = _localizer["TabulatorDemo.SaveValidationFailed"].Value,
                Errors = errors
            };
        }

        await using var transaction = await _db.Database.BeginTransactionAsync();

        foreach (var input in request.Created)
        {
            var entity = new TabulatorTask
            {
                UserId = userId,
                Title = input.Title.Trim(),
                Category = input.Category,
                Status = input.Status,
                Priority = input.Priority,
                Progress = input.Progress,
                Budget = input.Budget,
                DueDate = NormalizeDate(input.DueDate),
                Note = input.Note?.Trim(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.TabulatorTasks.Add(entity);
            await _db.SaveChangesAsync();
            await _activityLog.LogAsync(
                userId,
                ActivityActionType.Create,
                ActivityEntityType.TabulatorTask,
                entity.Id.ToString(),
                _localizer["ActivityLog.TabulatorTaskCreatedSummary", entity.Title].Value);
        }

        var updatedIds = request.Updated
            .Where(x => x.Id.HasValue)
            .Select(x => x.Id!.Value)
            .Distinct()
            .ToList();

        var existingUpdated = await _db.TabulatorTasks
            .Where(x => x.UserId == userId && updatedIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id);

        foreach (var input in request.Updated.Where(x => x.Id.HasValue))
        {
            if (!existingUpdated.TryGetValue(input.Id!.Value, out var entity))
            {
                return new TabulatorTaskBatchSaveResult
                {
                    Success = false,
                    Message = _localizer["TabulatorDemo.RowNotFound"].Value,
                    Errors = [_localizer["TabulatorDemo.RowNotFound"].Value]
                };
            }

            entity.Title = input.Title.Trim();
            entity.Category = input.Category;
            entity.Status = input.Status;
            entity.Priority = input.Priority;
            entity.Progress = input.Progress;
            entity.Budget = input.Budget;
            entity.DueDate = NormalizeDate(input.DueDate);
            entity.Note = input.Note?.Trim();
            entity.UpdatedAt = DateTime.UtcNow;
        }

        if (existingUpdated.Count > 0)
        {
            await _db.SaveChangesAsync();

            foreach (var entity in existingUpdated.Values)
            {
                await _activityLog.LogAsync(
                    userId,
                    ActivityActionType.Update,
                    ActivityEntityType.TabulatorTask,
                    entity.Id.ToString(),
                    _localizer["ActivityLog.TabulatorTaskUpdatedSummary", entity.Title].Value);
            }
        }

        var deletedIds = request.DeletedIds.Distinct().ToList();
        if (deletedIds.Count > 0)
        {
            var deleted = await _db.TabulatorTasks
                .Where(x => x.UserId == userId && deletedIds.Contains(x.Id))
                .ToListAsync();

            foreach (var entity in deleted)
            {
                _db.TabulatorTasks.Remove(entity);
            }

            if (deleted.Count > 0)
            {
                await _db.SaveChangesAsync();

                foreach (var entity in deleted)
                {
                    await _activityLog.LogAsync(
                        userId,
                        ActivityActionType.Delete,
                        ActivityEntityType.TabulatorTask,
                        entity.Id.ToString(),
                        _localizer["ActivityLog.TabulatorTaskDeletedSummary", entity.Title].Value);
                }
            }
        }

        await transaction.CommitAsync();

        return new TabulatorTaskBatchSaveResult
        {
            Success = true,
            Message = _localizer["TabulatorDemo.SaveSuccess"].Value,
            Items = await BuildDtosAsync(userId)
        };
    }

    private List<string> ValidateRequest(TabulatorTaskBatchSaveRequest request)
    {
        var errors = new List<string>();

        foreach (var input in request.Created.Concat(request.Updated))
        {
            if (string.IsNullOrWhiteSpace(input.Title))
            {
                errors.Add(_localizer["TabulatorDemo.ValidationTitleRequired"].Value);
            }

            if (input.Progress is < 0 or > 100)
            {
                errors.Add(_localizer["TabulatorDemo.ValidationProgressRange"].Value);
            }

            if (!Enum.IsDefined(input.Category))
            {
                errors.Add(_localizer["TabulatorDemo.ValidationCategoryInvalid"].Value);
            }

            if (!Enum.IsDefined(input.Status))
            {
                errors.Add(_localizer["TabulatorDemo.ValidationStatusInvalid"].Value);
            }

            if (!Enum.IsDefined(input.Priority))
            {
                errors.Add(_localizer["TabulatorDemo.ValidationPriorityInvalid"].Value);
            }
        }

        return errors.Distinct().ToList();
    }

    private async Task<IReadOnlyList<TabulatorTaskDto>> BuildDtosAsync(string userId)
    {
        return await _db.TabulatorTasks
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.Status)
            .ThenBy(x => x.DueDate ?? DateTime.MaxValue)
            .ThenByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .Select(x => new TabulatorTaskDto
            {
                Id = x.Id,
                RowKey = $"db-{x.Id}",
                Title = x.Title,
                Category = x.Category.ToString(),
                CategoryLabel = x.Category.Localize(_localizer),
                Status = x.Status.ToString(),
                StatusLabel = x.Status.Localize(_localizer),
                Priority = x.Priority.ToString(),
                PriorityLabel = x.Priority.Localize(_localizer),
                Progress = x.Progress,
                Budget = x.Budget,
                DueDate = x.DueDate.HasValue ? x.DueDate.Value.ToString("yyyy-MM-dd") : null,
                Note = x.Note,
                UpdatedAt = (x.UpdatedAt ?? x.CreatedAt).ToString("dd/MM/yyyy HH:mm")
            })
            .ToListAsync();
    }

    private static DateTime? NormalizeDate(DateTime? value)
    {
        return value?.Date;
    }
}
