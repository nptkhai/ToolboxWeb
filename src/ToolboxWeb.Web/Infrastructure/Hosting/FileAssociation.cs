using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace ToolboxWeb.Web.Infrastructure.Hosting;

/// <summary>
/// Puts the installed desktop copy in Windows' "Open with" list for Markdown files (.md,
/// .markdown), for the current user only (HKCU\Software\Classes). It never makes Toolkit the
/// default program: the user picks that in Windows. The command starts Toolkit with
/// <c>--open-file "path"</c>, which a running copy receives (DesktopHost) and shows in Files &amp;
/// folders. Registered by the installer hooks in Program.cs and removed on uninstall.
/// </summary>
public static class FileAssociation
{
    public const string OpenFileSwitch = "--open-file";
    private const string ProgId = "ToolkitApp.Markdown";
    private static readonly string[] Extensions = [".md", ".markdown"];

    /// <summary>Adds the install, update and uninstall hooks (Windows only; Velopack runs them).</summary>
    public static Velopack.VelopackApp WithMarkdownOpenWith(this Velopack.VelopackApp app)
    {
        if (OperatingSystem.IsWindows())
        {
            app.OnAfterInstallFastCallback(_ => TryRegister())
                .OnAfterUpdateFastCallback(_ => TryRegister())
                .OnBeforeUninstallFastCallback(_ => TryUnregister());
        }

        return app;
    }

    public static void TryRegister()
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrEmpty(Environment.ProcessPath))
        {
            return;
        }

        try
        {
            Register(Environment.ProcessPath);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            // Installing still succeeds; only "Open with" is missing.
        }
    }

    public static void TryUnregister()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            Unregister();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            // Left behind; it points to a program that is gone and Windows ignores it.
        }
    }

    [SupportedOSPlatform("windows")]
    private static void Register(string exePath)
    {
        using var classes = Registry.CurrentUser.CreateSubKey(@"Software\Classes");
        using (var prog = classes.CreateSubKey(ProgId))
        {
            prog.SetValue(string.Empty, "Markdown (Toolkit)");
            prog.SetValue("FriendlyTypeName", "Markdown (Toolkit)");
            using (var icon = prog.CreateSubKey("DefaultIcon"))
            {
                icon.SetValue(string.Empty, $"\"{exePath}\",0");
            }

            using var command = prog.CreateSubKey(@"shell\open\command");
            command.SetValue(string.Empty, $"\"{exePath}\" --desktop {OpenFileSwitch} \"%1\"");
        }

        foreach (var extension in Extensions)
        {
            using var openWith = classes.CreateSubKey($@"{extension}\OpenWithProgids");
            openWith.SetValue(ProgId, Array.Empty<byte>(), RegistryValueKind.None);
        }

        NotifyShell();
    }

    [SupportedOSPlatform("windows")]
    private static void Unregister()
    {
        using var classes = Registry.CurrentUser.OpenSubKey(@"Software\Classes", writable: true);
        if (classes is null)
        {
            return;
        }

        foreach (var extension in Extensions)
        {
            using var openWith = classes.OpenSubKey($@"{extension}\OpenWithProgids", writable: true);
            openWith?.DeleteValue(ProgId, throwOnMissingValue: false);
        }

        classes.DeleteSubKeyTree(ProgId, throwOnMissingSubKey: false);
        NotifyShell();
    }

    /// <summary>Tells Explorer that file associations changed (SHCNE_ASSOCCHANGED).</summary>
    [SupportedOSPlatform("windows")]
    private static void NotifyShell() => SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);
}
