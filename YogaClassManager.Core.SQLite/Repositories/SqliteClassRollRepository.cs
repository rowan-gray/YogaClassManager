using Dapper;
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

    public IDbModel<ClassRoll, ClassRollFilter> Query(ClassRollFilter filter)
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
            connection.ExecuteAsync("UPDATE ClassRoll SET ClassScheduleId=$scheduleId, Date=$date WHERE ClassId=$id",
                new { id = roll.Id, scheduleId = roll.ClassSchedule.Id, date = roll.Date }, transaction), cancellationToken);
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
        // Upsert-by-student: a student can't have two entries in one roll.
        var store = dataStoreProvider.RetrieveDataStore();
        return store.ExecuteInTransactionAsync((connection, transaction) =>
            connection.ExecuteAsync(
                """
                INSERT INTO ClassStudents(ClassId, StudentId, PassId) VALUES ($classId, $studentId, $passId)
                ON CONFLICT(ClassId, StudentId) DO UPDATE SET PassId = excluded.PassId
                """,
                new { classId = classRollId, studentId = entry.Student.Id, passId = entry.Pass?.Id }, transaction),
            cancellationToken);
    }

    public Task RemoveStudentEntryAsync(int classRollId, int studentId, CancellationToken cancellationToken = default)
    {
        var store = dataStoreProvider.RetrieveDataStore();
        return store.ExecuteInTransactionAsync((connection, transaction) =>
            connection.ExecuteAsync("DELETE FROM ClassStudents WHERE ClassId=$classId AND StudentId=$studentId",
                new { classId = classRollId, studentId }, transaction), cancellationToken);
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

    public async Task<IReadOnlyList<ClassAttendanceRecord>> GetAttendanceHistoryAsync(int studentId,
        CancellationToken cancellationToken = default)
    {
        var store = dataStoreProvider.RetrieveDataStore();
        var rows = await store.QueryAsync(connection => connection.QueryAsync<ClassAttendanceRow>(
            """
            SELECT cr.ClassId AS ClassRollId, cr.Date AS Date, cs.ClassScheduleId AS ClassScheduleId,
                   cs.Day AS Day, cs.Time AS Time, cs.IsActive AS IsActive,
                   csx.StudentId AS StudentId, csx.PassId AS PassId
            FROM ClassStudents csx
            JOIN ClassRoll cr ON cr.ClassId = csx.ClassId
            JOIN ClassSchedule cs ON cs.ClassScheduleId = cr.ClassScheduleId
            WHERE csx.StudentId = $studentId
            ORDER BY cr.Date DESC
            """, new { studentId }), cancellationToken);

        return rows.Select(r => r.ToRecord()).ToList();
    }
}

internal sealed class ClassRollRow
{
    public int ClassId { get; set; }
    public DateOnly Date { get; set; }
    public int ClassScheduleId { get; set; }
    public int Day { get; set; }
    public int Time { get; set; }
    public int IsActive { get; set; }

    public ClassSchedule ToClassSchedule()
    {
        return new ClassSchedule(ClassScheduleId, (DayOfWeek)Day, TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(Time)),
            isArchived: IsActive == 0);
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

internal class SqliteClassRollDbModel : IDbModel<ClassRoll, ClassRollFilter>
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
                          cs.Day AS Day, cs.Time AS Time, cs.IsActive AS IsActive
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

        var column = key switch
        {
            ClassRollSortOptions.Time => "cs.Time",
            ClassRollSortOptions.DayOfWeek => "cs.Day",
            _ => "cr.Date"
        };

        return SqlFilterBuilder.AppendPkTiebreaker($"{column} {direction}", "cr.ClassId");
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

    public Task Save(ClassRoll model, SaveOptions saveOptions = SaveOptions.CreateOrReplace,
        CancellationToken cancellationToken = default)
    {
        var store = dataStoreProvider.RetrieveDataStore();
        return store.ExecuteInTransactionAsync(async (connection, transaction) =>
        {
            var exists = model.Id > 0 && await connection.ExecuteScalarAsync<long>(
                "SELECT EXISTS(SELECT 1 FROM ClassRoll WHERE ClassId=$id)", new { id = model.Id }, transaction) == 1;

            if (exists && saveOptions == SaveOptions.Create)
                throw new ModelExistsException();
            if (!exists && saveOptions == SaveOptions.Replace)
                throw new ModelDoesNotExistException();

            if (exists)
            {
                await connection.ExecuteAsync("UPDATE ClassRoll SET ClassScheduleId=$scheduleId, Date=$date WHERE ClassId=$id",
                    new { id = model.Id, scheduleId = model.ClassSchedule.Id, date = model.Date }, transaction);
            }
            else
            {
                var id = await connection.ExecuteScalarAsync<long>(
                    "INSERT INTO ClassRoll(ClassScheduleId, Date) VALUES ($scheduleId, $date); SELECT last_insert_rowid();",
                    new { scheduleId = model.ClassSchedule.Id, date = model.Date }, transaction);
                model.Id = (int)id;
            }
        }, cancellationToken);
    }

    public Task Delete(ClassRoll model, CancellationToken cancellationToken = default)
    {
        var store = dataStoreProvider.RetrieveDataStore();
        return store.ExecuteInTransactionAsync((connection, transaction) =>
            connection.ExecuteAsync("DELETE FROM ClassRoll WHERE ClassId=$id", new { id = model.Id }, transaction),
            cancellationToken);
    }
}
