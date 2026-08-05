using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models.Classes;
using YogaClassManager.Core.SQLite.Repositories;

namespace YogaClassManager.Core.SQLite.Tests.SqliteSpecificTests;

/// <summary>Proves SqliteDataStore's single-gate design (§4 of the plan: all connection access,
/// reads included, serialized through one SemaphoreSlim) actually serializes correctly under real
/// concurrent load - not just in single-threaded test bodies.</summary>
public class ConnectionConcurrencyTests
{
    [Fact]
    public async Task ConcurrentReadsAndWrites_ThroughOneSharedStore_DoNotThrowOrCorruptState()
    {
        await using var store = await SqliteTestDatabaseFactory.CreateAsync();
        var repo = new SqliteClassScheduleRepository(store);

        var writeTasks = Enumerable.Range(0, 20)
            .Select(i => repo.AddAsync(new ClassSchedule(0, DayOfWeek.Monday, new TimeOnly(0, i), false)))
            .ToArray();

        var readTasks = Enumerable.Range(0, 20)
            .Select(_ => repo.Query(new ClassScheduleFilter()).LoadMultiple())
            .ToArray();

        await Task.WhenAll(writeTasks.Cast<Task>().Concat(readTasks));

        var ids = await Task.WhenAll(writeTasks);
        Assert.Equal(20, ids.Distinct().Count());

        var finalCount = (await repo.Query(new ClassScheduleFilter()).LoadMultiple()).Count;
        Assert.Equal(20, finalCount);
    }
}
