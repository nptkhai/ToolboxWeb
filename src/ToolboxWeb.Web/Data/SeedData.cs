using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using ToolboxWeb.Web;
using ToolboxWeb.Web.Domain;
using ToolboxWeb.Web.Enums;

namespace ToolboxWeb.Web.Data;

public static class SeedData
{
    public static async Task EnsureUserDefaultsAsync(ApplicationDbContext db, string userId, IStringLocalizer<SharedResource> localizer)
    {
        var existingCategories = await db.PromptTemplates
            .Where(x => x.UserId == userId)
            .Select(x => x.Category)
            .ToListAsync();

        var promptsToAdd = new List<PromptTemplate>();
        AddIfMissing(promptsToAdd, existingCategories, userId, localizer, PromptCategory.FeatureRequest, "SeedPrompt.FeatureRequest");
        AddIfMissing(promptsToAdd, existingCategories, userId, localizer, PromptCategory.BugReport, "SeedPrompt.BugReport");
        AddIfMissing(promptsToAdd, existingCategories, userId, localizer, PromptCategory.Maintenance, "SeedPrompt.MaintenanceHandoff");
        AddIfMissing(promptsToAdd, existingCategories, userId, localizer, PromptCategory.CodeReview, "SeedPrompt.CodeReview");
        AddIfMissing(promptsToAdd, existingCategories, userId, localizer, PromptCategory.UiChange, "SeedPrompt.UiChange");
        AddIfMissing(promptsToAdd, existingCategories, userId, localizer, PromptCategory.DatabaseChange, "SeedPrompt.DatabaseChange");

        if (promptsToAdd.Count == 0)
        {
            return;
        }

        db.PromptTemplates.AddRange(promptsToAdd);

        await db.SaveChangesAsync();
    }

    private static void AddIfMissing(List<PromptTemplate> promptsToAdd, IReadOnlyCollection<PromptCategory> existingCategories, string userId, IStringLocalizer<SharedResource> localizer, PromptCategory category, string keyPrefix)
    {
        if (existingCategories.Contains(category))
        {
            return;
        }

        promptsToAdd.Add(CreatePrompt(userId, localizer, category, keyPrefix));
    }

    private static PromptTemplate CreatePrompt(string userId, IStringLocalizer<SharedResource> localizer, PromptCategory category, string keyPrefix)
    {
        return new PromptTemplate
        {
            UserId = userId,
            Name = localizer[$"{keyPrefix}.Name"].Value,
            Category = category,
            Description = localizer[$"{keyPrefix}.Description"].Value,
            InputVariables = localizer[$"{keyPrefix}.InputVariables"].Value,
            OutputFormat = localizer[$"{keyPrefix}.OutputFormat"].Value,
            Content = localizer[$"{keyPrefix}.Content"].Value
        };
    }
}
