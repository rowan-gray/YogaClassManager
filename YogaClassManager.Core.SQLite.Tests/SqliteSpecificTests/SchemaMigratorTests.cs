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
        Assert.Equal(2, version);
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

        Assert.Equal([1, 2], versions);
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
        Assert.Equal(2, version);
    }

    [Fact]
    public void LoadEmbeddedMigrations_ParsesVersionNumbersFromResourceNames()
    {
        var migrations = SchemaMigrator.LoadEmbeddedMigrations();

        Assert.Equal(2, migrations.Count);
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
}
