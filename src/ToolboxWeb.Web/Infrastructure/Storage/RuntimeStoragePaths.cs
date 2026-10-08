namespace ToolboxWeb.Web.Infrastructure.Storage;

public sealed class RuntimeStoragePaths
{
    private RuntimeStoragePaths(string databasePath, string uploadsPath, string dataProtectionKeysPath)
    {
        DatabasePath = databasePath;
        UploadsPath = uploadsPath;
        DataProtectionKeysPath = dataProtectionKeysPath;
    }

    public string DatabasePath { get; }

    public string UploadsPath { get; }

    public string DataProtectionKeysPath { get; }

    public static RuntimeStoragePaths Create(IWebHostEnvironment environment, IConfiguration configuration)
    {
        var databasePath = ResolvePath(
            environment.ContentRootPath,
            configuration["Storage:DatabasePath"] ?? "app.db");
        var uploadsPath = ResolvePath(
            environment.ContentRootPath,
            configuration["Storage:UploadsPath"] ?? Path.Combine("Content", "uploads"));
        var dataProtectionKeysPath = ResolvePath(
            environment.ContentRootPath,
            configuration["Storage:DataProtectionKeysPath"] ?? "DataProtectionKeys");

        return new RuntimeStoragePaths(databasePath, uploadsPath, dataProtectionKeysPath);
    }

    public void Initialize(string contentRootPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
        Directory.CreateDirectory(UploadsPath);
        Directory.CreateDirectory(DataProtectionKeysPath);

        CopyLegacyDatabaseIfNeeded(contentRootPath);
        CopyLegacyUploadsIfNeeded(contentRootPath);
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

    private static string ResolvePath(string contentRootPath, string configuredPath)
    {
        var trimmedPath = configuredPath.Trim();
        if (string.IsNullOrWhiteSpace(trimmedPath))
        {
            throw new InvalidOperationException("Runtime storage path must not be empty.");
        }

        return Path.GetFullPath(Path.IsPathRooted(trimmedPath)
            ? trimmedPath
            : Path.Combine(contentRootPath, trimmedPath));
    }

    private static bool PathsEqual(string firstPath, string secondPath)
    {
        return string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(firstPath)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(secondPath)),
            StringComparison.OrdinalIgnoreCase);
    }
}
