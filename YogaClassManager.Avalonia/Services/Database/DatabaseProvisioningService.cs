using Microsoft.Data.Sqlite;
using YogaClassManager.Core.SQLite.Data;
using YogaClassManager.Core.SQLite.Schema;

namespace YogaClassManager.Avalonia.Services.Database;

public sealed class DatabaseProvisioningService : IDatabaseProvisioningService
{
    private readonly Action<string, string> copyFile;

    /// <param name="copyFile">Copies (source, destination) without overwriting; defaults to File.Copy.
    /// Replaceable so tests can simulate a backup failure (e.g. a permissions error) that's impractical
    /// to produce with a real file system.</param>
    public DatabaseProvisioningService(Action<string, string>? copyFile = null)
    {
        this.copyFile = copyFile ?? ((source, destination) => File.Copy(source, destination, overwrite: false));
    }

    public async Task<ProvisionResult> ProvisionAsync(string path, CancellationToken cancellationToken = default)
    {
        string? backupPath = null;
        var connectionString = ToConnectionString(path);

        if (File.Exists(path))
        {
            bool hasPending;
            try
            {
                hasPending = await SchemaMigrator.HasPendingMigrationsAsync(connectionString, cancellationToken);
            }
            catch (Exception ex)
            {
                return ProvisionResult.Failure($"Could not read database file: {ex.Message}");
            }

            if (hasPending)
            {
                backupPath = BackupNaming.ComputeBackupPath(path, DateOnly.FromDateTime(DateTime.Now));
                try
                {
                    copyFile(path, backupPath);
                }
                // UnauthorizedAccessException: e.g. the database lives in a folder the user can read but not write.
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    return ProvisionResult.Failure($"Could not create backup before migrating: {ex.Message}");
                }
            }
        }

        try
        {
            var store = await SqliteDataStore.OpenAsync(connectionString, cancellationToken);
            return ProvisionResult.Ok(store, backupPath);
        }
        catch (Exception ex)
        {
            return ProvisionResult.Failure($"Could not open database file: {ex.Message}");
        }
    }

    /// <summary>SqliteDataStore/SchemaMigrator take a connection string, not a file path - a bare path
    /// fails to parse as one, so no file ever gets created. Pooling is disabled so closing a connection
    /// actually releases the file (and checkpoints its WAL) before a backup copy or a database swap.</summary>
    private static string ToConnectionString(string path)
    {
        return new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(path),
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString();
    }
}

/// <summary>Computes the backup file path per the confirmed convention: full original filename
/// (including its own extension) + "." + yyyyMMdd + ".backup" - e.g. "yogaclassmanager.db" ->
/// "yogaclassmanager.db.20260805.backup". Never overwrites an earlier same-day backup; a second
/// pending-migration launch on the same day gets a numeric disambiguator instead
/// ("yogaclassmanager.db.20260805.2.backup").</summary>
internal static class BackupNaming
{
    internal static string ComputeBackupPath(string databasePath, DateOnly date)
    {
        var directory = Path.GetDirectoryName(databasePath)!;
        var fileName = Path.GetFileName(databasePath);
        var stamp = date.ToString("yyyyMMdd");

        var candidate = Path.Combine(directory, $"{fileName}.{stamp}.backup");
        if (!File.Exists(candidate))
            return candidate;

        for (var n = 2; ; n++)
        {
            var alternative = Path.Combine(directory, $"{fileName}.{stamp}.{n}.backup");
            if (!File.Exists(alternative))
                return alternative;
        }
    }
}
