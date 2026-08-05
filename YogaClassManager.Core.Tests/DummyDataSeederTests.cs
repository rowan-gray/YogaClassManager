using YogaClassManager.Core.Dummy;
using YogaClassManager.Core.Models.Passes;
using YogaClassManager.Core.Models.People;

namespace YogaClassManager.Core.Tests;

public class DummyDataSeederTests
{
    private static InMemoryDataStore SeedStore()
    {
        var store = new InMemoryDataStore();
        DummyDataSeeder.Seed(store);
        return store;
    }

    [Fact]
    public void Seed_CreatesRoughlyEighteenPeopleAndTenStudents()
    {
        var store = SeedStore();

        Assert.InRange(store.People.Count, 15, 20);
        Assert.InRange(store.People.Values.OfType<Student>().Count(), 8, 12);
    }

    [Fact]
    public void Seed_IncludesAtLeastOneExpiredAndOneDepletedPass()
    {
        var store = SeedStore();

        Assert.Contains(store.Passes.Values, p => p.IsExpired);
        Assert.Contains(store.Passes.Values, p => p.IsDepleted);
    }

    [Fact]
    public void Seed_IncludesEveryPassType()
    {
        var store = SeedStore();

        Assert.Contains(store.Passes.Values, p => p is CasualPass);
        Assert.Contains(store.Passes.Values, p => p is DatedPass);
        Assert.Contains(store.Passes.Values, p => p is TermPass);
    }

    [Fact]
    public void Seed_IncludesAtLeastOneArchivedClassScheduleAndOneCompletedTerm()
    {
        var store = SeedStore();

        Assert.Contains(store.ClassSchedules.Values, s => s.IsArchived);
        Assert.Contains(store.Terms.Values, t => t.IsCompleted);
    }

    [Fact]
    public void Seed_IncludesAtLeastTwoDuplicatePersonPairs_MatchableByPhoneOrEmail()
    {
        var store = SeedStore();

        var byPhone = store.People.Values.Where(p => p.PhoneNumber is not null)
            .GroupBy(p => p.PhoneNumber)
            .Count(g => g.Count() > 1);
        var byEmail = store.People.Values.Where(p => p.Email is not null)
            .GroupBy(p => p.Email)
            .Count(g => g.Count() > 1);

        Assert.True(byPhone + byEmail >= 2);
    }

    [Fact]
    public void Seed_CreatesEnoughClassRollsToExerciseAttendanceAndSearchViews()
    {
        var store = SeedStore();

        Assert.True(store.ClassRolls.Count >= 30);
        Assert.Contains(store.ClassRolls.Values, r => r.StudentEntries.Count >= 2);
    }
}
