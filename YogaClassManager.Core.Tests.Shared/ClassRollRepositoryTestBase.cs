using YogaClassManager.Core.Data;
using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models.Classes;
using YogaClassManager.Core.Models.People;

namespace YogaClassManager.Core.Tests.Shared;

public abstract class ClassRollRepositoryTestBase<TFactory> : IAsyncLifetime
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

    private async Task<(ClassSchedule mon, ClassSchedule wed, Student student)> CreateFixtureAsync()
    {
        var monId = await Factory.ClassSchedules.AddAsync(new ClassSchedule(0, DayOfWeek.Monday, new TimeOnly(9, 0), false));
        var wedId = await Factory.ClassSchedules.AddAsync(new ClassSchedule(0, DayOfWeek.Wednesday, new TimeOnly(18, 0), false));
        var mon = (await Factory.ClassSchedules.Query(new ClassScheduleFilter { Id = (uint)monId }).LoadSingle())!;
        var wed = (await Factory.ClassSchedules.Query(new ClassScheduleFilter { Id = (uint)wedId }).LoadSingle())!;

        var studentId = await Factory.Students.AddAsync(new Student(0, "Alice", "Johnson", null, "alice@example.com", true));
        var student = (await Factory.Students.Query(new StudentFilter { Id = (uint)studentId }).LoadSingle())!;

        var today = DateOnly.FromDateTime(DateTime.Now);
        await Factory.ClassRolls.AddAsync(new ClassRoll(0, today.AddDays(-14), mon, [new ClassRollEntry(student, null)]));
        await Factory.ClassRolls.AddAsync(new ClassRoll(0, today.AddDays(-1), wed, [new ClassRollEntry(student, null)]));

        return (mon, wed, student);
    }

    [Fact]
    public async Task Query_FiltersByDayOfWeekAndDateRange()
    {
        var (mon, _, _) = await CreateFixtureAsync();

        var mondayOnly = await Factory.ClassRolls.Query(new ClassRollFilter { DayOfWeek = DayOfWeek.Monday }).LoadMultiple();
        Assert.Single(mondayOnly);
        Assert.Equal(mon.Id, mondayOnly[0].ClassSchedule.Id);

        var today = DateOnly.FromDateTime(DateTime.Now);
        var lastThreeDays = await Factory.ClassRolls.Query(new ClassRollFilter { DateFrom = today.AddDays(-3) }).LoadMultiple();
        Assert.Single(lastThreeDays);
    }

    [Fact]
    public async Task Query_SortsByDateAscendingAndDescending()
    {
        await CreateFixtureAsync();

        var ascending = await Factory.ClassRolls.Query(new ClassRollFilter
        {
            SortBy = new KeyValuePair<ClassRollSortOptions, Order>(ClassRollSortOptions.Date, Order.Ascending)
        }).LoadMultiple();
        Assert.True(ascending[0].Date < ascending[1].Date);

        var descending = await Factory.ClassRolls.Query(new ClassRollFilter
        {
            SortBy = new KeyValuePair<ClassRollSortOptions, Order>(ClassRollSortOptions.Date, Order.Descending)
        }).LoadMultiple();
        Assert.True(descending[0].Date > descending[1].Date);
    }

    [Fact]
    public async Task GetAttendanceHistoryAsync_ReturnsEveryRollTheStudentAttended()
    {
        var (_, _, student) = await CreateFixtureAsync();

        var history = await Factory.ClassRolls.GetAttendanceHistoryAsync(student.Id);

        Assert.Equal(2, history.Count);
        Assert.All(history, r => Assert.Equal(student.Id, r.StudentId));
    }
}
