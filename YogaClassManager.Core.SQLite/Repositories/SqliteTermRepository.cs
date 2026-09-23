using Dapper;
using YogaClassManager.Core.Data;
using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models.Classes;
using YogaClassManager.Core.Repositories;
using YogaClassManager.Core.SQLite.Data;

namespace YogaClassManager.Core.SQLite.Repositories;

public class SqliteTermRepository : ITermRepository
{
    private readonly IDataStoreProvider dataStoreProvider;

    public SqliteTermRepository(IDataStoreProvider dataStoreProvider)
    {
        this.dataStoreProvider = dataStoreProvider;
    }

    public IDbModel<Term, TermFilter> Query(TermFilter filter)
    {
        return new SqliteTermDbModel(filter, dataStoreProvider);
    }

    public Task<int> AddAsync(Term term, CancellationToken cancellationToken = default)
    {
        var store = dataStoreProvider.RetrieveDataStore();
        return store.ExecuteInTransactionAsync(async (connection, transaction) =>
        {
            var id = await connection.ExecuteScalarAsync<long>(
                """
                INSERT INTO Term(TermName, StartDate, EndDate, CatchupStartDate, CatchupEndDate)
                VALUES ($name, $startDate, $endDate, $catchupStart, $catchupEnd);
                SELECT last_insert_rowid();
                """,
                new
                {
                    name = term.Name, startDate = term.StartDate, endDate = term.EndDate,
                    catchupStart = term.CatchupStartDate, catchupEnd = term.CatchupEndDate
                }, transaction);

            term.Id = (int)id;

            foreach (var termClass in term.Classes)
                await connection.ExecuteAsync(
                    "INSERT INTO TermClasses(TermId, ClassId, ClassCount) VALUES ($termId, $classId, $classCount)",
                    new { termId = term.Id, classId = termClass.ClassSchedule.Id, classCount = termClass.ClassCount },
                    transaction);

            return term.Id;
        }, cancellationToken);
    }

    /// <summary>Deliberately narrow: only updates Term's own five scalar columns, never touches
    /// TermClasses - LinkClassAsync/UnlinkClassAsync are the dedicated, surgical methods for changing
    /// class links, and letting a generic UpdateAsync silently wipe usage-bearing links would be a
    /// foot-gun (no existing test exercises UpdateAsync with a populated Classes collection, so this
    /// is a deliberate, documented choice rather than a constraint carried over from elsewhere).</summary>
    public Task UpdateAsync(Term term, CancellationToken cancellationToken = default)
    {
        var store = dataStoreProvider.RetrieveDataStore();
        return store.ExecuteInTransactionAsync((connection, transaction) =>
            connection.ExecuteAsync(
                """
                UPDATE Term SET TermName=$name, StartDate=$startDate, EndDate=$endDate,
                                CatchupStartDate=$catchupStart, CatchupEndDate=$catchupEnd
                WHERE TermId=$id
                """,
                new
                {
                    id = term.Id, name = term.Name, startDate = term.StartDate, endDate = term.EndDate,
                    catchupStart = term.CatchupStartDate, catchupEnd = term.CatchupEndDate
                }, transaction), cancellationToken);
    }

    public Task<bool> TryDeleteAsync(int termId, CancellationToken cancellationToken = default)
    {
        var store = dataStoreProvider.RetrieveDataStore();
        return store.ExecuteInTransactionAsync(async (connection, transaction) =>
        {
            var hasLinkedClasses = await connection.ExecuteScalarAsync<long>(
                "SELECT EXISTS(SELECT 1 FROM TermClasses WHERE TermId = $id)", new { id = termId }, transaction) == 1;

            if (hasLinkedClasses)
                return false;

            await connection.ExecuteAsync("DELETE FROM Term WHERE TermId=$id", new { id = termId }, transaction);
            return true;
        }, cancellationToken);
    }

    public Task LinkClassAsync(int termId, int classScheduleId, int classCount,
        CancellationToken cancellationToken = default)
    {
        var store = dataStoreProvider.RetrieveDataStore();
        return store.ExecuteInTransactionAsync(async (connection, transaction) =>
        {
            var termExists = await connection.ExecuteScalarAsync<long>(
                "SELECT EXISTS(SELECT 1 FROM Term WHERE TermId = $termId)", new { termId }, transaction) == 1;
            var scheduleExists = await connection.ExecuteScalarAsync<long>(
                "SELECT EXISTS(SELECT 1 FROM ClassSchedule WHERE ClassScheduleId = $classId)",
                new { classId = classScheduleId }, transaction) == 1;

            if (!termExists || !scheduleExists)
                return;

            await connection.ExecuteAsync(
                """
                INSERT INTO TermClasses(TermId, ClassId, ClassCount) VALUES ($termId, $classId, $classCount)
                ON CONFLICT(TermId, ClassId) DO UPDATE SET ClassCount = excluded.ClassCount
                """,
                new { termId, classId = classScheduleId, classCount }, transaction);
        }, cancellationToken);
    }

    public Task<bool> UnlinkClassAsync(int termId, int classScheduleId, CancellationToken cancellationToken = default)
    {
        var store = dataStoreProvider.RetrieveDataStore();
        return store.ExecuteInTransactionAsync(async (connection, transaction) =>
        {
            var uses = await connection.QuerySingleOrDefaultAsync<int?>(
                "SELECT Uses FROM TermClassUses WHERE TermId = $termId AND ClassId = $classId",
                new { termId, classId = classScheduleId }, transaction);

            if (uses is null || uses > 0)
                return false;

            await connection.ExecuteAsync("DELETE FROM TermClasses WHERE TermId=$termId AND ClassId=$classId",
                new { termId, classId = classScheduleId }, transaction);
            return true;
        }, cancellationToken);
    }
}

internal sealed class TermRow
{
    public int TermId { get; set; }
    public string TermName { get; set; } = "";
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public DateOnly? CatchupStartDate { get; set; }
    public DateOnly? CatchupEndDate { get; set; }
}

internal sealed class TermClassRow
{
    public int TermId { get; set; }
    public int ClassId { get; set; }
    public int ClassCount { get; set; }
    public int Uses { get; set; }
    public int Day { get; set; }
    public int Time { get; set; }
    public int IsActive { get; set; }

    public TermClassSchedule ToModel()
    {
        var classSchedule = new ClassSchedule(ClassId, (DayOfWeek)Day, TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(Time)),
            isArchived: IsActive == 0);
        return new TermClassSchedule(classSchedule, ClassCount, Uses);
    }
}

internal class SqliteTermDbModel : IDbModel<Term, TermFilter>
{
    private readonly IDataStoreProvider dataStoreProvider;

    public SqliteTermDbModel(TermFilter filter, IDataStoreProvider dataStoreProvider)
    {
        Filter = filter;
        this.dataStoreProvider = dataStoreProvider;
    }

    public TermFilter Filter { get; init; }

    public async Task<Term?> LoadSingle(CancellationToken cancellationToken = default)
    {
        var results = await LoadMultiple(1, 0, cancellationToken);
        return results.FirstOrDefault();
    }

    public async Task<IReadOnlyList<Term>> LoadMultiple(uint count = uint.MaxValue, uint skip = 0,
        CancellationToken cancellationToken = default)
    {
        var store = dataStoreProvider.RetrieveDataStore();
        var filter = Filter;
        var take = count > int.MaxValue ? int.MaxValue : (int)count;
        var orderBy = BuildOrderBy(filter.SortBy);

        var sql = $"""
                   SELECT TermId, TermName, StartDate, EndDate, CatchupStartDate, CatchupEndDate
                   FROM Term
                   WHERE ($id IS NULL OR TermId = $id)
                     AND ($nameFilter IS NULL OR TermName LIKE $namePattern ESCAPE '\')
                     AND ($includeCompleted = 1 OR date('now','localtime') <= COALESCE(CatchupEndDate, EndDate))
                   ORDER BY {orderBy}
                   LIMIT $take OFFSET $skip
                   """;

        var termRows = (await store.QueryAsync(connection => connection.QueryAsync<TermRow>(sql, new
        {
            id = (int?)filter.Id,
            nameFilter = filter.NameFilter,
            namePattern = filter.NameFilter is null ? null : SqlFilterBuilder.ContainsPattern(filter.NameFilter),
            includeCompleted = filter.IncludeCompleted ? 1 : 0,
            take,
            skip = (int)skip
        }), cancellationToken)).ToList();

        if (termRows.Count == 0)
            return [];

        var termIds = termRows.Select(t => t.TermId).ToList();

        var classRows = await store.QueryAsync(connection => connection.QueryAsync<TermClassRow>(
            """
            SELECT tc.TermId AS TermId, tc.ClassId AS ClassId, tc.ClassCount AS ClassCount, tcu.Uses AS Uses,
                   cs.Day AS Day, cs.Time AS Time, cs.IsActive AS IsActive
            FROM TermClasses tc
            JOIN ClassSchedule cs ON cs.ClassScheduleId = tc.ClassId
            LEFT JOIN TermClassUses tcu ON tcu.TermId = tc.TermId AND tcu.ClassId = tc.ClassId
            WHERE tc.TermId IN @termIds
            """, new { termIds }), cancellationToken);

        var classesByTerm = classRows.GroupBy(c => c.TermId)
            .ToDictionary(g => g.Key, g => g.Select(c => c.ToModel()).ToList());

        return termRows.Select(t => new Term(t.TermId, t.TermName, t.StartDate, t.EndDate, t.CatchupStartDate,
            t.CatchupEndDate, classesByTerm.GetValueOrDefault(t.TermId, []))).ToList();
    }

    private static string BuildOrderBy(KeyValuePair<TermSortOptions, Order>? sortBy)
    {
        if (sortBy is null)
            return SqlFilterBuilder.AppendPkTiebreaker("StartDate DESC", "TermId");

        var (key, order) = sortBy.Value;
        var direction = StringValueAttribute.GetStringValue(order);

        var column = key switch
        {
            TermSortOptions.Name => "TermName",
            TermSortOptions.StartDate => "StartDate",
            TermSortOptions.EndDate => "EndDate",
            _ => "TermId"
        };

        return SqlFilterBuilder.AppendPkTiebreaker($"{column} {direction}", "TermId");
    }

    public async Task<bool> Refresh(Term model, CancellationToken cancellationToken = default)
    {
        var fresh = await new SqliteTermDbModel(new TermFilter { Id = (uint)model.Id, IncludeCompleted = true },
            dataStoreProvider).LoadSingle(cancellationToken);

        if (fresh is null)
            return false;

        model.Name = fresh.Name;
        model.StartDate = fresh.StartDate;
        model.EndDate = fresh.EndDate;
        model.CatchupStartDate = fresh.CatchupStartDate;
        model.CatchupEndDate = fresh.CatchupEndDate;
        model.Classes.Clear();
        foreach (var termClass in fresh.Classes)
            model.Classes.Add(termClass);
        return true;
    }

    public Task Save(Term model, SaveOptions saveOptions = SaveOptions.CreateOrReplace,
        CancellationToken cancellationToken = default)
    {
        var store = dataStoreProvider.RetrieveDataStore();
        return store.ExecuteInTransactionAsync(async (connection, transaction) =>
        {
            var exists = model.Id > 0 && await connection.ExecuteScalarAsync<long>(
                "SELECT EXISTS(SELECT 1 FROM Term WHERE TermId=$id)", new { id = model.Id }, transaction) == 1;

            if (exists && saveOptions == SaveOptions.Create)
                throw new ModelExistsException();
            if (!exists && saveOptions == SaveOptions.Replace)
                throw new ModelDoesNotExistException();

            if (exists)
            {
                await connection.ExecuteAsync(
                    """
                    UPDATE Term SET TermName=$name, StartDate=$startDate, EndDate=$endDate,
                                    CatchupStartDate=$catchupStart, CatchupEndDate=$catchupEnd
                    WHERE TermId=$id
                    """,
                    new
                    {
                        id = model.Id, name = model.Name, startDate = model.StartDate, endDate = model.EndDate,
                        catchupStart = model.CatchupStartDate, catchupEnd = model.CatchupEndDate
                    }, transaction);
            }
            else
            {
                var id = await connection.ExecuteScalarAsync<long>(
                    """
                    INSERT INTO Term(TermName, StartDate, EndDate, CatchupStartDate, CatchupEndDate)
                    VALUES ($name, $startDate, $endDate, $catchupStart, $catchupEnd);
                    SELECT last_insert_rowid();
                    """,
                    new
                    {
                        name = model.Name, startDate = model.StartDate, endDate = model.EndDate,
                        catchupStart = model.CatchupStartDate, catchupEnd = model.CatchupEndDate
                    }, transaction);
                model.Id = (int)id;
            }
        }, cancellationToken);
    }

    public Task Delete(Term model, CancellationToken cancellationToken = default)
    {
        var store = dataStoreProvider.RetrieveDataStore();
        return store.ExecuteInTransactionAsync((connection, transaction) =>
            connection.ExecuteAsync("DELETE FROM Term WHERE TermId=$id", new { id = model.Id }, transaction),
            cancellationToken);
    }
}
