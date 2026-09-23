using YogaClassManager.Core.SQLite.Data;

namespace YogaClassManager.Avalonia.Services.Database;

/// <summary>Result of <see cref="IDatabaseProvisioningService.ProvisionAsync" />. <see cref="BackupPath" />
/// is non-null only when a pre-migration backup was actually written (used purely for a confirmation
/// toast - never required by callers for correctness).</summary>
public sealed record ProvisionResult(bool Success, SqliteDataStore? Store, string? BackupPath, string? ErrorMessage)
{
    public static ProvisionResult Ok(SqliteDataStore store, string? backupPath)
    {
        return new ProvisionResult(true, store, backupPath, null);
    }

    public static ProvisionResult Failure(string errorMessage)
    {
        return new ProvisionResult(false, null, null, errorMessage);
    }
}
