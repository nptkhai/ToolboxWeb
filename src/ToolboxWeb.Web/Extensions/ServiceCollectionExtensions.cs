using Microsoft.AspNetCore.Identity;
using ToolboxWeb.Web.Infrastructure.Excel;
using ToolboxWeb.Web.Infrastructure.DacFx;
using ToolboxWeb.Web.Infrastructure.Jira;
using ToolboxWeb.Web.Infrastructure.Notes;
using ToolboxWeb.Web.Infrastructure.SqlProfiler;
using ToolboxWeb.Web.Infrastructure.SqlSchema;
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
        // Stateless wrapper over the persisted Data Protection key ring.
        services.AddSingleton<INoteSecretProtector, NoteSecretProtector>();
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

        services.AddOptions<SqlSchemaOptions>().BindConfiguration("SchemaCompare");
        services.AddScoped<ISqlConnectionStringFactory, SqlConnectionStringFactory>();
        services.AddScoped<ISqlSchemaReader, SqlSchemaReader>();
        services.AddScoped<ISchemaComparer, SchemaComparer>();
        services.AddScoped<ISyncScriptGenerator, SyncScriptGenerator>();
        services.AddSingleton<ISchemaCompareResultStore, SchemaCompareResultStore>();
        services.AddScoped<ISchemaCompareService, SchemaCompareService>();

        // DacFx engine: stateless, so a singleton. The session store outlives requests
        // because a comparison stays alive while the user ticks rows.
        services.AddSingleton<IDacFxCompareEngine, DacFxCompareEngine>();
        services.AddSingleton<ISchemaCompareSessionStore, SchemaCompareSessionStore>();
        services.AddScoped<IDacFxSchemaCompareService, DacFxSchemaCompareService>();

        // SQL profiler. The store is a singleton because a capture outlives the request that
        // started it and must be reachable to stop and clean up.
        services.AddSingleton<IXEventSessionManager, XEventSessionManager>();
        services.AddSingleton<IProcedureStatsReader, ProcedureStatsReader>();
        services.AddSingleton<ISqlProfilerSessionStore, SqlProfilerSessionStore>();
        services.AddScoped<ISqlProfilerService, SqlProfilerService>();
        services.AddHostedService<SqlProfilerJanitor>();

        return services;
    }
}
