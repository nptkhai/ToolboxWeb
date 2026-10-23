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
    Task<PromptTemplatesIndexViewModel> GetListAsync(string userId, PromptListQuery query);

    /// <summary>The prompts a person reaches for first: pinned, then the most used.</summary>
    Task<IReadOnlyList<PromptTemplate>> GetTopAsync(string userId, int take);
    Task<PromptTemplate?> GetByIdAsync(string userId, int id);
    Task<int> CreateAsync(string userId, PromptTemplateFormViewModel model);
    Task<bool> UpdateAsync(string userId, PromptTemplateFormViewModel model);

    /// <summary>Counts one use; returns the prompt afterwards, or null when it is not the user's.</summary>
    Task<PromptTemplate?> MarkUsedAsync(string userId, int id);

    /// <summary>Returns the new pinned state, or null when the prompt is not the user's.</summary>
    Task<bool?> TogglePinAsync(string userId, int id);

    /// <summary>Returns the id of the copy, or null when the prompt is not the user's.</summary>
    Task<int?> DuplicateAsync(string userId, int id);

    Task<bool> DeleteAsync(string userId, int id);
    Task<PromptExportFile> ExportAsync(string userId);
    Task<PromptImportResult> ImportAsync(string userId, IReadOnlyList<PromptExportItem> items);
}

public class PromptTemplateService : IPromptTemplateService
{
    /// <summary>A file larger than this is not a prompt backup.</summary>
    public const int MaxImportItems = 500;

    private readonly ApplicationDbContext _db;
    private readonly IActivityLogService _activityLog;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public PromptTemplateService(ApplicationDbContext db, IActivityLogService activityLog, IStringLocalizer<SharedResource> localizer)
    {
        _db = db;
        _activityLog = activityLog;
        _localizer = localizer;
    }

    public async Task<PromptTemplatesIndexViewModel> GetListAsync(string userId, PromptListQuery query)
    {
        // A person's prompts number in the dozens, so one read and in-memory shaping lets the
        // search ignore case and Vietnamese accents (NoteSearch) on every field.
        var all = await _db.PromptTemplates.AsNoTracking().Where(x => x.UserId == userId).ToListAsync();
        var terms = NoteSearch.Terms(query.Search);
        var matching = all
            .Where(x => NoteSearch.Matches(terms, x.Name, x.Description, x.InputVariables, x.OutputFormat, x.Content))
            .ToList();

        var filtered = query.Category is { } category ? matching.Where(x => x.Category == category) : matching;
        var ordered = filtered.OrderByDescending(x => x.IsPinned);
        ordered = (PromptSorts.Normalize(query.Sort) ?? PromptSorts.Name) switch
        {
            PromptSorts.MostUsed => ordered.ThenByDescending(x => x.UseCount).ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase),
            PromptSorts.RecentlyUsed => ordered.ThenByDescending(x => x.LastUsedAt ?? DateTime.MinValue).ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase),
            _ => ordered.ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
        };

        return new PromptTemplatesIndexViewModel
        {
            Query = query,
            Templates = ordered.ToList(),
            Total = all.Count,
            PinnedCount = all.Count(x => x.IsPinned),
            SearchCount = matching.Count,
            CategoryCounts = matching.GroupBy(x => x.Category).ToDictionary(x => x.Key, x => x.Count())
        };
    }

    public async Task<IReadOnlyList<PromptTemplate>> GetTopAsync(string userId, int take)
    {
        return await _db.PromptTemplates.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.IsPinned)
            .ThenByDescending(x => x.UseCount)
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

    public async Task<PromptTemplate?> MarkUsedAsync(string userId, int id)
    {
        var template = await _db.PromptTemplates.FirstOrDefaultAsync(x => x.UserId == userId && x.Id == id);
        if (template is null)
        {
            return null;
        }

        template.UseCount++;
        template.LastUsedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _activityLog.LogAsync(userId, ActivityActionType.Use, ActivityEntityType.PromptTemplate, template.Id.ToString(), _localizer["ActivityLog.PromptUsedSummary", template.Name].Value);
        return template;
    }

    public async Task<bool?> TogglePinAsync(string userId, int id)
    {
        var template = await _db.PromptTemplates.FirstOrDefaultAsync(x => x.UserId == userId && x.Id == id);
        if (template is null)
        {
            return null;
        }

        template.IsPinned = !template.IsPinned;
        await _db.SaveChangesAsync();
        return template.IsPinned;
    }

    public async Task<int?> DuplicateAsync(string userId, int id)
    {
        var source = await _db.PromptTemplates.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId && x.Id == id);
        if (source is null)
        {
            return null;
        }

        var name = _localizer["PromptTemplates.CopyName", source.Name].Value;
        return await CreateAsync(userId, new PromptTemplateFormViewModel
        {
            Name = name.Length > 140 ? name[..140] : name,
            Category = source.Category,
            Description = source.Description,
            InputVariables = source.InputVariables,
            OutputFormat = source.OutputFormat,
            Content = source.Content
        });
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

    public async Task<PromptExportFile> ExportAsync(string userId)
    {
        var prompts = await _db.PromptTemplates.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.Category)
            .ThenBy(x => x.Name)
            .ToListAsync();

        return new PromptExportFile
        {
            ExportedAt = DateTime.UtcNow,
            Prompts = prompts.Select(x => new PromptExportItem
            {
                Name = x.Name,
                Category = x.Category.ToString(),
                Description = x.Description,
                InputVariables = x.InputVariables,
                OutputFormat = x.OutputFormat,
                Content = x.Content,
                IsPinned = x.IsPinned
            }).ToList()
        };
    }

    /// <summary>
    /// Adds the prompts of a backup. A prompt with the same name and content as one the user
    /// already has is skipped, so importing the same file twice does not double the list; one
    /// that breaks the field limits is skipped too.
    /// </summary>
    public async Task<PromptImportResult> ImportAsync(string userId, IReadOnlyList<PromptExportItem> items)
    {
        var existing = await _db.PromptTemplates.AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => new { x.Name, x.Content })
            .ToListAsync();
        var seen = new HashSet<string>(existing.Select(x => Key(x.Name, x.Content)), StringComparer.Ordinal);

        var created = 0;
        var skipped = 0;
        foreach (var item in items.Take(MaxImportItems))
        {
            var name = item.Name?.Trim() ?? string.Empty;
            var content = item.Content?.Trim() ?? string.Empty;
            if (name.Length is 0 or > 140 || content.Length == 0
                || (item.Description?.Length ?? 0) > 500
                || (item.InputVariables?.Length ?? 0) > 1000
                || (item.OutputFormat?.Length ?? 0) > 1000
                || !seen.Add(Key(name, content)))
            {
                skipped++;
                continue;
            }

            _db.PromptTemplates.Add(new PromptTemplate
            {
                UserId = userId,
                Name = name,
                Category = Enum.TryParse<PromptCategory>(item.Category, true, out var category) && Enum.IsDefined(category)
                    ? category
                    : PromptCategory.General,
                Description = item.Description?.Trim(),
                InputVariables = item.InputVariables?.Trim(),
                OutputFormat = item.OutputFormat?.Trim(),
                Content = content,
                IsPinned = item.IsPinned,
                CreatedAt = DateTime.UtcNow
            });
            created++;
        }

        skipped += Math.Max(0, items.Count - MaxImportItems);
        if (created > 0)
        {
            await _db.SaveChangesAsync();
            await _activityLog.LogAsync(userId, ActivityActionType.Create, ActivityEntityType.PromptTemplate, null, _localizer["ActivityLog.PromptImportedSummary", created].Value);
        }

        return new PromptImportResult(created, skipped);
    }

    private static string Key(string name, string content) => name.Trim().ToLowerInvariant() + "\n" + content.Trim();
}
