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
        if (await db.PromptTemplates.AnyAsync(x => x.UserId == userId))
        {
            return;
        }

        db.PromptTemplates.AddRange(
            new PromptTemplate
            {
                UserId = userId,
                Name = localizer["SeedPrompt.FeatureRequest.Name"].Value,
                Category = PromptCategory.FeatureRequest,
                Description = localizer["SeedPrompt.FeatureRequest.Description"].Value,
                Content = localizer["SeedPrompt.FeatureRequest.Content"].Value
            },
            new PromptTemplate
            {
                UserId = userId,
                Name = localizer["SeedPrompt.BugReport.Name"].Value,
                Category = PromptCategory.BugReport,
                Description = localizer["SeedPrompt.BugReport.Description"].Value,
                Content = localizer["SeedPrompt.BugReport.Content"].Value
            },
            new PromptTemplate
            {
                UserId = userId,
                Name = localizer["SeedPrompt.MaintenanceHandoff.Name"].Value,
                Category = PromptCategory.Maintenance,
                Description = localizer["SeedPrompt.MaintenanceHandoff.Description"].Value,
                Content = localizer["SeedPrompt.MaintenanceHandoff.Content"].Value
            },
            new PromptTemplate
            {
                UserId = userId,
                Name = localizer["SeedPrompt.CodeReview.Name"].Value,
                Category = PromptCategory.CodeReview,
                Description = localizer["SeedPrompt.CodeReview.Description"].Value,
                Content = localizer["SeedPrompt.CodeReview.Content"].Value
            });

        await db.SaveChangesAsync();
    }
}
