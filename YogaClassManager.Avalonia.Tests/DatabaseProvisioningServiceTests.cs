using Dapper;
using Microsoft.Data.Sqlite;
using YogaClassManager.Avalonia.Services.Database;

namespace YogaClassManager.Avalonia.Tests;

public sealed class DatabaseProvisioningServiceTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "YcmProvisioningTests", Guid.NewGuid().ToString("N"));

    public DatabaseProvisioningServiceTests()
    {
        Directory.CreateDirectory(directory);
    }

    public void Dispose()
    {
        // Pooled connections keep the file handle open on Windows otherwise.
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public async Task ProvisionAsync_CreatesSchemaInitialisedFile_WhenPathDoesNotExist()
    {
        var path = Path.Combine(directory, DatabaseFileDefaults.FileName);

        var result = await new DatabaseProvisioningService().ProvisionAsync(path);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Null(result.BackupPath);
        await using (var store = result.Store!)
        {
            var version = await store.QueryAsync(connection =>
                connection.ExecuteScalarAsync<int>("SELECT MAX(Version) FROM SchemaVersion"));
            Assert.True(version > 0);
        }

        Assert.True(File.Exists(path));
        Assert.True(new FileInfo(path).Length > 0);
    }

    [Fact]
    public async Task ProvisionAsync_ReopensExistingFile_WithoutBackup_WhenAlreadyUpToDate()
    {
        var path = Path.Combine(directory, DatabaseFileDefaults.FileName);
        var service = new DatabaseProvisioningService();

        var first = await service.ProvisionAsync(path);
        Assert.True(first.Success, first.ErrorMessage);
        await first.Store!.DisposeAsync();

        var second = await service.ProvisionAsync(path);
        Assert.True(second.Success, second.ErrorMessage);
        Assert.Null(second.BackupPath);
        await second.Store!.DisposeAsync();
    }

    [Fact]
    public async Task ProvisionAsync_HandlesPathsContainingSpacesAndSemicolons()
    {
        var path = Path.Combine(directory, "My Studio; 2026", DatabaseFileDefaults.FileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var result = await new DatabaseProvisioningService().ProvisionAsync(path);

        Assert.True(result.Success, result.ErrorMessage);
        await result.Store!.DisposeAsync();
        Assert.True(File.Exists(path));
    }

    // An outdated database: a real SQLite file with user data but no SchemaVersion table, so the
    // migrator treats it as version 0 (the shape of a pre-migrator production file) and every
    // migration is pending.
    private static async Task CreateOutdatedDatabaseAsync(string path)
    {
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Pooling = false
        }.ToString());
        await connection.OpenAsync();
        await connection.ExecuteAsync("CREATE TABLE Marker(Value TEXT NOT NULL); INSERT INTO Marker VALUES ('original data');");
    }

    private static async Task<T> QueryFileAsync<T>(string path, string sql)
    {
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString());
        await connection.OpenAsync();
        return await connection.ExecuteScalarAsync<T>(sql) ?? throw new InvalidOperationException($"No result: {sql}");
    }

    private static string BackupPathFor(string databasePath, DateOnly date, int? disambiguator = null)
    {
        var suffix = disambiguator is null ? "" : $".{disambiguator}";
        return $"{databasePath}.{date:yyyyMMdd}{suffix}.backup";
    }

    [Fact]
    public async Task ProvisionAsync_BacksUpThePreMigrationFile_BeforeMigratingAnOutdatedDatabase()
    {
        var path = Path.Combine(directory, DatabaseFileDefaults.FileName);
        await CreateOutdatedDatabaseAsync(path);
        var originalBytes = await File.ReadAllBytesAsync(path);
        var today = DateOnly.FromDateTime(DateTime.Now);

        var result = await new DatabaseProvisioningService().ProvisionAsync(path);

        Assert.True(result.Success, result.ErrorMessage);
        await result.Store!.DisposeAsync();
        Assert.Equal(BackupPathFor(path, today), result.BackupPath);

        // The backup is the file exactly as it was - the pending-migration check mustn't have written to it...
        Assert.Equal(originalBytes, await File.ReadAllBytesAsync(result.BackupPath!));
        Assert.Equal("original data", await QueryFileAsync<string>(result.BackupPath!, "SELECT Value FROM Marker"));

        // ...while the live file was migrated in place, keeping that data too.
        Assert.Equal("original data", await QueryFileAsync<string>(path, "SELECT Value FROM Marker"));
        Assert.True(await QueryFileAsync<long>(path, "SELECT MAX(Version) FROM SchemaVersion") > 0);
    }

    [Fact]
    public async Task ProvisionAsync_NeverOverwritesAnEarlierSameDayBackup()
    {
        var path = Path.Combine(directory, DatabaseFileDefaults.FileName);
        await CreateOutdatedDatabaseAsync(path);
        var today = DateOnly.FromDateTime(DateTime.Now);
        var earlierBackup = BackupPathFor(path, today);
        await File.WriteAllTextAsync(earlierBackup, "earlier backup");
        await File.WriteAllTextAsync(BackupPathFor(path, today, 2), "second earlier backup");

        var result = await new DatabaseProvisioningService().ProvisionAsync(path);

        Assert.True(result.Success, result.ErrorMessage);
        await result.Store!.DisposeAsync();
        Assert.Equal(BackupPathFor(path, today, 3), result.BackupPath);
        Assert.Equal("earlier backup", await File.ReadAllTextAsync(earlierBackup));
        Assert.Equal("second earlier backup", await File.ReadAllTextAsync(BackupPathFor(path, today, 2)));
        Assert.Equal("original data", await QueryFileAsync<string>(result.BackupPath!, "SELECT Value FROM Marker"));
    }

    /// <summary>If the backup can't be written the database must be left untouched (un-migrated), and the
    /// failure reported rather than thrown. A directory squatting on the backup file name makes the copy fail.</summary>
    [Fact]
    public async Task ProvisionAsync_FailsWithoutMigrating_WhenTheBackupCannotBeWritten()
    {
        var path = Path.Combine(directory, DatabaseFileDefaults.FileName);
        await CreateOutdatedDatabaseAsync(path);
        var originalBytes = await File.ReadAllBytesAsync(path);
        Directory.CreateDirectory(BackupPathFor(path, DateOnly.FromDateTime(DateTime.Now)));

        var result = await new DatabaseProvisioningService().ProvisionAsync(path);

        AssertBackupFailure(result);
        Assert.Equal(originalBytes, await File.ReadAllBytesAsync(path));
    }

    /// <summary>A folder the user can read but not write throws UnauthorizedAccessException, not
    /// IOException - it must be reported the same way instead of escaping and crashing startup.</summary>
    [Fact]
    public async Task ProvisionAsync_FailsWithoutMigrating_WhenTheBackupIsDeniedPermission()
    {
        var path = Path.Combine(directory, DatabaseFileDefaults.FileName);
        await CreateOutdatedDatabaseAsync(path);
        var originalBytes = await File.ReadAllBytesAsync(path);
        var service = new DatabaseProvisioningService((_, destination) =>
            throw new UnauthorizedAccessException($"Access to the path '{destination}' is denied."));

        var result = await service.ProvisionAsync(path);

        AssertBackupFailure(result);
        Assert.Contains("is denied", result.ErrorMessage);
        Assert.Equal(originalBytes, await File.ReadAllBytesAsync(path));
    }

    private static void AssertBackupFailure(ProvisionResult result)
    {
        Assert.False(result.Success);
        Assert.Null(result.Store);
        Assert.Contains("backup", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DefaultDatabaseFileName_IsYogaClassManagerDb()
    {
        Assert.Equal("yogaclassmanager.db", DatabaseFileDefaults.FileName);
    }
}
