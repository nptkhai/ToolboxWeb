namespace ToolkitLicense.Web.Infrastructure;

/// <summary>
/// Where the license server keeps its files (Storage:DataRoot, outside the site folder in
/// production): the database, its backups, the Data Protection keys, the signing key and the
/// one-time setup code.
/// </summary>
public sealed class LicenseStorage
{
    public LicenseStorage(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var configured = configuration["Storage:DataRoot"];
        var root = string.IsNullOrWhiteSpace(configured) ? "App_Data" : configured.Trim();
        DataRoot = Path.GetFullPath(Path.IsPathRooted(root) ? root : Path.Combine(environment.ContentRootPath, root));
        Directory.CreateDirectory(DataRoot);
        Directory.CreateDirectory(KeysPath);
        Directory.CreateDirectory(UpdatesPath);
    }

    public string DataRoot { get; }
    public string DatabasePath => Path.Combine(DataRoot, "license.db");
    public string BackupsPath => Path.Combine(DataRoot, "backups");
    public string KeysPath => Path.Combine(DataRoot, "keys");
    public string SigningKeyPath => Path.Combine(DataRoot, "signing-key.pem");
    public string SetupCodePath => Path.Combine(DataRoot, "setup-code.txt");

    /// <summary>The desktop installer and update packages (Velopack output), served read-only at /updates.</summary>
    public string UpdatesPath => Path.Combine(DataRoot, "updates");
}
