using Dapper;
using Microsoft.Data.Sqlite;
using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models.Passes;
using YogaClassManager.Core.SQLite.Data;
using YogaClassManager.Core.SQLite.Repositories;
using YogaClassManager.Core.SQLite.Schema;

namespace YogaClassManager.Core.SQLite.Tests.SqliteSpecificTests;

/// <summary>
///     The legacy MAUI app stored IsArchived in a ClassSchedule column named "IsActive" (1 = archived,
///     0 = active), so reading it at face value showed every active class in an imported database as
///     archived. Migration 006 renames the column to IsArchived, keeping the values. A save-then-load
///     round trip can't catch an inversion (it passes either way), so these tests seed raw
///     legacy-shaped rows, then migrate.
/// </summary>
public class LegacyClassScheduleArchiveTests
{
    /// <summary>A database as the legacy app leaves it (schema 001 only, no SchemaVersion table) with
    /// one active and one archived class in the legacy encoding, then brought up to date - exactly
    /// what importing an old database does.</summary>
    private static async Task<(SqliteConnection KeepAlive, SqliteDataStore Store)> OpenMigratedLegacyAsync(
        string extraLegacySql = "")
    {
        var dataSource = $"Data Source=file:{Guid.NewGuid():N}?mode=memory&cache=shared";
        var keepAlive = new SqliteConnection(dataSource);
        await keepAlive.OpenAsync();

        await keepAlive.ExecuteAsync(SchemaMigrator.LoadEmbeddedMigrations().Single(m => m.Version == 1).Sql);
        await keepAlive.ExecuteAsync(
            """
            INSERT INTO ClassSchedule(ClassScheduleId, Day, Time, IsActive) VALUES (1, 1, 540, 0); -- active
            INSERT INTO ClassSchedule(ClassScheduleId, Day, Time, IsActive) VALUES (2, 2, 540, 1); -- archived
            """ + "\n" + extraLegacySql);

        // SqliteDataStore.OpenAsync runs the migrator.
        return (keepAlive, await SqliteDataStore.OpenAsync(dataSource));
    }

    [Fact]
    public async Task Migration_RenamesColumnToIsArchived_KeepingLegacyValues()
    {
        var (keepAlive, store) = await OpenMigratedLegacyAsync(
            // A row that fell back on the column's legacy DEFAULT 'True' (a string) - normalised to 1.
            "INSERT INTO ClassSchedule(ClassScheduleId, Day, Time) VALUES (3, 3, 540);");
        await using var _ = keepAlive;
        await using var __ = store;

        var columns = (await store.QueryAsync(connection =>
            connection.QueryAsync<string>("SELECT name FROM pragma_table_info('ClassSchedule')"))).ToList();
        Assert.Contains("IsArchived", columns);
        Assert.DoesNotContain("IsActive", columns);

        var stored = (await store.QueryAsync(connection => connection.QueryAsync<(long Id, long IsArchived)>(
            "SELECT ClassScheduleId, IsArchived FROM ClassSchedule ORDER BY ClassScheduleId"))).ToList();
        Assert.Equal([(1L, 0L), (2L, 1L), (3L, 1L)], stored);
    }

    [Fact]
    public async Task Query_ShowsLegacyActiveClassesAsActive()
    {
        var (keepAlive, store) = await OpenMigratedLegacyAsync();
        await using var _ = keepAlive;
        await using var __ = store;
        var repo = new SqliteClassScheduleRepository(new FixedDataStoreProvider(store));

        var all = await repo.Query(new ClassScheduleFilter { IncludeArchived = true }).LoadMultiple();
        Assert.False(all.Single(s => s.Id == 1).IsArchived);
        Assert.True(all.Single(s => s.Id == 2).IsArchived);

        var unarchivedOnly = await repo.Query(new ClassScheduleFilter()).LoadMultiple();
        Assert.Equal([1], unarchivedOnly.Select(s => s.Id));
    }

    [Fact]
    public async Task ArchiveAndUnarchive_WriteIsArchived()
    {
        var (keepAlive, store) = await OpenMigratedLegacyAsync();
        await using var _ = keepAlive;
        await using var __ = store;
        var repo = new SqliteClassScheduleRepository(new FixedDataStoreProvider(store));

        await repo.ArchiveAsync(1);
        await repo.UnarchiveAsync(2);

        var stored = (await store.QueryAsync(connection => connection.QueryAsync<(long Id, long IsArchived)>(
            "SELECT ClassScheduleId, IsArchived FROM ClassSchedule ORDER BY ClassScheduleId"))).ToList();
        Assert.Equal([(1L, 1L), (2L, 0L)], stored);
    }

    [Fact]
    public async Task TermPass_ReadsItsClassScheduleArchiveStateThroughThePassViews()
    {
        var (keepAlive, store) = await OpenMigratedLegacyAsync(
            """
            INSERT INTO Person(PersonId, FirstName, PhoneNumber) VALUES (1, 'Annie', '0400000000');
            INSERT INTO Student(StudentId) VALUES (1);
            INSERT INTO Term(TermId, TermName, StartDate, EndDate) VALUES (1, 'Term 1', '2026-01-01', '2026-03-31');
            INSERT INTO TermClasses(TermId, ClassId, ClassCount) VALUES (1, 1, 10);
            INSERT INTO Pass(PassId, StudentId) VALUES (1, 1);
            INSERT INTO TermPass(PassId, TermId, ClassId) VALUES (1, 1, 1);
            """);
        await using var _ = keepAlive;
        await using var __ = store;

        var pass = await new SqlitePassRepository(new FixedDataStoreProvider(store))
            .Query(new PassFilter { StudentId = 1, IncludeExpired = true, IncludeDepleted = true })
            .LoadSingle();

        Assert.False(Assert.IsType<TermPass>(pass).TermClassSchedule.ClassSchedule.IsArchived);
    }
}
