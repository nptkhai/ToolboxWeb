using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace ToolboxWeb.Web.Infrastructure.Hosting;

/// <summary>
/// Start-up rules for the desktop copy (App:Mode=Desktop): appsettings.Desktop.json, the content
/// root next to the executable, one running copy per Windows account, a loopback-only address,
/// the browser opened on start, and the pages a desktop has no use for.
/// </summary>
public static class DesktopHost
{
    public const string Switch = "--desktop";
    public const string PingPath = "/desktop/ping";
    public const string PingReply = "toolkit";

    private const string InstanceFileName = "instance.json";
    private const int DefaultPort = 5180;

    // Sign-in pages lead nowhere on a desktop (the owner is signed in automatically)...
    private static readonly string[] RedirectedPaths =
    [
        "/Identity/Account/Login",
        "/Identity/Account/Register",
        "/Identity/Account/Logout",
        "/Identity/Account/LoginWith2fa",
        "/Identity/Account/LoginWithRecoveryCode",
        "/Identity/Account/Lockout"
    ];

    // ...and a desktop owner has no password, email or two-factor sign-in to manage.
    private static readonly string[] HiddenPaths =
    [
        "/Identity/Account/Manage/Email",
        "/Identity/Account/Manage/ChangePassword",
        "/Identity/Account/Manage/SetPassword",
        "/Identity/Account/Manage/TwoFactorAuthentication",
        "/Identity/Account/Manage/EnableAuthenticator",
        "/Identity/Account/Manage/Disable2fa",
        "/Identity/Account/Manage/ResetAuthenticator",
        "/Identity/Account/Manage/GenerateRecoveryCodes",
        "/Identity/Account/Manage/ShowRecoveryCodes"
    ];

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <summary>
    /// True when the app is started as a desktop copy: the <c>--desktop</c> switch (removed from
    /// <paramref name="args"/>, since the command-line configuration cannot read a bare switch),
    /// <c>TOOLKIT_DESKTOP=1</c>, or <c>App__Mode=Desktop</c> in the environment.
    /// </summary>
    public static bool IsRequested(ref string[] args)
    {
        var requested = args.Any(IsSwitch);
        args = args.Where(arg => !IsSwitch(arg)).ToArray();

        var flag = Environment.GetEnvironmentVariable("TOOLKIT_DESKTOP");
        return requested
            || flag is "1" || string.Equals(flag, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Environment.GetEnvironmentVariable("App__Mode"), nameof(AppMode.Desktop), StringComparison.OrdinalIgnoreCase);

        static bool IsSwitch(string arg) => string.Equals(arg, Switch, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A shortcut may start the app from any working folder; the web root then has to come from
    /// the folder of the executable. During development (dotnet run) the project folder is kept.
    /// </summary>
    public static string? ContentRoot()
    {
        var current = Directory.GetCurrentDirectory();
        var appFolder = AppContext.BaseDirectory;
        return !Directory.Exists(Path.Combine(current, "Content")) && Directory.Exists(Path.Combine(appFolder, "Content"))
            ? appFolder
            : null;
    }

    /// <summary>
    /// Adds appsettings.Desktop.json over the base settings, then the environment and the
    /// command line again so they still win, as they do for the other settings files.
    /// </summary>
    public static void AddConfiguration(ConfigurationManager configuration, string[] args)
    {
        configuration.AddJsonFile("appsettings.Desktop.json", optional: false, reloadOnChange: false);
        configuration.AddEnvironmentVariables();
        configuration.AddCommandLine(args);
    }

    /// <summary>
    /// When this Windows account already runs a copy, opens the browser on it and returns true:
    /// the new process then exits instead of starting a second copy on the same data.
    /// </summary>
    public static async Task<bool> HandOverToRunningInstanceAsync(AppModeInfo mode)
    {
        var instance = ReadInstance(mode);
        if (instance is null || !ProcessIsAlive(instance.Pid) || !Uri.TryCreate(instance.Url, UriKind.Absolute, out var url) || !url.IsLoopback)
        {
            return false;
        }

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            var reply = await http.GetStringAsync(new Uri(url, PingPath));
            if (!string.Equals(reply.Trim(), PingReply, StringComparison.Ordinal))
            {
                return false;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return false;
        }

        OpenBrowser(instance.Url, logger: null);
        return true;
    }

    /// <summary>
    /// Listens on 127.0.0.1 only: the configured port (Desktop:Port, 5180), or any free port when
    /// another program holds it. An address given in ASPNETCORE_URLS or Kestrel settings wins.
    /// </summary>
    public static void ConfigureUrls(WebApplicationBuilder builder)
    {
        if (!string.IsNullOrWhiteSpace(builder.Configuration["urls"]) || builder.Configuration.GetSection("Kestrel:Endpoints").Exists())
        {
            return;
        }

        var port = builder.Configuration.GetValue<int?>("Desktop:Port") ?? DefaultPort;
        builder.WebHost.UseUrls($"http://127.0.0.1:{(PortIsFree(port) ? port : 0)}");
    }

    /// <summary>
    /// Desktop request rules, placed before routing: only this computer may connect, and the
    /// sign-in and password pages are left out. Also records the running copy and opens the browser.
    /// </summary>
    public static void UseDesktop(WebApplication app, AppModeInfo mode)
    {
        app.Use(async (context, next) =>
        {
            var remote = context.Connection.RemoteIpAddress;
            if (remote is not null && !IPAddress.IsLoopback(remote))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            var path = context.Request.Path;
            if (RedirectedPaths.Any(p => path.Equals(p, StringComparison.OrdinalIgnoreCase)))
            {
                context.Response.Redirect("/");
                return;
            }

            if (HiddenPaths.Any(p => path.Equals(p, StringComparison.OrdinalIgnoreCase)))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            await next();
        });

        var lifetime = app.Lifetime;
        lifetime.ApplicationStarted.Register(() =>
        {
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses.FirstOrDefault();
            if (address is null)
            {
                return;
            }

            // Kestrel reports 127.0.0.1; the browser gets "localhost" so the sign-in cookie
            // and the browser's saved settings stay on one host name.
            var url = address.Replace("127.0.0.1", "localhost", StringComparison.Ordinal);
            WriteInstance(mode, new InstanceFile(Environment.ProcessId, url), app.Logger);
            app.Logger.LogInformation("Toolkit desktop is running at {Url}; data folder {DataRoot}.", url, mode.DataRoot);
            if (app.Configuration.GetValue("Desktop:OpenBrowser", false))
            {
                OpenBrowser(url, app.Logger);
            }
        });
        lifetime.ApplicationStopping.Register(() => DeleteInstance(mode));
    }

    private static bool PortIsFree(int port)
    {
        try
        {
            var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private static void OpenBrowser(string url, ILogger? logger)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Could not open the browser at {Url}.", url);
        }
    }

    private static bool ProcessIsAlive(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static string InstancePath(AppModeInfo mode) => Path.Combine(mode.DataRoot, InstanceFileName);

    private static InstanceFile? ReadInstance(AppModeInfo mode)
    {
        try
        {
            var path = InstancePath(mode);
            return File.Exists(path) ? JsonSerializer.Deserialize<InstanceFile>(File.ReadAllText(path), Json) : null;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void WriteInstance(AppModeInfo mode, InstanceFile instance, ILogger logger)
    {
        try
        {
            Directory.CreateDirectory(mode.DataRoot);
            File.WriteAllText(InstancePath(mode), JsonSerializer.Serialize(instance, Json));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not record the running copy in {Path}.", InstancePath(mode));
        }
    }

    private static void DeleteInstance(AppModeInfo mode)
    {
        try
        {
            if (ReadInstance(mode)?.Pid == Environment.ProcessId)
            {
                File.Delete(InstancePath(mode));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A stale file is harmless: the next start checks that its process is still alive.
        }
    }

    private sealed record InstanceFile(int Pid, string Url);
}
