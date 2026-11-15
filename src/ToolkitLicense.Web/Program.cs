using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using ToolboxWeb.Web.Localization;
using ToolkitLicense.Web.Data;
using ToolkitLicense.Web.Domain;
using ToolkitLicense.Web.Infrastructure;
using ToolkitLicense.Web.Services;

// Toolkit License: the owner's admin app on the main server (docs/LICENSE_SERVER.md).
// The web root is the Content folder copied next to the build, which also holds the design
// files shared with ToolboxWeb.Web (linked in the csproj).
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    WebRootPath = Path.Combine(AppContext.BaseDirectory, "Content")
});

var storage = new LicenseStorage(builder.Configuration, builder.Environment);
builder.Services.AddSingleton(storage);
builder.Services.AddSingleton<SigningKey>();
builder.Services.AddSingleton<SetupCode>();
Display.Configure(builder.Configuration["App:TimeZone"]);

builder.Services.AddDbContext<LicenseDbContext>(options =>
    options.UseSqlite($"Data Source={storage.DatabasePath}"));

builder.Services.AddDataProtection()
    .SetApplicationName("ToolkitLicense")
    .PersistKeysToFileSystem(new DirectoryInfo(storage.KeysPath));

builder.Services
    .AddIdentityCore<AdminUser>(options =>
    {
        options.Password.RequiredLength = 10;
        options.Password.RequireNonAlphanumeric = false;
        options.User.RequireUniqueEmail = false;
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<LicenseDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies(cookies =>
{
    cookies.ApplicationCookie?.Configure(options =>
    {
        options.Cookie.Name = "toolkit.license.auth";
        options.Cookie.SameSite = SameSiteMode.Strict;
        // HTTPS only outside development (the site runs behind IIS with a certificate).
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        options.LoginPath = "/account/login";
        options.LogoutPath = "/account/logout";
        options.AccessDeniedPath = "/account/login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });
    cookies.TwoFactorUserIdCookie?.Configure(options =>
    {
        options.Cookie.Name = "toolkit.license.2fa";
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    });
});
builder.Services.AddAuthorization();

builder.Services.AddLocalization();
builder.Services.AddSingleton<JsonLocalizationStore>();
builder.Services.Replace(ServiceDescriptor.Singleton<IStringLocalizerFactory, JsonStringLocalizerFactory>());
var cultures = new[] { new CultureInfo("vi-VN"), new CultureInfo("en-US") };
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture("vi-VN");
    options.SupportedCultures = cultures;
    options.SupportedUICultures = cultures;
    options.RequestCultureProviders = [new CookieRequestCultureProvider(), new AcceptLanguageHeaderRequestCultureProvider()];
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuditLog, AuditLog>();
builder.Services.AddScoped<LicenseService>();
builder.Services.AddScoped<ReleaseService>();
builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<UpdateFiles>();

// Every page needs the signed-in administrator unless it says otherwise ([AllowAnonymous]:
// setup, sign-in, the device API). Every form post carries the antiforgery token.
builder.Services.AddControllersWithViews(options =>
    {
        options.Filters.Add(new AuthorizeFilter(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()));
        options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    })
    .AddViewLocalization();

// The device API and the sign-in pages are open to the internet: limit how often one address may call them.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(RateLimits.Api, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1) }));
    options.AddPolicy(RateLimits.SignIn, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LicenseDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    await DatabaseBackup.BeforeMigrationsAsync(db, storage, logger);
    await db.Database.MigrateAsync();
    _ = scope.ServiceProvider.GetRequiredService<SigningKey>();
    if (!await db.Users.AnyAsync())
    {
        scope.ServiceProvider.GetRequiredService<SetupCode>().EnsureCreated(logger);
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/home/error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers.XContentTypeOptions = "nosniff";
    headers.XFrameOptions = "DENY";
    headers["Referrer-Policy"] = "no-referrer";
    await next();
});
app.UseStaticFiles();
// The desktop installer and update packages (docs/RELEASE.md): public and read-only, no folder listing.
app.UseStaticFiles(UpdateFiles.Options(storage));
app.UseRouting();
app.UseRequestLocalization();
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<RequireTwoFactorMiddleware>();
app.UseAuthorization();

app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");

app.Run();

/// <summary>Rate-limit policy names.</summary>
public static class RateLimits
{
    public const string Api = "api";
    public const string SignIn = "sign-in";
}
