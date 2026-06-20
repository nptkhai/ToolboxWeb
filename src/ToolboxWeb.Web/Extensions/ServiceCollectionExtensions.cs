using ToolboxWeb.Web.Infrastructure.Excel;
using ToolboxWeb.Web.Services;

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
        services.AddScoped<IExcelReportService, AsposeExcelReportService>();
        return services;
    }
}
