using YogaClassManager.Core.Data;
using YogaClassManager.Core.Dummy;
using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models.Classes;
using YogaClassManager.Core.Models.People;

namespace YogaClassManager.Core.Tests;

public class ClassRollRepositoryTests
{
    private static (InMemoryDataStore store, InMemoryClassRollRepository repo, ClassSchedule mon, ClassSchedule wed,
        Student student) CreateFixture()
    {
        var store = new InMemoryDataStore();
        var repo = new InMemoryClassRollRepository(store);

        var mon = new ClassSchedule(store.NextId(), DayOfWeek.Monday, new TimeOnly(9, 0), false);
        var wed = new ClassSchedule(store.NextId(), DayOfWeek.Wednesday, new TimeOnly(18, 0), false);
        store.ClassSchedules[mon.Id] = mon;
        store.ClassSchedules[wed.Id] = wed;

        var student = new Student(store.NextId(), "Alice", "Johnson", null, null, true);
        store.People[student.Id] = student;

        var today = DateOnly.FromDateTime(DateTime.Now);
        var oldRoll = new ClassRoll(store.NextId(), today.AddDays(-14), mon, [new ClassRollEntry(student, null)]);
        var recentRoll = new ClassRoll(store.NextId(), today.AddDays(-1), wed, [new ClassRollEntry(student, null)]);
        store.ClassRolls[oldRoll.Id] = oldRoll;
        store.ClassRolls[recentRoll.Id] = recentRoll;

        return (store, repo, mon, wed, student);
    }

    [Fact]
    public async Task Query_FiltersByDayOfWeekAndDateRange()
    {
        var (_, repo, mon, _, _) = CreateFixture();

        var mondayOnly = await repo.Query(new ClassRollFilter { DayOfWeek = DayOfWeek.Monday }).LoadMultiple();
        Assert.Single(mondayOnly);
        Assert.Equal(mon.Id, mondayOnly[0].ClassSchedule.Id);

        var today = DateOnly.FromDateTime(DateTime.Now);
        var lastThreeDays = await repo.Query(new ClassRollFilter { DateFrom = today.AddDays(-3) }).LoadMultiple();
        Assert.Single(lastThreeDays);
    }

    [Fact]
    public async Task Query_SortsByDateAscendingAndDescending()
    {
        var (_, repo, _, _, _) = CreateFixture();

        var ascending = await repo.Query(new ClassRollFilter
        {
            SortBy = new KeyValuePair<ClassRollSortOptions, Order>(ClassRollSortOptions.Date, Order.Ascending)
        }).LoadMultiple();
        Assert.True(ascending[0].Date < ascending[1].Date);

        var descending = await repo.Query(new ClassRollFilter
        {
            SortBy = new KeyValuePair<ClassRollSortOptions, Order>(ClassRollSortOptions.Date, Order.Descending)
        }).LoadMultiple();
        Assert.True(descending[0].Date > descending[1].Date);
    }

    [Fact]
    public async Task GetAttendanceHistoryAsync_ReturnsEveryRollTheStudentAttended()
    {
        var (_, repo, _, _, student) = CreateFixture();

        var history = await repo.GetAttendanceHistoryAsync(student.Id);

        Assert.Equal(2, history.Count);
        Assert.All(history, r => Assert.Equal(student.Id, r.StudentId));
    }
}
