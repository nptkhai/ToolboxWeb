using System.Diagnostics;

namespace ToolboxWeb.Web.Infrastructure.Hosting;

/// <summary>
/// Opens the desktop app on screen. Desktop:Window = "App" (the default) uses Microsoft Edge or
/// Google Chrome in app mode (--app): its own window without an address bar, with its own profile
/// in the data folder (window/). Without either browser, or with Desktop:Window = "Browser", the
/// default browser opens a tab. Closing the window leaves the app running; "Quit Toolkit" stops it.
/// </summary>
public static class AppWindow
{
    public static void Open(string url, string dataRoot, IConfiguration configuration, ILogger? logger)
    {
        var wantApp = !string.Equals(configuration["Desktop:Window"], "Browser", StringComparison.OrdinalIgnoreCase);
        var browser = wantApp ? FindAppBrowser(configuration["Desktop:BrowserPath"]) : null;
        try
        {
            if (browser is not null)
            {
                var start = new ProcessStartInfo(browser) { UseShellExecute = false };
                start.ArgumentList.Add("--app=" + url);
                start.ArgumentList.Add("--user-data-dir=" + Path.Combine(dataRoot, "window"));
                start.ArgumentList.Add("--window-size=1320,880");
                start.ArgumentList.Add("--no-first-run");
                start.ArgumentList.Add("--no-default-browser-check");
                Process.Start(start);
                return;
            }

            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Could not open the app window at {Url}.", url);
        }
    }

    /// <summary>Edge first (it comes with Windows), then Chrome; a configured path wins.</summary>
    public static string? FindAppBrowser(string? configured)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return File.Exists(configured) ? configured : null;
        }

        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        }.Where(root => !string.IsNullOrEmpty(root));
        var candidates = new[] { @"Microsoft\Edge\Application\msedge.exe", @"Google\Chrome\Application\chrome.exe" };
        return candidates
            .SelectMany(candidate => roots.Select(root => Path.Combine(root, candidate)))
            .FirstOrDefault(File.Exists);
    }
}
