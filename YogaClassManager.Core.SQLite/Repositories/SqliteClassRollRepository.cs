using Dapper;
using Microsoft.Data.Sqlite;
using YogaClassManager.Core.Data;
using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models.Classes;
using YogaClassManager.Core.Models.People;
using YogaClassManager.Core.Repositories;
using YogaClassManager.Core.SQLite.Data;
using YogaClassManager.Core.SQLite.Hydration;

namespace YogaClassManager.Core.SQLite.Repositories;

public class SqliteClassRollRepository : IClassRollRepository
{
    private readonly IDataStoreProvider dataStoreProvider;

    public SqliteClassRollRepository(IDataStoreProvider dataStoreProvider)
    {
        this.dataStoreProvider = dataStoreProvider;
    }

    public IReadOnlyDbModel<ClassRoll, ClassRollFilter> Query(ClassRollFilter filter)
    {
        return new SqliteClassRollDbModel(filter, dataStoreProvider);
    }

    public async Task<int> AddAsync(ClassRoll roll, CancellationToken cancellationToken = default)
    {
        var store = dataStoreProvider.RetrieveDataStore();
        return await store.ExecuteInTransactionAsync(async (connection, transaction) =>
        {
            var id = await connection.ExecuteScalarAsync<long>(
                "INSERT INTO ClassRoll(ClassScheduleId, Date) VALUES ($scheduleId, $date); SELECT last_insert_rowid();",
                new { scheduleId = roll.ClassSchedule.Id, date = roll.Date }, transaction);

            roll.Id = (int)id;

            foreach (var entry in roll.StudentEntries)
                await connection.ExecuteAsync(
                    "INSERT INTO ClassStudents(ClassId, StudentId, PassId) VALUES ($classId, $studentId, $passId)",
                    new { classId = roll.Id, studentId = entry.Student.Id, passId = entry.Pass?.Id }, transaction);

            return roll.Id;
        }, cancellationToken);
    }

    public Task UpdateDetailsAsync(ClassRoll roll, CancellationToken cancellationToken = default)
    {
        // Deliberately touches only ClassRoll's own columns, never ClassStudents - StudentEntries are
        // managed via AddStudentEntryAsync/RemoveStudentEntryAsync/UpdateStudentEntryPassAsync.
        var store = dataStoreProvider.RetrieveDataStore();
        return store.ExecuteInTransactionAsync((connection, transaction) =>
            UpdateDetailsAsync(connection, transaction, roll), cancellationToken);
    }

    public Task DeleteAsync(int classRollId, CancellationToken cancellationToken = default)
    {
        // ClassStudents cascades via its existing ON DELETE CASCADE FK to ClassRoll - no manual cleanup needed.
        var store = dataStoreProvider.RetrieveDataStore();
        return store.ExecuteInTransactionAsync((connection, transaction) =>
            connection.ExecuteAsync("DELETE FROM ClassRoll WHERE ClassId=$id", new { id = classRollId }, transaction),
            cancellationToken);
    }

    public Task AddStudentEntryAsync(int classRollId, ClassRollEntry entry, CancellationToken cancellationToken = default)
    {
        var store = dataStoreProvider.RetrieveDataStore();
        return store.ExecuteInTransactionAsync((connection, transaction) =>
            UpsertStudentEntryAsync(connection, transaction, classRollId, entry), cancellationToken);
    }

    public Task RemoveStudentEntryAsync(int classRollId, int studentId, CancellationToken cancellationToken = default)
    {
        var store = dataStoreProvider.RetrieveDataStore();
        return store.ExecuteInTransactionAsync((connection, transaction) =>
            RemoveStudentEntryAsync(connection, transaction, classRollId, studentId), cancellationToken);
    }

    public Task UpdateStudentEntryPassAsync(int classRollId, int studentId, int? passId,
        CancellationToken cancellationToken = default)
    {
        var store = dataStoreProvider.RetrieveDataStore();
        return store.ExecuteInTransactionAsync((connection, transaction) =>
            connection.ExecuteAsync(
                "UPDATE ClassStudents SET PassId=$passId WHERE ClassId=$classId AND StudentId=$studentId",
                new { classId = classRollId, studentId, passId }, transaction), cancellationToken);
    }

    public Task ApplyChangesAsync(ClassRoll details, IReadOnlyCollection<int> removedStudentIds,
        IReadOnlyCollection<ClassRollEntry> addedOrChangedEntries, CancellationToken cancellationToken = default)
    {
        // One transaction for the whole edit session, so a failure part-way (e.g. a FK violation on one
        // entry) rolls back the details and every earlier entry change instead of half-saving the roll.
        var store = dataStoreProvider.RetrieveDataStore();
        return store.ExecuteInTransactionAsync(async (connection, transaction) =>
        {
            await UpdateDetailsAsync(connection, transaction, details);

            foreach (var studentId in removedStudentIds)
                await RemoveStudentEntryAsync(connection, transaction, details.Id, studentId);

            foreach (var entry in addedOrChangedEntries)
                await UpsertStudentEntryAsync(connection, transaction, details.Id, entry);
        }, cancellationToken);
    }

    private static Task UpdateDetailsAsync(SqliteConnection connection, SqliteTransaction transaction, ClassRoll roll)
    {
        return connection.ExecuteAsync("UPDATE ClassRoll SET ClassScheduleId=$scheduleId, Date=$date WHERE ClassId=$id",
            new { id = roll.Id, scheduleId = roll.ClassSchedule.Id, date = roll.Date }, transaction);
    }

    private static Task RemoveStudentEntryAsync(SqliteConnection connection, SqliteTransaction transaction,
        int classRollId, int studentId)
    {
        return connection.ExecuteAsync("DELETE FROM ClassStudents WHERE ClassId=$classId AND StudentId=$studentId",
            new { classId = classRollId, studentId }, transaction);
    }

    /// <summary>Upsert-by-student: a student can't have two entries in one roll. Deliberately
    /// UPDATE-then-INSERT rather than INSERT ... ON CONFLICT DO UPDATE: an UPSERT's conflict policy overrides
    /// the Modifications table's ON CONFLICT REPLACE inside the ClassStudents triggers, so the update
    /// trigger's audit row collides with the one the original insert logged and the statement fails.</summary>
    private static async Task UpsertStudentEntryAsync(SqliteConnection connection, SqliteTransaction transaction,
        int classRollId, ClassRollEntry entry)
    {
        var parameters = new { classId = classRollId, studentId = entry.Student.Id, passId = entry.Pass?.Id };

        var updated = await connection.ExecuteAsync(
            "UPDATE ClassStudents SET PassId=$passId WHERE ClassId=$classId AND StudentId=$studentId",
            parameters, transaction);

        if (updated == 0)
            await connection.ExecuteAsync(
                "INSERT INTO ClassStudents(ClassId, StudentId, PassId) VALUES ($classId, $studentId, $passId)",
                parameters, transaction);
    }

    public async Task<IReadOnlyList<ClassAttendanceRecord>> GetAttendanceHistoryAsync(int studentId,
        ClassRollFilter filter = default, CancellationToken cancellationToken = default)
    {
        // Routed through Query/SqliteClassRollDbModel - the exact same filtering/ordering (and the
        // same pass hydration via PassBatchLoader) the Class Rolls list uses - rather than a parallel
        // hand-written query, so the two can't drift apart.
        var rolls = await Query(filter).LoadMultiple(cancellationToken: cancellationToken);

        return rolls
            .Where(r => r.StudentEntries.Any(e => e.Student.Id == studentId))
            .Select(r =>
            {
                var entry = r.StudentEntries.First(e => e.Student.Id == studentId);
                return new ClassAttendanceRecord(r.Id, r.Date, r.ClassSchedule, studentId, entry.Pass);
            })
            .ToList();
    }
}

internal sealed class ClassRollRow
{
    public int ClassId { get; set; }
    public DateOnly Date { get; set; }
    public int ClassScheduleId { get; set; }
    public int Day { get; set; }
    public int Time { get; set; }
    public int IsArchived { get; set; }

    public ClassSchedule ToClassSchedule()
    {
        return new ClassSchedule(ClassScheduleId, (DayOfWeek)Day, TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(Time)),
            isArchived: IsArchived != 0);
    }
}

internal sealed class ClassRollEntryRow
{
    public int ClassId { get; set; }
    public int StudentId { get; set; }
    public string FirstName { get; set; } = "";
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public int IsActive { get; set; }
    public int? PassId { get; set; }
}

internal class SqliteClassRollDbModel : IReadOnlyDbModel<ClassRoll, ClassRollFilter>
{
    private readonly IDataStoreProvider dataStoreProvider;

    public SqliteClassRollDbModel(ClassRollFilter filter, IDataStoreProvider dataStoreProvider)
    {
        Filter = filter;
        this.dataStoreProvider = dataStoreProvider;
    }

    public ClassRollFilter Filter { get; init; }

    public async Task<ClassRoll?> LoadSingle(CancellationToken cancellationToken = default)
    {
        var results = await LoadMultiple(1, 0, cancellationToken);
        return results.FirstOrDefault();
    }

    public async Task<IReadOnlyList<ClassRoll>> LoadMultiple(uint count = uint.MaxValue, uint skip = 0,
        CancellationToken cancellationToken = default)
    {
        var store = dataStoreProvider.RetrieveDataStore();
        var filter = Filter;
        var take = count > int.MaxValue ? int.MaxValue : (int)count;
        var orderBy = BuildOrderBy(filter.SortBy);

        var sql = $"""
                   SELECT cr.ClassId AS ClassId, cr.Date AS Date, cs.ClassScheduleId AS ClassScheduleId,
                          cs.Day AS Day, cs.Time AS Time, cs.IsArchived AS IsArchived
                   FROM ClassRoll cr
                   JOIN ClassSchedule cs ON cs.ClassScheduleId = cr.ClassScheduleId
                   WHERE ($id IS NULL OR cr.ClassId = $id)
                     AND ($classScheduleId IS NULL OR cr.ClassScheduleId = $classScheduleId)
                     AND ($dateFrom IS NULL OR cr.Date >= $dateFrom)
                     AND ($dateTo IS NULL OR cr.Date <= $dateTo)
                     AND ($timeFrom IS NULL OR cs.Time >= $timeFrom)
                     AND ($timeTo IS NULL OR cs.Time <= $timeTo)
                     AND ($dayOfWeek IS NULL OR cs.Day = $dayOfWeek)
                   ORDER BY {orderBy}
                   LIMIT $take OFFSET $skip
                   """;

        var rollRows = (await store.QueryAsync(connection => connection.QueryAsync<ClassRollRow>(sql, new
        {
            id = (int?)filter.Id,
            classScheduleId = filter.ClassScheduleId,
            dateFrom = filter.DateFrom,
            dateTo = filter.DateTo,
            timeFrom = filter.TimeFrom is null ? null : (int?)(filter.TimeFrom.Value.Hour * 60 + filter.TimeFrom.Value.Minute),
            timeTo = filter.TimeTo is null ? null : (int?)(filter.TimeTo.Value.Hour * 60 + filter.TimeTo.Value.Minute),
            dayOfWeek = (int?)filter.DayOfWeek,
            take,
            skip = (int)skip
        }), cancellationToken)).ToList();

        if (rollRows.Count == 0)
            return [];

        var rollIds = rollRows.Select(r => r.ClassId).ToList();

        var entryRows = (await store.QueryAsync(connection => connection.QueryAsync<ClassRollEntryRow>(
            """
            SELECT csx.ClassId AS ClassId, csx.StudentId AS StudentId, p.FirstName AS FirstName,
                   p.LastName AS LastName, p.PhoneNumber AS PhoneNumber, p.Email AS Email,
                   p.IsActive AS IsActive, csx.PassId AS PassId
            FROM ClassStudents csx
            JOIN Person p ON p.PersonId = csx.StudentId
            WHERE csx.ClassId IN @rollIds
            """, new { rollIds }), cancellationToken)).ToList();

        var passIds = entryRows.Where(e => e.PassId is not null).Select(e => e.PassId!.Value).Distinct().ToList();
        var passesById = await PassBatchLoader.LoadAsync(store, passIds, cancellationToken);

        var entriesByRoll = entryRows.GroupBy(e => e.ClassId).ToDictionary(g => g.Key, g => g.Select(e =>
        {
            var student = new Student(e.StudentId, e.FirstName, e.LastName ?? "", e.PhoneNumber, e.Email, e.IsActive == 1);
            var pass = e.PassId is not null ? passesById.GetValueOrDefault(e.PassId.Value) : null;
            return new ClassRollEntry(student, pass);
        }).ToList());

        return rollRows.Select(r => new ClassRoll(r.ClassId, r.Date, r.ToClassSchedule(),
            entriesByRoll.GetValueOrDefault(r.ClassId, []))).ToList();
    }

    private static string BuildOrderBy(KeyValuePair<ClassRollSortOptions, Order>? sortBy)
    {
        if (sortBy is null)
            return SqlFilterBuilder.AppendPkTiebreaker("cr.Date DESC, cs.Time ASC", "cr.ClassId");

        var (key, order) = sortBy.Value;
        var direction = StringValueAttribute.GetStringValue(order);

        // Ties break so each sort still reads chronologically (must match InMemoryClassRollRepository):
        // Date - same-day classes by time in the same direction (newest first = latest class first);
        // Time - most recent date first; Day of week - time of day, then most recent date first.
        var orderBy = key switch
        {
            ClassRollSortOptions.Time => $"cs.Time {direction}, cr.Date DESC",
            ClassRollSortOptions.DayOfWeek => $"cs.Day {direction}, cs.Time ASC, cr.Date DESC",
            _ => $"cr.Date {direction}, cs.Time {direction}"
        };

        return SqlFilterBuilder.AppendPkTiebreaker(orderBy, "cr.ClassId");
    }

    public async Task<bool> Refresh(ClassRoll model, CancellationToken cancellationToken = default)
    {
        var fresh = await new SqliteClassRollDbModel(new ClassRollFilter { Id = (uint)model.Id }, dataStoreProvider)
            .LoadSingle(cancellationToken);

        if (fresh is null)
            return false;

        model.Date = fresh.Date;
        model.ClassSchedule = fresh.ClassSchedule;
        model.StudentEntries = fresh.StudentEntries;
        return true;
    }
}
