using YogaClassManager.Core.Data;
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

    /// <summary>The Term edit dialog saves a new Term object carrying the original's linked classes;
    /// the details must persist and the class links must survive.</summary>
    [Fact]
    public async Task UpdateAsync_PersistsEditedDetails_AndKeepsLinkedClasses()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var termId = await Factory.Terms.AddAsync(new Term(0, "Term 3", today, today.AddDays(70), null, null, []));
        var scheduleId = await Factory.ClassSchedules.AddAsync(new ClassSchedule(0, DayOfWeek.Monday, new TimeOnly(9, 0), false));
        await Factory.Terms.LinkClassAsync(termId, scheduleId, 10);
        var loaded = (await Factory.Terms.Query(new TermFilter { Id = (uint)termId }).LoadSingle())!;

        await Factory.Terms.UpdateAsync(new Term(termId, "Term 3 (extended)", today.AddDays(1), today.AddDays(80),
            today.AddDays(-6), today.AddDays(90), loaded.Classes));

        var reloaded = (await Factory.Terms.Query(new TermFilter { Id = (uint)termId }).LoadSingle())!;
        Assert.Equal("Term 3 (extended)", reloaded.Name);
        Assert.Equal((today.AddDays(1), today.AddDays(80)), (reloaded.StartDate, reloaded.EndDate));
        Assert.Equal((today.AddDays(-6), today.AddDays(90)), (reloaded.CatchupStartDate!.Value, reloaded.CatchupEndDate!.Value));
        var termClass = Assert.Single(reloaded.Classes);
        Assert.Equal((scheduleId, 10), (termClass.ClassSchedule.Id, termClass.ClassCount));
    }

    /// <summary>UpdateAsync ignores the Classes it's given: an empty list doesn't unlink anything (which
    /// would also bypass UnlinkClassAsync's used-class guard), and an extra class isn't linked.</summary>
    [Fact]
    public async Task UpdateAsync_IgnoresTheGivenClasses_LinksChangeOnlyViaLinkAndUnlink()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var termId = await Factory.Terms.AddAsync(new Term(0, "Term", today, today.AddDays(70), null, null, []));
        var linkedId = await Factory.ClassSchedules.AddAsync(new ClassSchedule(0, DayOfWeek.Monday, new TimeOnly(9, 0), false));
        var otherId = await Factory.ClassSchedules.AddAsync(new ClassSchedule(0, DayOfWeek.Friday, new TimeOnly(9, 0), false));
        var other = (await Factory.ClassSchedules.Query(new ClassScheduleFilter { Id = (uint)otherId }).LoadSingle())!;
        await Factory.Terms.LinkClassAsync(termId, linkedId, 10);

        await Factory.Terms.UpdateAsync(new Term(termId, "Renamed", today, today.AddDays(70), null, null, []));
        var afterEmpty = (await Factory.Terms.Query(new TermFilter { Id = (uint)termId }).LoadSingle())!;
        Assert.Equal("Renamed", afterEmpty.Name);
        Assert.Equal(linkedId, Assert.Single(afterEmpty.Classes).ClassSchedule.Id);

        await Factory.Terms.UpdateAsync(new Term(termId, "Renamed", today, today.AddDays(70), null, null,
            [new TermClassSchedule(other, 5, 0)]));
        var afterExtra = (await Factory.Terms.Query(new TermFilter { Id = (uint)termId }).LoadSingle())!;
        Assert.Equal(linkedId, Assert.Single(afterExtra.Classes).ClassSchedule.Id);
    }

    [Fact]
    public async Task UpdateAsync_CanRemoveTheCatchupPeriod()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var termId = await Factory.Terms.AddAsync(new Term(0, "Term", today, today.AddDays(70),
            today.AddDays(-7), today.AddDays(77), []));

        await Factory.Terms.UpdateAsync(new Term(termId, "Term", today, today.AddDays(70), null, null, []));

        var reloaded = (await Factory.Terms.Query(new TermFilter { Id = (uint)termId }).LoadSingle())!;
        Assert.Null(reloaded.CatchupStartDate);
        Assert.Null(reloaded.CatchupEndDate);
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

    [Theory]
    [InlineData(TermSortOptions.Id, Order.Ascending)]
    [InlineData(TermSortOptions.Id, Order.Descending)]
    [InlineData(TermSortOptions.Name, Order.Ascending)]
    [InlineData(TermSortOptions.Name, Order.Descending)]
    [InlineData(TermSortOptions.StartDate, Order.Ascending)]
    [InlineData(TermSortOptions.StartDate, Order.Descending)]
    [InlineData(TermSortOptions.EndDate, Order.Ascending)]
    [InlineData(TermSortOptions.EndDate, Order.Descending)]
    public async Task Query_SortsByEveryKeyInBothDirections(TermSortOptions key, Order order)
    {
        int[] ids =
        [
            await Factory.Terms.AddAsync(new Term(0, "Winter", new DateOnly(2026, 6, 1), new DateOnly(2026, 9, 30), null, null, [])),
            await Factory.Terms.AddAsync(new Term(0, "Autumn", new DateOnly(2026, 3, 1), new DateOnly(2026, 10, 31), null, null, [])),
            await Factory.Terms.AddAsync(new Term(0, "Spring", new DateOnly(2026, 9, 1), new DateOnly(2026, 11, 30), null, null, [])),
            await Factory.Terms.AddAsync(new Term(0, "Bonus", new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 28), null, null, []))
        ];
        int[] ascending = key switch
        {
            TermSortOptions.Name => [1, 3, 2, 0],
            TermSortOptions.StartDate => [3, 1, 0, 2],
            TermSortOptions.EndDate => [3, 0, 1, 2],
            _ => [0, 1, 2, 3]
        };

        var results = await Factory.Terms.Query(new TermFilter
        {
            IncludeCompleted = true,
            SortBy = new KeyValuePair<TermSortOptions, Order>(key, order)
        }).LoadMultiple();

        var expected = ascending.Select(i => ids[i]);
        Assert.Equal(order == Order.Ascending ? expected : expected.Reverse(), results.Select(t => t.Id));
    }
}
