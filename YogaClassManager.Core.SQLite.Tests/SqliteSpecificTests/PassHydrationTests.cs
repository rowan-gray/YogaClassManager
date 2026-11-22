using Dapper;
using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models.Passes;
using YogaClassManager.Core.SQLite.Data;
using YogaClassManager.Core.SQLite.Repositories;

namespace YogaClassManager.Core.SQLite.Tests.SqliteSpecificTests;

public class PassHydrationTests
{
    /// <summary>Dapper builds its row mapper from the first row's column types. PassStatus'
    /// TermClassUsesCount used to be NULL (untyped) for non-term passes and INTEGER for term passes,
    /// so a student whose first pass wasn't a term pass failed to load with "Error parsing column
    /// (TermClassUsesCount=... - Int64)" as soon as a term pass row followed.</summary>
    [Fact]
    public async Task LoadMultiple_HydratesTermPass_WhenANonTermPassRowComesFirst()
    {
        await using var store = await SqliteTestDatabaseFactory.CreateAsync();
        await store.ExecuteInTransactionAsync((connection, transaction) => connection.ExecuteAsync(
            """
            INSERT INTO Person(PersonId, FirstName, PhoneNumber) VALUES (1, 'Annie', '0400000000');
            INSERT INTO Student(StudentId) VALUES (1);
            INSERT INTO ClassSchedule(ClassScheduleId, Day, Time, IsArchived) VALUES (1, 1, 540, 0);
            INSERT INTO Term(TermId, TermName, StartDate, EndDate) VALUES (1, 'Term 1', '2026-01-01', '2026-03-31');
            INSERT INTO TermClasses(TermId, ClassId, ClassCount) VALUES (1, 1, 10);
            INSERT INTO Pass(PassId, StudentId) VALUES (1, 1), (2, 1);
            INSERT INTO DatedPass(PassId, ClassCount, StartDate, EndDate) VALUES (1, 5, '2026-01-01', '2026-12-31');
            INSERT INTO TermPass(PassId, TermId, ClassId) VALUES (2, 1, 1);
            """, transaction: transaction));

        var passes = await new SqlitePassRepository(new FixedDataStoreProvider(store))
            .Query(new PassFilter { StudentId = 1, IncludeExpired = true, IncludeDepleted = true })
            .LoadMultiple();

        Assert.Equal(2, passes.Count);
        Assert.Contains(passes, p => p is DatedPass);
        Assert.Contains(passes, p => p is TermPass);
    }

    /// <summary>Legacy production DatedPass rows can have a NULL EndDate, which used to fail with
    /// "Nullable object must have a value" and stop the whole student from loading.</summary>
    [Fact]
    public async Task LoadMultiple_TreatsMissingDatedPassEndDate_AsStartDatePlusTenWeeks()
    {
        await using var store = await SqliteTestDatabaseFactory.CreateAsync();
        await store.ExecuteInTransactionAsync((connection, transaction) => connection.ExecuteAsync(
            """
            INSERT INTO Person(PersonId, FirstName, PhoneNumber) VALUES (1, 'Alison', '0400000000');
            INSERT INTO Student(StudentId) VALUES (1);
            INSERT INTO Pass(PassId, StudentId) VALUES (1, 1);
            INSERT INTO DatedPass(PassId, ClassCount, StartDate, EndDate) VALUES (1, 0, '2022-06-11', NULL);
            """, transaction: transaction));
        var repository = new SqlitePassRepository(new FixedDataStoreProvider(store));

        var passes = await repository
            .Query(new PassFilter { StudentId = 1, IncludeExpired = true, IncludeDepleted = true })
            .LoadMultiple();

        var pass = Assert.IsType<DatedPass>(Assert.Single(passes));
        Assert.Equal(new DateOnly(2022, 8, 20), pass.EndDate);
        Assert.True(pass.IsExpired);

        // The SQL-side IsExpired (used by the filter) must agree with the model.
        Assert.Empty(await repository
            .Query(new PassFilter { StudentId = 1, IncludeExpired = false, IncludeDepleted = true })
            .LoadMultiple());
    }
}
