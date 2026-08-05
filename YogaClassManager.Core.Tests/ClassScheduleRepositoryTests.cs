using YogaClassManager.Core.Dummy;
using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models.Classes;
using YogaClassManager.Core.Models.People;

namespace YogaClassManager.Core.Tests;

public class ClassScheduleRepositoryTests
{
    private static (InMemoryDataStore store, InMemoryClassScheduleRepository repo) CreateRepo()
    {
        var store = new InMemoryDataStore();
        return (store, new InMemoryClassScheduleRepository(store));
    }

    [Fact]
    public async Task AddAsync_Throws_OnDayTimeCollision()
    {
        var (_, repo) = CreateRepo();
        await repo.AddAsync(new ClassSchedule(0, DayOfWeek.Monday, new TimeOnly(9, 0), false));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repo.AddAsync(new ClassSchedule(0, DayOfWeek.Monday, new TimeOnly(9, 0), false)));
    }

    [Fact]
    public async Task TryDeleteAsync_RefusesWhenReferencedByARoll_AllowsOtherwise()
    {
        var (store, repo) = CreateRepo();
        var referenced = new ClassSchedule(store.NextId(), DayOfWeek.Monday, new TimeOnly(9, 0), false);
        var unreferenced = new ClassSchedule(store.NextId(), DayOfWeek.Tuesday, new TimeOnly(9, 0), false);
        store.ClassSchedules[referenced.Id] = referenced;
        store.ClassSchedules[unreferenced.Id] = unreferenced;

        var student = new Student(store.NextId(), "Alice", "Johnson", null, null, true);
        var roll = new ClassRoll(store.NextId(), DateOnly.FromDateTime(DateTime.Now), referenced,
            [new ClassRollEntry(student, null)]);
        store.ClassRolls[roll.Id] = roll;

        Assert.False(await repo.TryDeleteAsync(referenced.Id));
        Assert.True(store.ClassSchedules.ContainsKey(referenced.Id));

        Assert.True(await repo.TryDeleteAsync(unreferenced.Id));
        Assert.False(store.ClassSchedules.ContainsKey(unreferenced.Id));
    }

    [Fact]
    public async Task Query_ExcludesArchivedByDefault_IncludesWhenRequested()
    {
        var (store, repo) = CreateRepo();
        var active = new ClassSchedule(store.NextId(), DayOfWeek.Monday, new TimeOnly(9, 0), false);
        var archived = new ClassSchedule(store.NextId(), DayOfWeek.Saturday, new TimeOnly(10, 0), true);
        store.ClassSchedules[active.Id] = active;
        store.ClassSchedules[archived.Id] = archived;

        var defaultResults = await repo.Query(new ClassScheduleFilter()).LoadMultiple();
        Assert.Single(defaultResults);

        var withArchived = await repo.Query(new ClassScheduleFilter { IncludeArchived = true }).LoadMultiple();
        Assert.Equal(2, withArchived.Count);
    }
}
