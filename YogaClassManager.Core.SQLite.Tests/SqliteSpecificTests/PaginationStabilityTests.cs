using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models.Classes;
using YogaClassManager.Core.SQLite.Repositories;

namespace YogaClassManager.Core.SQLite.Tests.SqliteSpecificTests;

/// <summary>Mirrors GrowableCollection.GrowCollection's exact calling pattern - repeated
/// LoadMultiple(amount, skip: runningCount) - against a table with duplicate primary sort-key values
/// and rows inserted mid-sequence, proving the mandatory PK tiebreaker (SqlFilterBuilder.
/// AppendPkTiebreaker) actually delivers stable paging, unlike a bare LIMIT/OFFSET over a
/// non-fully-deterministic ORDER BY would.</summary>
public class PaginationStabilityTests
{
    [Fact]
    public async Task LoadMultiple_PagesStably_WithDuplicateSortKeysAndRowsInsertedMidSequence()
    {
        await using var store = await SqliteTestDatabaseFactory.CreateAsync();
        var scheduleRepo = new SqliteClassScheduleRepository(store);
        var rollRepo = new SqliteClassRollRepository(store);

        var scheduleId = await scheduleRepo.AddAsync(new ClassSchedule(0, DayOfWeek.Monday, new TimeOnly(9, 0), false));
        var schedule = (await scheduleRepo.Query(new ClassScheduleFilter { Id = (uint)scheduleId }).LoadSingle())!;

        // All rolls share the same Date - the default sort's primary key (Date DESC) is entirely
        // non-unique here, so correctness depends entirely on the ClassId tiebreaker.
        var sameDate = DateOnly.FromDateTime(DateTime.Now);
        var allIds = new List<int>();
        for (var i = 0; i < 10; i++)
            allIds.Add(await rollRepo.AddAsync(new ClassRoll(0, sameDate, schedule, [])));

        var dbModel = rollRepo.Query(new ClassRollFilter());
        var collected = new List<int>();
        uint runningCount = 0;

        while (true)
        {
            var page = await dbModel.LoadMultiple(3, runningCount);
            if (page.Count == 0)
                break;

            collected.AddRange(page.Select(r => r.Id));
            runningCount += (uint)page.Count;

            // A row inserted here always gets a higher autoincrement id than everything already
            // paged past, so with ClassId ASC as the tiebreaker it can only ever appear in a later
            // page - never skipped, never duplicated, never reordered into an already-fetched page.
            if (runningCount == 3)
                allIds.Add(await rollRepo.AddAsync(new ClassRoll(0, sameDate, schedule, [])));
        }

        Assert.Equal(allIds.Count, collected.Count);
        Assert.Equal(collected.Count, collected.Distinct().Count());
        Assert.Equal(allIds.OrderBy(x => x), collected.OrderBy(x => x));
    }
}
