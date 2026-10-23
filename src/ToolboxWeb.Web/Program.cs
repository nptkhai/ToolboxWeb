using System.Globalization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using ToolboxWeb.Web.Constants;
using ToolboxWeb.Web.Data;
using ToolboxWeb.Web.Domain;
using ToolboxWeb.Web.Extensions;
using ToolboxWeb.Web.Infrastructure.DacFx;
using ToolboxWeb.Web.Infrastructure.Jira;
using ToolboxWeb.Web.Infrastructure.Logging;
using ToolboxWeb.Web.Infrastructure.Storage;
using ToolboxWeb.Web.Infrastructure.Time;
using ToolboxWeb.Web.Infrastructure.Web;
using ToolboxWeb.Web.Localization;
using ToolboxWeb.Web.ViewModels.Jira;

if (await DacFxExtractWorker.TryRunAsync(args))
{
    return;
}

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    WebRootPath = "Content"
});
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Under IIS Express there is no console to read, so anything worth diagnosing after the fact
// has to reach a file. See Logging:File in appsettings.json.
builder.Logging.AddFile(builder.Configuration, builder.Environment.ContentRootPath);

var runtimeStorage = RuntimeStoragePaths.Create(builder.Environment, builder.Configuration);
runtimeStorage.Initialize(builder.Environment.ContentRootPath);
builder.Services.AddSingleton(runtimeStorage);

var supportedCultures = new[]
{
    new CultureInfo("vi-VN"),
    new CultureInfo("en-US")
};

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
var sqliteConnection = new SqliteConnectionStringBuilder(connectionString)
{
    DataSource = runtimeStorage.DatabasePath
}.ToString();
builder.Services.AddLocalization();
builder.Services.AddSingleton<JsonLocalizationStore>();
builder.Services.Replace(ServiceDescriptor.Singleton<IStringLocalizerFactory, JsonStringLocalizerFactory>());
// One display time zone for server dates and the client clock (App:TimeZone, IANA id).
AppClock.Configure(builder.Configuration["App:TimeZone"]);
builder.Services.Configure<JiraOptions>(builder.Configuration.GetSection("Jira"));
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture("vi-VN");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    options.ApplyCurrentCultureToResponseHeaders = true;
    options.RequestCultureProviders =
    [
        new CookieRequestCultureProvider(),
        new QueryStringRequestCultureProvider(),
        new AcceptLanguageHeaderRequestCultureProvider()
    ];
});
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(sqliteConnection));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(Math.Max(1, builder.Configuration.GetValue<int?>("Jira:SessionTimeoutHours") ?? 4));
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+:";
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddErrorDescriber<LocalizedIdentityErrorDescriber>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("JiraOnly", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim(ToolboxClaimTypes.AuthSource, AuthSources.Jira);
    });
});
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Events = new CookieAuthenticationEvents
    {
        OnRedirectToLogin = context =>
        {
            if (IsAjaxRequest(context.Request))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        },
        OnRedirectToAccessDenied = context =>
        {
            if (IsAjaxRequest(context.Request))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        }
    };
});
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(runtimeStorage.DataProtectionKeysPath));
var mvcBuilder = builder.Services.AddControllersWithViews()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization();
if (builder.Environment.IsDevelopment())
{
    mvcBuilder.AddRazorRuntimeCompilation();
}
builder.Services.AddToolboxServices();

// Sample Jira data for checking the Jira screens without a Jira account. Development only:
// the flag is ignored in every other environment so it can never reach real users.
var jiraFakeDataRequested = builder.Configuration.GetValue<bool>("Jira:UseFakeData");
var useFakeJiraData = jiraFakeDataRequested && builder.Environment.IsDevelopment();
if (useFakeJiraData)
{
    builder.Services.Replace(ServiceDescriptor.Singleton<IJiraClientFactory, FakeJiraClientFactory>());
}

builder.Services.AddStaticAssetCompression();

var app = builder.Build();
if (useFakeJiraData)
{
    app.Logger.LogWarning("Jira:UseFakeData is on. Jira screens show sample data and no real Jira requests are made.");
}
else if (jiraFakeDataRequested)
{
    app.Logger.LogWarning("Jira:UseFakeData is ignored outside the Development environment.");
}
var localizationOptions = app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value;

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
    await EnsureIdentitySchemaAsync(db);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseRequestLocalization(localizationOptions);
app.UseHttpsRedirection();
// Before the static files, so CSS, JS and SVG go out compressed (StaticAssetDelivery).
app.UseResponseCompression();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(runtimeStorage.UploadsPath),
    RequestPath = "/uploads"
});
app.UseStaticFiles(new StaticFileOptions { OnPrepareResponse = StaticAssetDelivery.SetCacheHeaders });
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
   .WithStaticAssets();

app.Run();

static bool IsAjaxRequest(HttpRequest request)
{
    return string.Equals(request.Headers.XRequestedWith, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
}

static async Task EnsureIdentitySchemaAsync(ApplicationDbContext db)
{
    var connection = db.Database.GetDbConnection();
    var shouldClose = connection.State != System.Data.ConnectionState.Open;

    if (shouldClose)
    {
        await connection.OpenAsync();
    }

    try
    {
        var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await using (var pragmaCommand = connection.CreateCommand())
        {
            pragmaCommand.CommandText = "PRAGMA table_info('AspNetUsers');";

            await using var reader = await pragmaCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var columnName = reader["name"]?.ToString();
                if (!string.IsNullOrWhiteSpace(columnName))
                {
                    existingColumns.Add(columnName);
                }
            }
        }

        var requiredColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["AvatarUrl"] = "TEXT NULL",
            ["AuthSource"] = "TEXT NULL",
            ["JiraBaseUrl"] = "TEXT NULL",
            ["JiraUsername"] = "TEXT NULL",
            ["JiraDisplayName"] = "TEXT NULL"
        };

        foreach (var entry in requiredColumns)
        {
            if (existingColumns.Contains(entry.Key))
            {
                continue;
            }

            await using var alterCommand = connection.CreateCommand();
            alterCommand.CommandText = $"ALTER TABLE AspNetUsers ADD COLUMN {entry.Key} {entry.Value};";
            await alterCommand.ExecuteNonQueryAsync();
        }
    }
    finally
    {
        if (shouldClose)
        {
            await connection.CloseAsync();
        }
    }
}
