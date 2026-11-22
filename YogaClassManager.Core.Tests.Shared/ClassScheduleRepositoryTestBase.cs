using YogaClassManager.Core.Data;
using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models.Classes;
using YogaClassManager.Core.Models.People;

namespace YogaClassManager.Core.Tests.Shared;

public abstract class ClassScheduleRepositoryTestBase<TFactory> : IAsyncLifetime
    where TFactory : IRepositoryTestFactory, new()
{
    protected TFactory Factory { get; private set; } = default!;

    public virtual async Task InitializeAsync()
    {
        Factory = new TFactory();
        await Factory.InitializeAsync();
    }

    public virtual Task DisposeAsync()
    {
        return Factory.DisposeAsync().AsTask();
    }

    [Fact]
    public async Task AddAsync_Throws_OnDayTimeCollision()
    {
        await Factory.ClassSchedules.AddAsync(new ClassSchedule(0, DayOfWeek.Monday, new TimeOnly(9, 0), false));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Factory.ClassSchedules.AddAsync(new ClassSchedule(0, DayOfWeek.Monday, new TimeOnly(9, 0), false)));
    }

    private async Task<ClassSchedule> ReloadScheduleAsync(int id)
    {
        return (await Factory.ClassSchedules.Query(new ClassScheduleFilter { Id = (uint)id, IncludeArchived = true }).LoadSingle())!;
    }

    [Fact]
    public async Task UpdateAsync_MovesTheClassToANewDayAndTime()
    {
        var id = await Factory.ClassSchedules.AddAsync(new ClassSchedule(0, DayOfWeek.Monday, new TimeOnly(9, 0), false));

        await Factory.ClassSchedules.UpdateAsync(new ClassSchedule(id, DayOfWeek.Thursday, new TimeOnly(18, 30), false));

        var reloaded = await ReloadScheduleAsync(id);
        Assert.Equal((DayOfWeek.Thursday, new TimeOnly(18, 30)), (reloaded.Day, reloaded.Time));
    }

    /// <summary>The collision check must exclude the class being saved, or re-saving an unchanged class fails.</summary>
    [Fact]
    public async Task UpdateAsync_AllowsResavingAClassInItsOwnSlot()
    {
        var id = await Factory.ClassSchedules.AddAsync(new ClassSchedule(0, DayOfWeek.Monday, new TimeOnly(9, 0), false));

        await Factory.ClassSchedules.UpdateAsync(new ClassSchedule(id, DayOfWeek.Monday, new TimeOnly(9, 0), false));

        Assert.Equal(DayOfWeek.Monday, (await ReloadScheduleAsync(id)).Day);
    }

    [Fact]
    public async Task UpdateAsync_Throws_WhenMovingIntoAnotherClassesSlot_AndLeavesTheClassUnchanged()
    {
        await Factory.ClassSchedules.AddAsync(new ClassSchedule(0, DayOfWeek.Monday, new TimeOnly(9, 0), false));
        var id = await Factory.ClassSchedules.AddAsync(new ClassSchedule(0, DayOfWeek.Tuesday, new TimeOnly(9, 0), false));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Factory.ClassSchedules.UpdateAsync(new ClassSchedule(id, DayOfWeek.Monday, new TimeOnly(9, 0), false)));

        Assert.Equal(DayOfWeek.Tuesday, (await ReloadScheduleAsync(id)).Day);
    }

    [Fact]
    public async Task TryDeleteAsync_RefusesWhenReferencedByARoll_AllowsOtherwise()
    {
        var referencedId = await Factory.ClassSchedules.AddAsync(
            new ClassSchedule(0, DayOfWeek.Monday, new TimeOnly(9, 0), false));
        var unreferencedId = await Factory.ClassSchedules.AddAsync(
            new ClassSchedule(0, DayOfWeek.Tuesday, new TimeOnly(9, 0), false));

        var studentId = await Factory.Students.AddAsync(
            new Student(0, "Alice", "Johnson", null, "alice@example.com", true));

        var referenced = await Factory.ClassSchedules.Query(new ClassScheduleFilter { Id = (uint)referencedId })
            .LoadSingle();
        var student = await Factory.Students.Query(new StudentFilter { Id = (uint)studentId }).LoadSingle();

        await Factory.ClassRolls.AddAsync(new ClassRoll(0, DateOnly.FromDateTime(DateTime.Now), referenced!,
            [new ClassRollEntry(student!, null)]));

        Assert.False(await Factory.ClassSchedules.TryDeleteAsync(referencedId));
        Assert.NotNull(await Factory.ClassSchedules.Query(new ClassScheduleFilter { Id = (uint)referencedId }).LoadSingle());

        Assert.True(await Factory.ClassSchedules.TryDeleteAsync(unreferencedId));
        Assert.Null(await Factory.ClassSchedules.Query(new ClassScheduleFilter { Id = (uint)unreferencedId }).LoadSingle());
    }

    [Fact]
    public async Task Query_ExcludesArchivedByDefault_IncludesWhenRequested()
    {
        await Factory.ClassSchedules.AddAsync(new ClassSchedule(0, DayOfWeek.Monday, new TimeOnly(9, 0), false));
        await Factory.ClassSchedules.AddAsync(new ClassSchedule(0, DayOfWeek.Saturday, new TimeOnly(10, 0), true));

        var defaultResults = await Factory.ClassSchedules.Query(new ClassScheduleFilter()).LoadMultiple();
        Assert.Single(defaultResults);

        var withArchived = await Factory.ClassSchedules.Query(new ClassScheduleFilter { IncludeArchived = true })
            .LoadMultiple();
        Assert.Equal(2, withArchived.Count);
    }

    /// <summary>Day sorts by week order (Sunday first), not alphabetically, and a 7am class sorts before
    /// a 9:30 one.</summary>
    [Theory]
    [InlineData(ClassScheduleSortOptions.Id, Order.Ascending)]
    [InlineData(ClassScheduleSortOptions.Id, Order.Descending)]
    [InlineData(ClassScheduleSortOptions.Day, Order.Ascending)]
    [InlineData(ClassScheduleSortOptions.Day, Order.Descending)]
    [InlineData(ClassScheduleSortOptions.Time, Order.Ascending)]
    [InlineData(ClassScheduleSortOptions.Time, Order.Descending)]
    public async Task Query_SortsByEveryKeyInBothDirections(ClassScheduleSortOptions key, Order order)
    {
        int[] ids =
        [
            await Factory.ClassSchedules.AddAsync(new ClassSchedule(0, DayOfWeek.Wednesday, new TimeOnly(18, 0), false)),
            await Factory.ClassSchedules.AddAsync(new ClassSchedule(0, DayOfWeek.Monday, new TimeOnly(12, 0), false)),
            await Factory.ClassSchedules.AddAsync(new ClassSchedule(0, DayOfWeek.Friday, new TimeOnly(7, 0), false)),
            await Factory.ClassSchedules.AddAsync(new ClassSchedule(0, DayOfWeek.Tuesday, new TimeOnly(9, 30), false))
        ];
        int[] ascending = key switch
        {
            ClassScheduleSortOptions.Day => [1, 3, 0, 2],
            ClassScheduleSortOptions.Time => [2, 3, 1, 0],
            _ => [0, 1, 2, 3]
        };

        var results = await Factory.ClassSchedules.Query(new ClassScheduleFilter
        {
            SortBy = new KeyValuePair<ClassScheduleSortOptions, Order>(key, order)
        }).LoadMultiple();

        var expected = ascending.Select(i => ids[i]);
        Assert.Equal(order == Order.Ascending ? expected : expected.Reverse(), results.Select(s => s.Id));
    }
}
