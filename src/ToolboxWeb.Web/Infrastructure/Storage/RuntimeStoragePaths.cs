using ToolboxWeb.Web.Infrastructure.Hosting;

namespace ToolboxWeb.Web.Infrastructure.Storage;

/// <summary>
/// Where the app keeps its data (Storage:*). Relative paths resolve against the content root on
/// the server and against the data folder (<see cref="AppModeInfo.DataRoot"/>) on a desktop.
/// </summary>
public sealed class RuntimeStoragePaths
{
    private readonly bool _copyLegacyFiles;
    private readonly bool _keysOnly;

    private RuntimeStoragePaths(string databasePath, string uploadsPath, string dataProtectionKeysPath, string backupsPath, string filesTrashPath, bool copyLegacyFiles, bool keysOnly)
    {
        FilesTrashPath = filesTrashPath;
        _keysOnly = keysOnly;
        DatabasePath = databasePath;
        UploadsPath = uploadsPath;
        DataProtectionKeysPath = dataProtectionKeysPath;
        BackupsPath = backupsPath;
        _copyLegacyFiles = copyLegacyFiles;
    }

    public string DatabasePath { get; }

    public string UploadsPath { get; }

    public string DataProtectionKeysPath { get; }

    /// <summary>Database copies taken before migrations (<see cref="DatabaseBackup"/>). Default: "backups" next to the database.</summary>
    public string BackupsPath { get; }

    /// <summary>
    /// Toolkit's trash for "Files &amp; folders" (<c>FileTrashService</c>). Default: "files-trash"
    /// next to the database, so it stays outside the site folder whenever the database does.
    /// </summary>
    public string FilesTrashPath { get; }

    public static RuntimeStoragePaths Create(IConfiguration configuration, AppModeInfo mode)
    {
        var root = mode.DataRoot;
        var databasePath = ResolvePath(root, configuration["Storage:DatabasePath"] ?? "app.db");
        var uploadsPath = ResolvePath(root, configuration["Storage:UploadsPath"] ?? Path.Combine("Content", "uploads"));
        var dataProtectionKeysPath = ResolvePath(root, configuration["Storage:DataProtectionKeysPath"] ?? "DataProtectionKeys");
        var configuredBackups = configuration["Storage:BackupsPath"];
        var backupsPath = string.IsNullOrWhiteSpace(configuredBackups)
            ? Path.Combine(Path.GetDirectoryName(databasePath)!, "backups")
            : ResolvePath(root, configuredBackups);
        var configuredTrash = configuration["Storage:FilesTrashPath"];
        var filesTrashPath = string.IsNullOrWhiteSpace(configuredTrash)
            ? Path.Combine(Path.GetDirectoryName(databasePath)!, "files-trash")
            : ResolvePath(root, configuredTrash);

        // Older server copies kept the database and uploads inside the site folder; a desktop never did.
        // The web copy stores no data: only its Data Protection keys go to disk.
        return new RuntimeStoragePaths(databasePath, uploadsPath, dataProtectionKeysPath, backupsPath, filesTrashPath,
            copyLegacyFiles: mode.Mode == AppMode.Server, keysOnly: mode.IsWeb);
    }

    public void Initialize(string contentRootPath)
    {
        if (_keysOnly)
        {
            Directory.CreateDirectory(DataProtectionKeysPath);
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
        Directory.CreateDirectory(UploadsPath);
        Directory.CreateDirectory(DataProtectionKeysPath);

        if (_copyLegacyFiles)
        {
            CopyLegacyDatabaseIfNeeded(contentRootPath);
            CopyLegacyUploadsIfNeeded(contentRootPath);
        }
    }

    private void CopyLegacyDatabaseIfNeeded(string contentRootPath)
    {
        var legacyDatabasePath = Path.GetFullPath(Path.Combine(contentRootPath, "app.db"));
        if (PathsEqual(legacyDatabasePath, DatabasePath) || File.Exists(DatabasePath) || !File.Exists(legacyDatabasePath))
        {
            return;
        }

        File.Copy(legacyDatabasePath, DatabasePath, overwrite: false);

        foreach (var suffix in new[] { "-wal", "-shm" })
        {
            var legacySidecarPath = legacyDatabasePath + suffix;
            var destinationSidecarPath = DatabasePath + suffix;
            if (File.Exists(legacySidecarPath) && !File.Exists(destinationSidecarPath))
            {
                File.Copy(legacySidecarPath, destinationSidecarPath, overwrite: false);
            }
        }
    }

    private void CopyLegacyUploadsIfNeeded(string contentRootPath)
    {
        var legacyUploadsPath = Path.GetFullPath(Path.Combine(contentRootPath, "Content", "uploads"));
        if (PathsEqual(legacyUploadsPath, UploadsPath) || !Directory.Exists(legacyUploadsPath))
        {
            return;
        }

        foreach (var sourceFile in Directory.EnumerateFiles(legacyUploadsPath, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(legacyUploadsPath, sourceFile);
            var destinationFile = Path.Combine(UploadsPath, relativePath);
            if (File.Exists(destinationFile))
            {
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)!);
            File.Copy(sourceFile, destinationFile, overwrite: false);
        }
    }

    private static string ResolvePath(string basePath, string configuredPath)
    {
        var trimmedPath = configuredPath.Trim();
        if (string.IsNullOrWhiteSpace(trimmedPath))
        {
            throw new InvalidOperationException("Runtime storage path must not be empty.");
        }

        return Path.GetFullPath(Path.IsPathRooted(trimmedPath)
            ? trimmedPath
            : Path.Combine(basePath, trimmedPath));
    }

    private static bool PathsEqual(string firstPath, string secondPath)
    {
        return string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(firstPath)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(secondPath)),
            StringComparison.OrdinalIgnoreCase);
    }
}
