using YogaClassManager.Core.Dummy;
using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models.Classes;

namespace YogaClassManager.Core.Tests;

public class TermRepositoryTests
{
    private static (InMemoryDataStore store, InMemoryTermRepository repo) CreateRepo()
    {
        var store = new InMemoryDataStore();
        return (store, new InMemoryTermRepository(store));
    }

    [Fact]
    public async Task Query_NameFilter_ActuallyFilters()
    {
        // The original MAUI app's TermsPageModel search was wired up but stubbed - it ignored the
        // query entirely. This is the explicit regression test that Core's TermFilter works.
        var (store, repo) = CreateRepo();
        var today = DateOnly.FromDateTime(DateTime.Now);
        var termA = new Term(store.NextId(), "Term 3 2026", today, today.AddDays(70), null, null, []);
        var termB = new Term(store.NextId(), "Winter Intensive", today, today.AddDays(30), null, null, []);
        store.Terms[termA.Id] = termA;
        store.Terms[termB.Id] = termB;

        var results = await repo.Query(new TermFilter { NameFilter = "Term" }).LoadMultiple();

        Assert.Single(results);
        Assert.Equal(termA.Id, results[0].Id);
    }

    [Fact]
    public async Task Query_ExcludesCompletedTerms_UnlessRequested()
    {
        var (store, repo) = CreateRepo();
        var today = DateOnly.FromDateTime(DateTime.Now);
        var active = new Term(store.NextId(), "Active", today.AddDays(-10), today.AddDays(70), null, null, []);
        var completed = new Term(store.NextId(), "Completed", today.AddDays(-120), today.AddDays(-30), null, null, []);
        store.Terms[active.Id] = active;
        store.Terms[completed.Id] = completed;

        var defaultResults = await repo.Query(new TermFilter()).LoadMultiple();
        Assert.Single(defaultResults);

        var withCompleted = await repo.Query(new TermFilter { IncludeCompleted = true }).LoadMultiple();
        Assert.Equal(2, withCompleted.Count);
    }

    [Fact]
    public async Task UnlinkClassAsync_RefusesWhenClassHasBeenUsed()
    {
        var (store, repo) = CreateRepo();
        var today = DateOnly.FromDateTime(DateTime.Now);
        var term = new Term(store.NextId(), "Term", today, today.AddDays(70), null, null, []);
        var schedule = new ClassSchedule(store.NextId(), DayOfWeek.Monday, new TimeOnly(9, 0), false);
        term.Classes.Add(new TermClassSchedule(schedule, 10, 3)); // already used 3 times
        store.Terms[term.Id] = term;
        store.ClassSchedules[schedule.Id] = schedule;

        Assert.False(await repo.UnlinkClassAsync(term.Id, schedule.Id));
        Assert.Single(term.Classes);
    }

    [Fact]
    public async Task TryDeleteAsync_RefusesWhenTermHasLinkedClasses()
    {
        var (store, repo) = CreateRepo();
        var today = DateOnly.FromDateTime(DateTime.Now);
        var term = new Term(store.NextId(), "Term", today, today.AddDays(70), null, null, []);
        var schedule = new ClassSchedule(store.NextId(), DayOfWeek.Monday, new TimeOnly(9, 0), false);
        term.Classes.Add(new TermClassSchedule(schedule, 10, 0));
        store.Terms[term.Id] = term;

        Assert.False(await repo.TryDeleteAsync(term.Id));

        term.Classes.Clear();
        Assert.True(await repo.TryDeleteAsync(term.Id));
    }
}
