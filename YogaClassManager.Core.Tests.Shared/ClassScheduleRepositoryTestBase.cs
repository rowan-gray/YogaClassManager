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
}
