using Dapper;
using Microsoft.Data.Sqlite;
using YogaClassManager.Core.SQLite.Data;
using YogaClassManager.Core.SQLite.Schema;

namespace YogaClassManager.Core.SQLite.Tests.SqliteSpecificTests;

public class SchemaMigratorTests
{
    [Fact]
    public async Task EnsureUpToDateAsync_CreatesFullSchema_OnFreshEmptyDatabase()
    {
        await using var store = await SqliteTestDatabaseFactory.CreateAsync();

        var names = (await store.QueryAsync(connection =>
                connection.QueryAsync<string>("SELECT name FROM sqlite_master WHERE type IN ('table','view')")))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var expected in new[]
                 {
                     "Person", "Student", "Pass", "CasualPass", "DatedPass", "TermPass", "PassAlterations",
                     "ClassSchedule", "ClassRoll", "ClassStudents", "Term", "TermClasses",
                     "StudentEmergencyContacts", "StudentHealthConcerns", "Modifications",
                     "PassDetails", "PassStatus", "IdentityLinkage", "StudentLastAttendance",
                     "EmergencyContactDetails", "PassUses", "PassesTotalClasses", "TermClassUses",
                     "SchemaVersion"
                 })
            Assert.Contains(expected, names);

        var version = await store.QueryAsync(connection =>
            connection.ExecuteScalarAsync<int>("SELECT MAX(Version) FROM SchemaVersion"));
        Assert.Equal(6, version);
    }

    [Fact]
    public async Task EnsureUpToDateAsync_IsANoOp_WhenAlreadyAtLatestVersion()
    {
        await using var store = await SqliteTestDatabaseFactory.CreateAsync();

        // Run the migrator a second time directly against the already-up-to-date connection.
        await store.QueryAsync(async connection =>
        {
            await SchemaMigrator.EnsureUpToDateAsync(connection);
            return 0;
        });

        var versions = (await store.QueryAsync(connection =>
            connection.QueryAsync<int>("SELECT Version FROM SchemaVersion ORDER BY Version"))).ToList();

        Assert.Equal([1, 2, 3, 4, 5, 6], versions);
    }

    [Fact]
    public async Task EnsureUpToDateAsync_AppliesCleanlyAgainstProductionShapedDatabase_WithoutLosingData()
    {
        // Migration 001 is a byte-for-byte reproduction of the production yogamanager.db.sql schema
        // (with IF NOT EXISTS added to views/triggers) - applying only it, then seeding a row exactly
        // as an already-running production install would already have, models "the migrator runs for
        // the first time against a real existing database" without needing the repo-root .sql file itself.
        var dataSource = $"Data Source=file:{Guid.NewGuid():N}?mode=memory&cache=shared";
        await using var connection = new SqliteConnection(dataSource);
        await connection.OpenAsync();

        var migration001 = SchemaMigrator.LoadEmbeddedMigrations().Single(m => m.Version == 1).Sql;
        await connection.ExecuteAsync(migration001);

        var personId = (int)await connection.ExecuteScalarAsync<long>(
            "INSERT INTO Person(FirstName, PhoneNumber) VALUES ('Existing', '0400000000'); SELECT last_insert_rowid();");

        await SchemaMigrator.EnsureUpToDateAsync(connection);

        var stillThere = await connection.ExecuteScalarAsync<long>(
            "SELECT EXISTS(SELECT 1 FROM Person WHERE PersonId=$id)", new { id = personId });
        Assert.Equal(1, stillThere);

        var hasCasualPassTable = await connection.ExecuteScalarAsync<long>(
            "SELECT EXISTS(SELECT 1 FROM sqlite_master WHERE type='table' AND name='CasualPass')");
        Assert.Equal(1, hasCasualPassTable);

        var version = await connection.ExecuteScalarAsync<int>("SELECT MAX(Version) FROM SchemaVersion");
        Assert.Equal(6, version);
    }

    [Fact]
    public void LoadEmbeddedMigrations_ParsesVersionNumbersFromResourceNames()
    {
        var migrations = SchemaMigrator.LoadEmbeddedMigrations();

        Assert.Equal(6, migrations.Count);
        Assert.Contains(migrations, m => m.Version == 1 && m.Sql.Contains("CREATE TABLE IF NOT EXISTS \"Person\""));
        Assert.Contains(migrations, m => m.Version == 2 && m.Sql.Contains("CREATE TABLE IF NOT EXISTS \"CasualPass\""));
    }

    [Fact]
    public async Task HasPendingMigrationsAsync_ReturnsFalse_OnAFreshlyMigratedDatabase()
    {
        var dataSource = $"Data Source=file:{Guid.NewGuid():N}?mode=memory&cache=shared";
        // A second, independent connection to the same shared-cache in-memory database - kept open for
        // the test's lifetime so the in-memory db itself isn't dropped once OpenAsync's own connection
        // (inside SqliteDataStore) would otherwise be the only thing keeping it alive alongside this one.
        await using var keepAlive = new SqliteConnection(dataSource);
        await keepAlive.OpenAsync();

        await using var store = await SqliteDataStore.OpenAsync(dataSource);

        Assert.False(await SchemaMigrator.HasPendingMigrationsAsync(dataSource));
    }

    /// <summary>The check runs before the pre-migration backup is copied, so it must not write to the
    /// file - not even the SchemaVersion bootstrap table EnsureUpToDateAsync creates.</summary>
    [Fact]
    public async Task HasPendingMigrationsAsync_ReturnsTrue_WithoutCreatingSchemaVersion_OnAnUnversionedDatabase()
    {
        var dataSource = $"Data Source=file:{Guid.NewGuid():N}?mode=memory&cache=shared";
        await using var connection = new SqliteConnection(dataSource);
        await connection.OpenAsync();
        await connection.ExecuteAsync("CREATE TABLE Marker(Value TEXT)");

        Assert.True(await SchemaMigrator.HasPendingMigrationsAsync(dataSource));

        Assert.Equal(0, await connection.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM sqlite_master WHERE name = 'SchemaVersion'"));
    }

    [Fact]
    public async Task HasPendingMigrationsAsync_ReturnsTrue_WhenOnlyAnOlderMigrationHasBeenApplied()
    {
        var dataSource = $"Data Source=file:{Guid.NewGuid():N}?mode=memory&cache=shared";
        await using var connection = new SqliteConnection(dataSource);
        await connection.OpenAsync();

        // Only apply migration 001, never 002 - simulates a database that's behind the latest schema.
        var migration001 = SchemaMigrator.LoadEmbeddedMigrations().Single(m => m.Version == 1).Sql;
        await connection.ExecuteAsync(migration001);
        await connection.ExecuteAsync(
            """
            CREATE TABLE IF NOT EXISTS "SchemaVersion" ("Version" INTEGER NOT NULL PRIMARY KEY, "AppliedAt" TEXT NOT NULL DEFAULT (datetime('now')));
            INSERT INTO SchemaVersion(Version) VALUES (1);
            """);

        Assert.True(await SchemaMigrator.HasPendingMigrationsAsync(dataSource));
    }

    /// <summary>A real production database that migration 002 was applied to before 002 used the
    /// correct "AlterationCount" name: the column is right, but PassesTotalClasses was rebuilt
    /// against "AlerationCount" and fails on every query.</summary>
    [Fact]
    public async Task EnsureUpToDateAsync_RepairsPassesTotalClasses_BuiltAgainstMisspelledColumn()
    {
        await using var connection = await OpenMigratedTo2Async();
        await SeedDatedPassWithAlterationAsync(connection, classCount: 10, alteration: 2);
        await connection.ExecuteAsync(
            """
            DROP VIEW PassesTotalClasses;
            CREATE VIEW PassesTotalClasses AS
            SELECT Pass.PassId AS PassId, SUM(COALESCE(PassAlterations.AlerationCount, 0)) AS NumberOfClasses
            FROM Pass LEFT JOIN PassAlterations ON Pass.PassId = PassAlterations.PassId GROUP BY Pass.PassId;
            """);

        await SchemaMigrator.EnsureUpToDateAsync(connection);

        Assert.Equal(12, await connection.ExecuteScalarAsync<int>("SELECT NumberOfClasses FROM PassesTotalClasses"));
        Assert.Equal(1, await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM PassStatus"));
    }

    /// <summary>A database this app created from the earlier, misspelled migration 001: the column
    /// itself is "AlerationCount" and must be renamed without losing its rows.</summary>
    [Fact]
    public async Task EnsureUpToDateAsync_RenamesMisspelledAlerationCountColumn_KeepingData()
    {
        await using var connection = await OpenMigratedTo2Async();
        await SeedDatedPassWithAlterationAsync(connection, classCount: 10, alteration: 3);
        // SQLite rewrites dependent views too, reproducing that database's schema exactly.
        await connection.ExecuteAsync("ALTER TABLE PassAlterations RENAME COLUMN AlterationCount TO AlerationCount");

        await SchemaMigrator.EnsureUpToDateAsync(connection);

        var columns = (await connection.QueryAsync<string>("SELECT name FROM pragma_table_info('PassAlterations')")).ToList();
        Assert.Contains("AlterationCount", columns);
        Assert.DoesNotContain("AlerationCount", columns);
        Assert.Equal(3, await connection.ExecuteScalarAsync<int>("SELECT AlterationCount FROM PassAlterations"));
        Assert.Equal(13, await connection.ExecuteScalarAsync<int>("SELECT NumberOfClasses FROM PassesTotalClasses"));
    }

    [Fact]
    public async Task EnsureUpToDateAsync_CreatesAlterationCountColumn_OnFreshDatabase()
    {
        await using var store = await SqliteTestDatabaseFactory.CreateAsync();

        var columns = await store.QueryAsync(connection =>
            connection.QueryAsync<string>("SELECT name FROM pragma_table_info('PassAlterations')"));

        Assert.Contains("AlterationCount", columns);
        Assert.DoesNotContain("AlerationCount", columns);
    }

    private static async Task<SqliteConnection> OpenMigratedTo2Async()
    {
        var connection = new SqliteConnection($"Data Source=file:{Guid.NewGuid():N}?mode=memory&cache=shared");
        await connection.OpenAsync();

        foreach (var migration in SchemaMigrator.LoadEmbeddedMigrations().Where(m => m.Version <= 2).OrderBy(m => m.Version))
            await connection.ExecuteAsync(migration.Sql);

        await connection.ExecuteAsync(
            """
            CREATE TABLE IF NOT EXISTS "SchemaVersion" ("Version" INTEGER NOT NULL PRIMARY KEY, "AppliedAt" TEXT NOT NULL DEFAULT (datetime('now')));
            INSERT INTO SchemaVersion(Version) VALUES (1), (2);
            """);
        return connection;
    }

    private static Task SeedDatedPassWithAlterationAsync(SqliteConnection connection, int classCount, int alteration)
    {
        return connection.ExecuteAsync(
            """
            INSERT INTO Person(PersonId, FirstName, PhoneNumber) VALUES (1, 'Existing', '0400000000');
            INSERT INTO Student(StudentId) VALUES (1);
            INSERT INTO Pass(PassId, StudentId) VALUES (1, 1);
            INSERT INTO DatedPass(PassId, ClassCount, StartDate, EndDate) VALUES (1, $classCount, '2026-01-01', '2026-12-31');
            INSERT INTO PassAlterations(PassId, AlterationCount, AlterationReason) VALUES (1, $alteration, 'test');
            """, new { classCount, alteration });
    }
}
