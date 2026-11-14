using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ToolboxWeb.Web.Data;

namespace ToolboxWeb.Web.Infrastructure.Storage;

/// <summary>
/// Copies the SQLite database aside before new migrations change it, in every mode. Only when
/// the database already exists and has migrations to apply; the newest copies are kept.
/// </summary>
public static class DatabaseBackup
{
    public const int Kept = 5;
    private const string Prefix = "app-";

    /// <summary>Returns the backup file, or null when nothing needed a backup. Throws if the copy fails.</summary>
    public static async Task<string?> BeforeMigrationsAsync(ApplicationDbContext db, RuntimeStoragePaths storage, ILogger logger)
    {
        if (!File.Exists(storage.DatabasePath) || new FileInfo(storage.DatabasePath).Length == 0)
        {
            return null;
        }

        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
        if (pending.Count == 0 || !(await db.Database.GetAppliedMigrationsAsync()).Any())
        {
            return null;
        }

        Directory.CreateDirectory(storage.BackupsPath);
        // "20261024093000_AddChecklistPriority" -> "AddChecklistPriority"
        var next = pending[0][(pending[0].IndexOf('_') + 1)..];
        var target = Path.Combine(storage.BackupsPath, $"{Prefix}{DateTime.Now:yyyyMMdd-HHmmss}-before-{next}.db");

        // The online backup API copies a consistent database even with a write-ahead log open.
        await using (var source = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = storage.DatabasePath, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString()))
        await using (var destination = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = target, Pooling = false }.ToString()))
        {
            await source.OpenAsync();
            await destination.OpenAsync();
            source.BackupDatabase(destination);
        }

        logger.LogWarning("Backed up the database to {Backup} before applying {Count} migration(s): {Migrations}.", target, pending.Count, string.Join(", ", pending));
        Prune(storage.BackupsPath, logger);
        return target;
    }

    private static void Prune(string folder, ILogger logger)
    {
        // File names start with the time, so name order is age order.
        foreach (var old in Directory.EnumerateFiles(folder, Prefix + "*.db").OrderByDescending(Path.GetFileName, StringComparer.Ordinal).Skip(Kept))
        {
            try
            {
                File.Delete(old);
            }
            catch (IOException ex)
            {
                logger.LogWarning(ex, "Could not delete the old backup {Backup}.", old);
            }
        }
    }
}
