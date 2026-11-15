using System.Reflection;

namespace ToolboxWeb.Web.Infrastructure.Hosting;

/// <summary>
/// The running version: <see cref="Version"/> is the release number from the project file
/// (shown to people); <see cref="Build"/> changes with every build, so an open page can tell that a
/// new copy has been deployed (GET /app/version, Content/js/site.js).
/// </summary>
public static class AppVersion
{
    private static readonly Assembly Assembly = typeof(AppVersion).Assembly;

    public static string Version { get; } = ReadVersion();

    public static string Build { get; } = Assembly.ManifestModule.ModuleVersionId.ToString("N")[..12];

    private static string ReadVersion()
    {
        // "1.0.0+3f2c…" when the build adds the source revision; people only need "1.0.0".
        var informational = Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var version = informational ?? Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        var plus = version.IndexOf('+');
        return plus > 0 ? version[..plus] : version;
    }
}
