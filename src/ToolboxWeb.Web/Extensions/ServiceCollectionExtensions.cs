using Microsoft.AspNetCore.Identity;
using ToolboxWeb.Web.Infrastructure.Excel;
using ToolboxWeb.Web.Infrastructure.Jira;
using ToolboxWeb.Web.Services;
using ToolboxWeb.Web.ViewModels.Jira;

namespace ToolboxWeb.Web.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddToolboxServices(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IActivityLogService, ActivityLogService>();
        services.AddScoped<INoteService, NoteService>();
        services.AddScoped<IChecklistService, ChecklistService>();
        services.AddScoped<IQuickLinkService, QuickLinkService>();
        services.AddScoped<IPromptTemplateService, PromptTemplateService>();
        services.AddScoped<IFocusSessionService, FocusSessionService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<ITabulatorTaskService, TabulatorTaskService>();
        services.AddScoped<IUserProfileService, UserProfileService>();
        services.AddScoped<IExcelReportService, AsposeExcelReportService>();
        services.AddSingleton<IJiraClientFactory, JiraClientFactory>();
        services.AddSingleton<IJiraSessionStore, JiraSessionStore>();
        services.AddScoped<IJiraAuthService, JiraAuthService>();
        services.AddScoped<IUserClaimsPrincipalFactory<Domain.ApplicationUser>, ToolboxUserClaimsPrincipalFactory>();
        return services;
    }
}
