namespace ToolboxWeb.Web.Infrastructure.Hosting;

/// <summary>How this copy of the app runs (<c>App:Mode</c>). See docs/DEPLOYMENT_MODEL.md.</summary>
public enum AppMode
{
    /// <summary>The shared site (IIS): accounts, sign-in, one database for everyone. The default.</summary>
    Server,

    /// <summary>
    /// One person's computer: the database lives on that computer, its owner is signed in
    /// automatically and only the computer itself can open the app.
    /// </summary>
    Desktop,

    /// <summary>
    /// The browser and phone site (second IIS site): Jira screens and tools that store nothing.
    /// Sign-in is Jira only and no database is written to disk.
    /// </summary>
    Web
}

/// <summary>The run mode and where a desktop copy keeps its data. Registered as a singleton.</summary>
public sealed class AppModeInfo
{
    private AppModeInfo(AppMode mode, string dataRoot)
    {
        Mode = mode;
        DataRoot = dataRoot;
    }

    public AppMode Mode { get; }

    public bool IsDesktop => Mode == AppMode.Desktop;

    public bool IsWeb => Mode == AppMode.Web;

    /// <summary>
    /// The folder relative storage paths resolve against: the content root on the server,
    /// <c>%LOCALAPPDATA%\Toolkit</c> (or <c>Desktop:DataRoot</c>) on a desktop, so updating
    /// the app folder never touches the data.
    /// </summary>
    public string DataRoot { get; }

    public static AppModeInfo Create(IConfiguration configuration, string contentRootPath)
    {
        var configured = configuration["App:Mode"];
        var mode = AppMode.Server;
        if (!string.IsNullOrWhiteSpace(configured) && !Enum.TryParse(configured.Trim(), ignoreCase: true, out mode))
        {
            throw new InvalidOperationException($"App:Mode '{configured}' is not valid. Use one of: {string.Join(", ", Enum.GetNames<AppMode>())}.");
        }

        if (mode != AppMode.Desktop)
        {
            return new AppModeInfo(mode, Path.GetFullPath(contentRootPath));
        }

        var dataRoot = configuration["Desktop:DataRoot"];
        if (string.IsNullOrWhiteSpace(dataRoot))
        {
            dataRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create),
                "Toolkit");
        }

        return new AppModeInfo(mode, Path.GetFullPath(dataRoot.Trim()));
    }
}
