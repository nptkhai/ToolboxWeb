using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using ToolboxWeb.Web;
using ToolboxWeb.Web.Data;
using ToolboxWeb.Web.Domain;
using ToolboxWeb.Web.Enums;
using ToolboxWeb.Web.ViewModels;

namespace ToolboxWeb.Web.Services;

public interface IPromptTemplateService
{
    Task<IReadOnlyList<PromptTemplate>> GetAsync(string userId, int take = 100);
    Task<PromptTemplate?> GetByIdAsync(string userId, int id);
    Task<int> CreateAsync(string userId, PromptTemplateFormViewModel model);
    Task<bool> UpdateAsync(string userId, PromptTemplateFormViewModel model);
    Task<bool> MarkUsedAsync(string userId, int id);
    Task<bool> DeleteAsync(string userId, int id);
}

public class PromptTemplateService : IPromptTemplateService
{
    private readonly ApplicationDbContext _db;
    private readonly IActivityLogService _activityLog;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public PromptTemplateService(ApplicationDbContext db, IActivityLogService activityLog, IStringLocalizer<SharedResource> localizer)
    {
        _db = db;
        _activityLog = activityLog;
        _localizer = localizer;
    }

    public async Task<IReadOnlyList<PromptTemplate>> GetAsync(string userId, int take = 100)
    {
        return await _db.PromptTemplates
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.Category)
            .ThenBy(x => x.Name)
            .Take(take)
            .ToListAsync();
    }

    public async Task<PromptTemplate?> GetByIdAsync(string userId, int id)
    {
        return await _db.PromptTemplates.FirstOrDefaultAsync(x => x.UserId == userId && x.Id == id);
    }

    public async Task<int> CreateAsync(string userId, PromptTemplateFormViewModel model)
    {
        var template = new PromptTemplate
        {
            UserId = userId,
            Name = model.Name.Trim(),
            Category = model.Category,
            Description = model.Description?.Trim(),
            InputVariables = model.InputVariables?.Trim(),
            OutputFormat = model.OutputFormat?.Trim(),
            Content = model.Content.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _db.PromptTemplates.Add(template);
        await _db.SaveChangesAsync();
        await _activityLog.LogAsync(userId, ActivityActionType.Create, ActivityEntityType.PromptTemplate, template.Id.ToString(), _localizer["ActivityLog.PromptCreatedSummary", template.Name].Value);
        return template.Id;
    }

    public async Task<bool> UpdateAsync(string userId, PromptTemplateFormViewModel model)
    {
        if (!model.Id.HasValue)
        {
            return false;
        }

        var template = await _db.PromptTemplates.FirstOrDefaultAsync(x => x.UserId == userId && x.Id == model.Id.Value);
        if (template is null)
        {
            return false;
        }

        template.Name = model.Name.Trim();
        template.Category = model.Category;
        template.Description = model.Description?.Trim();
        template.InputVariables = model.InputVariables?.Trim();
        template.OutputFormat = model.OutputFormat?.Trim();
        template.Content = model.Content.Trim();
        template.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await _activityLog.LogAsync(userId, ActivityActionType.Update, ActivityEntityType.PromptTemplate, template.Id.ToString(), _localizer["ActivityLog.PromptUpdatedSummary", template.Name].Value);
        return true;
    }

    public async Task<bool> MarkUsedAsync(string userId, int id)
    {
        var template = await _db.PromptTemplates.FirstOrDefaultAsync(x => x.UserId == userId && x.Id == id);
        if (template is null)
        {
            return false;
        }

        template.UseCount++;
        template.LastUsedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _activityLog.LogAsync(userId, ActivityActionType.Use, ActivityEntityType.PromptTemplate, template.Id.ToString(), _localizer["ActivityLog.PromptUsedSummary", template.Name].Value);
        return true;
    }

    public async Task<bool> DeleteAsync(string userId, int id)
    {
        var template = await _db.PromptTemplates.FirstOrDefaultAsync(x => x.UserId == userId && x.Id == id);
        if (template is null)
        {
            return false;
        }

        _db.PromptTemplates.Remove(template);
        await _db.SaveChangesAsync();
        await _activityLog.LogAsync(userId, ActivityActionType.Delete, ActivityEntityType.PromptTemplate, template.Id.ToString(), _localizer["ActivityLog.PromptDeletedSummary", template.Name].Value);
        return true;
    }
}
