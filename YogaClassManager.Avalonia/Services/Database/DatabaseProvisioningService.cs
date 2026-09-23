using YogaClassManager.Core.SQLite.Data;
using YogaClassManager.Core.SQLite.Schema;

namespace YogaClassManager.Avalonia.Services.Database;

public sealed class DatabaseProvisioningService : IDatabaseProvisioningService
{
    public async Task<ProvisionResult> ProvisionAsync(string path, CancellationToken cancellationToken = default)
    {
        string? backupPath = null;

        if (File.Exists(path))
        {
            bool hasPending;
            try
            {
                hasPending = await SchemaMigrator.HasPendingMigrationsAsync(path, cancellationToken);
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
                    File.Copy(path, backupPath, overwrite: false);
                }
                catch (IOException ex)
                {
                    return ProvisionResult.Failure($"Could not create backup before migrating: {ex.Message}");
                }
            }
        }

        try
        {
            var store = await SqliteDataStore.OpenAsync(path, cancellationToken);
            return ProvisionResult.Ok(store, backupPath);
        }
        catch (Exception ex)
        {
            return ProvisionResult.Failure($"Could not open database file: {ex.Message}");
        }
    }
}

/// <summary>Computes the backup file path per the confirmed convention: full original filename
/// (including its own extension) + "." + yyyyMMdd + ".backup" - e.g. "yogamanager.db" ->
/// "yogamanager.db.20260805.backup". Never overwrites an earlier same-day backup; a second
/// pending-migration launch on the same day gets a numeric disambiguator instead
/// ("yogamanager.db.20260805.2.backup").</summary>
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
