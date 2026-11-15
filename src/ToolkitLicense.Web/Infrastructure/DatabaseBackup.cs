using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ToolkitLicense.Web.Data;

namespace ToolkitLicense.Web.Infrastructure;

/// <summary>
/// Copies the database aside before new migrations change it (the same rule as ToolboxWeb's
/// DatabaseBackup); the five newest copies are kept. A failed copy stops the start-up.
/// </summary>
public static class DatabaseBackup
{
    private const int Kept = 5;

    public static async Task BeforeMigrationsAsync(LicenseDbContext db, LicenseStorage storage, ILogger logger)
    {
        if (!File.Exists(storage.DatabasePath) || new FileInfo(storage.DatabasePath).Length == 0)
        {
            return;
        }

        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
        if (pending.Count == 0 || !(await db.Database.GetAppliedMigrationsAsync()).Any())
        {
            return;
        }

        Directory.CreateDirectory(storage.BackupsPath);
        var next = pending[0][(pending[0].IndexOf('_') + 1)..];
        var target = Path.Combine(storage.BackupsPath, $"license-{DateTime.Now:yyyyMMdd-HHmmss}-before-{next}.db");
        await using (var source = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = storage.DatabasePath, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString()))
        await using (var destination = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = target, Pooling = false }.ToString()))
        {
            await source.OpenAsync();
            await destination.OpenAsync();
            source.BackupDatabase(destination);
        }

        logger.LogWarning("Backed up the license database to {Backup} before {Count} migration(s).", target, pending.Count);
        foreach (var old in Directory.EnumerateFiles(storage.BackupsPath, "license-*.db").OrderByDescending(Path.GetFileName, StringComparer.Ordinal).Skip(Kept))
        {
            File.Delete(old);
        }
    }
}
