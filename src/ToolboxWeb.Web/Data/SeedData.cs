using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using ToolboxWeb.Web;
using ToolboxWeb.Web.Domain;
using ToolboxWeb.Web.Enums;

namespace ToolboxWeb.Web.Data;

public static class SeedData
{
    /// <summary>
    /// The built-in prompts: category and localization key prefix (SeedPrompt.{Key}.*). The texts
    /// live in docs/FEATURE_PROMPTS.md, docs/MAINTENANCE_PROMPTS.md and both localization files.
    /// </summary>
    public static readonly IReadOnlyList<(PromptCategory Category, string Key)> PromptSeeds =
    [
        (PromptCategory.FeatureRequest, "FeatureRequest"),
        (PromptCategory.BugReport, "BugReport"),
        (PromptCategory.Maintenance, "MaintenanceHandoff"),
        (PromptCategory.CodeReview, "CodeReview"),
        (PromptCategory.UiChange, "UiChange"),
        (PromptCategory.UiChange, "NewScreen"),
        (PromptCategory.DatabaseChange, "DatabaseChange")
    ];

    /// <summary>
    /// Gives a user the built-in prompts of every category they have none in. A user who already
    /// has prompts keeps them; "Restore built-in prompts" on the prompt page brings newer texts.
    /// </summary>
    public static async Task EnsureUserDefaultsAsync(ApplicationDbContext db, string userId, IStringLocalizer<SharedResource> localizer)
    {
        var existingCategories = await db.PromptTemplates
            .Where(x => x.UserId == userId)
            .Select(x => x.Category)
            .ToListAsync();

        var promptsToAdd = PromptSeeds
            .Where(seed => !existingCategories.Contains(seed.Category))
            .Select(seed => CreatePrompt(userId, localizer, seed.Category, seed.Key))
            .ToList();

        if (promptsToAdd.Count == 0)
        {
            return;
        }

        db.PromptTemplates.AddRange(promptsToAdd);

        await db.SaveChangesAsync();
    }

    public static PromptTemplate CreatePrompt(string userId, IStringLocalizer<SharedResource> localizer, PromptCategory category, string key)
    {
        var keyPrefix = $"SeedPrompt.{key}";
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
