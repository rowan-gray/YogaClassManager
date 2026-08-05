using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models.Classes;

namespace YogaClassManager.Core.Tests.Shared;

public abstract class TermRepositoryTestBase<TFactory> : IAsyncLifetime
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
    public async Task Query_NameFilter_ActuallyFilters()
    {
        // The original MAUI app's TermsPageModel search was wired up but stubbed - it ignored the
        // query entirely. This is the explicit regression test that Core's TermFilter works.
        var today = DateOnly.FromDateTime(DateTime.Now);
        var termAId = await Factory.Terms.AddAsync(new Term(0, "Term 3 2026", today, today.AddDays(70), null, null, []));
        await Factory.Terms.AddAsync(new Term(0, "Winter Intensive", today, today.AddDays(30), null, null, []));

        var results = await Factory.Terms.Query(new TermFilter { NameFilter = "Term" }).LoadMultiple();

        Assert.Single(results);
        Assert.Equal(termAId, results[0].Id);
    }

    [Fact]
    public async Task Query_ExcludesCompletedTerms_UnlessRequested()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        await Factory.Terms.AddAsync(new Term(0, "Active", today.AddDays(-10), today.AddDays(70), null, null, []));
        await Factory.Terms.AddAsync(new Term(0, "Completed", today.AddDays(-120), today.AddDays(-30), null, null, []));

        var defaultResults = await Factory.Terms.Query(new TermFilter()).LoadMultiple();
        Assert.Single(defaultResults);

        var withCompleted = await Factory.Terms.Query(new TermFilter { IncludeCompleted = true }).LoadMultiple();
        Assert.Equal(2, withCompleted.Count);
    }

    [Fact]
    public async Task UnlinkClassAsync_RefusesWhenClassHasBeenUsed()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var termId = await Factory.Terms.AddAsync(new Term(0, "Term", today, today.AddDays(70), null, null, []));
        var scheduleId = await Factory.ClassSchedules.AddAsync(new ClassSchedule(0, DayOfWeek.Monday, new TimeOnly(9, 0), false));

        await Factory.Terms.LinkClassAsync(termId, scheduleId, 10);
        await Factory.SeedTermClassUsageAsync(termId, scheduleId, usesCount: 3);

        Assert.False(await Factory.Terms.UnlinkClassAsync(termId, scheduleId));

        var term = await Factory.Terms.Query(new TermFilter { Id = (uint)termId }).LoadSingle();
        Assert.Single(term!.Classes);
    }

    [Fact]
    public async Task TryDeleteAsync_RefusesWhenTermHasLinkedClasses()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var termId = await Factory.Terms.AddAsync(new Term(0, "Term", today, today.AddDays(70), null, null, []));
        var scheduleId = await Factory.ClassSchedules.AddAsync(new ClassSchedule(0, DayOfWeek.Monday, new TimeOnly(9, 0), false));

        await Factory.Terms.LinkClassAsync(termId, scheduleId, 10);

        Assert.False(await Factory.Terms.TryDeleteAsync(termId));

        Assert.True(await Factory.Terms.UnlinkClassAsync(termId, scheduleId));
        Assert.True(await Factory.Terms.TryDeleteAsync(termId));
    }
}
