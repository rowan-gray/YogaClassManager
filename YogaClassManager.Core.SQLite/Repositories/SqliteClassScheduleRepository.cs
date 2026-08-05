using Dapper;
using YogaClassManager.Core.Data;
using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models.Classes;
using YogaClassManager.Core.Repositories;
using YogaClassManager.Core.SQLite.Data;

namespace YogaClassManager.Core.SQLite.Repositories;

public class SqliteClassScheduleRepository : IClassScheduleRepository
{
    private readonly SqliteDataStore store;

    public SqliteClassScheduleRepository(SqliteDataStore store)
    {
        this.store = store;
    }

    public IDbModel<ClassSchedule, ClassScheduleFilter> Query(ClassScheduleFilter filter)
    {
        return new SqliteClassScheduleDbModel(filter, store);
    }

    public async Task<int> AddAsync(ClassSchedule schedule, CancellationToken cancellationToken = default)
    {
        return await store.ExecuteInTransactionAsync(async (connection, transaction) =>
        {
            await EnsureNoDayTimeCollisionAsync(connection, transaction, schedule.Day, schedule.Time, excludeId: null);

            var id = await connection.ExecuteScalarAsync<long>(
                """
                INSERT INTO ClassSchedule(Day, Time, IsActive) VALUES ($day, $time, $isActive);
                SELECT last_insert_rowid();
                """,
                new
                {
                    day = (int)schedule.Day,
                    time = schedule.Time.Hour * 60 + schedule.Time.Minute,
                    isActive = schedule.IsArchived ? 0 : 1
                }, transaction);

            schedule.Id = (int)id;
            return schedule.Id;
        }, cancellationToken);
    }

    public Task UpdateAsync(ClassSchedule schedule, CancellationToken cancellationToken = default)
    {
        return store.ExecuteInTransactionAsync(async (connection, transaction) =>
        {
            await EnsureNoDayTimeCollisionAsync(connection, transaction, schedule.Day, schedule.Time, schedule.Id);

            await connection.ExecuteAsync(
                "UPDATE ClassSchedule SET Day=$day, Time=$time, IsActive=$isActive WHERE ClassScheduleId=$id",
                new
                {
                    id = schedule.Id,
                    day = (int)schedule.Day,
                    time = schedule.Time.Hour * 60 + schedule.Time.Minute,
                    isActive = schedule.IsArchived ? 0 : 1
                }, transaction);
        }, cancellationToken);
    }

    private static async Task EnsureNoDayTimeCollisionAsync(Microsoft.Data.Sqlite.SqliteConnection connection,
        Microsoft.Data.Sqlite.SqliteTransaction transaction, DayOfWeek day, TimeOnly time, int? excludeId)
    {
        var collides = await connection.ExecuteScalarAsync<long>(
            """
            SELECT EXISTS(
                SELECT 1 FROM ClassSchedule
                WHERE Day = $day AND Time = $time AND ($excludeId IS NULL OR ClassScheduleId != $excludeId)
            )
            """,
            new { day = (int)day, time = time.Hour * 60 + time.Minute, excludeId }, transaction);

        if (collides == 1)
            throw new InvalidOperationException($"A class schedule already exists for {day} at {time}.");
    }

    public Task ArchiveAsync(int scheduleId, CancellationToken cancellationToken = default)
    {
        return store.ExecuteInTransactionAsync((connection, transaction) =>
            connection.ExecuteAsync("UPDATE ClassSchedule SET IsActive=0 WHERE ClassScheduleId=$id", new { id = scheduleId },
                transaction), cancellationToken);
    }

    public Task UnarchiveAsync(int scheduleId, CancellationToken cancellationToken = default)
    {
        return store.ExecuteInTransactionAsync((connection, transaction) =>
            connection.ExecuteAsync("UPDATE ClassSchedule SET IsActive=1 WHERE ClassScheduleId=$id", new { id = scheduleId },
                transaction), cancellationToken);
    }

    public Task<bool> TryDeleteAsync(int scheduleId, CancellationToken cancellationToken = default)
    {
        return store.ExecuteInTransactionAsync(async (connection, transaction) =>
        {
            var referenced = await connection.ExecuteScalarAsync<long>(
                """
                SELECT EXISTS(SELECT 1 FROM ClassRoll WHERE ClassScheduleId = $id)
                    OR EXISTS(SELECT 1 FROM TermClasses WHERE ClassId = $id)
                """,
                new { id = scheduleId }, transaction);

            if (referenced == 1)
                return false;

            await connection.ExecuteAsync("DELETE FROM ClassSchedule WHERE ClassScheduleId=$id", new { id = scheduleId },
                transaction);
            return true;
        }, cancellationToken);
    }
}

internal sealed class ClassScheduleRow
{
    public int ClassScheduleId { get; set; }
    public int Day { get; set; }
    public int Time { get; set; }
    public int IsActive { get; set; }

    public ClassSchedule ToModel()
    {
        return new ClassSchedule(ClassScheduleId, (DayOfWeek)Day, TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(Time)),
            isArchived: IsActive == 0);
    }
}

internal class SqliteClassScheduleDbModel : IDbModel<ClassSchedule, ClassScheduleFilter>
{
    private readonly SqliteDataStore store;

    public SqliteClassScheduleDbModel(ClassScheduleFilter filter, SqliteDataStore store)
    {
        Filter = filter;
        this.store = store;
    }

    public ClassScheduleFilter Filter { get; init; }

    public async Task<ClassSchedule?> LoadSingle(CancellationToken cancellationToken = default)
    {
        var results = await LoadMultiple(1, 0, cancellationToken);
        return results.FirstOrDefault();
    }

    public async Task<IReadOnlyList<ClassSchedule>> LoadMultiple(uint count = uint.MaxValue, uint skip = 0,
        CancellationToken cancellationToken = default)
    {
        var filter = Filter;
        var take = count > int.MaxValue ? int.MaxValue : (int)count;

        var orderBy = BuildOrderBy(filter.SortBy);

        var sql = $"""
                   SELECT ClassScheduleId, Day, Time, IsActive
                   FROM ClassSchedule
                   WHERE ($id IS NULL OR ClassScheduleId = $id)
                     AND ($day IS NULL OR Day = $day)
                     AND ($timeFrom IS NULL OR Time >= $timeFrom)
                     AND ($timeTo IS NULL OR Time <= $timeTo)
                     AND ($includeArchived = 1 OR IsActive = 1)
                   ORDER BY {orderBy}
                   LIMIT $take OFFSET $skip
                   """;

        var rows = await store.QueryAsync(connection => connection.QueryAsync<ClassScheduleRow>(sql, new
        {
            id = (int?)filter.Id,
            day = (int?)filter.Day,
            timeFrom = filter.TimeFrom is null ? null : (int?)(filter.TimeFrom.Value.Hour * 60 + filter.TimeFrom.Value.Minute),
            timeTo = filter.TimeTo is null ? null : (int?)(filter.TimeTo.Value.Hour * 60 + filter.TimeTo.Value.Minute),
            includeArchived = filter.IncludeArchived ? 1 : 0,
            take,
            skip = (int)skip
        }), cancellationToken);

        return rows.Select(r => r.ToModel()).ToList();
    }

    private static string BuildOrderBy(KeyValuePair<ClassScheduleSortOptions, Order>? sortBy)
    {
        if (sortBy is null)
            return SqlFilterBuilder.AppendPkTiebreaker("Day ASC, Time ASC", "ClassScheduleId");

        var (key, order) = sortBy.Value;
        var direction = StringValueAttribute.GetStringValue(order);

        var column = key switch
        {
            ClassScheduleSortOptions.Day => "Day",
            ClassScheduleSortOptions.Time => "Time",
            _ => "ClassScheduleId"
        };

        return SqlFilterBuilder.AppendPkTiebreaker($"{column} {direction}", "ClassScheduleId");
    }

    public async Task<bool> Refresh(ClassSchedule model, CancellationToken cancellationToken = default)
    {
        var row = await store.QueryAsync(connection => connection.QuerySingleOrDefaultAsync<ClassScheduleRow>(
            "SELECT ClassScheduleId, Day, Time, IsActive FROM ClassSchedule WHERE ClassScheduleId=$id",
            new { id = model.Id }), cancellationToken);

        if (row is null)
            return false;

        model.Day = (DayOfWeek)row.Day;
        model.Time = TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(row.Time));
        model.IsArchived = row.IsActive == 0;
        return true;
    }

    public Task Save(ClassSchedule model, SaveOptions saveOptions = SaveOptions.CreateOrReplace,
        CancellationToken cancellationToken = default)
    {
        return store.ExecuteInTransactionAsync(async (connection, transaction) =>
        {
            var exists = model.Id > 0 && await connection.ExecuteScalarAsync<long>(
                "SELECT EXISTS(SELECT 1 FROM ClassSchedule WHERE ClassScheduleId=$id)", new { id = model.Id },
                transaction) == 1;

            if (exists && saveOptions == SaveOptions.Create)
                throw new ModelExistsException();
            if (!exists && saveOptions == SaveOptions.Replace)
                throw new ModelDoesNotExistException();

            var isActive = model.IsArchived ? 0 : 1;
            var day = (int)model.Day;
            var time = model.Time.Hour * 60 + model.Time.Minute;

            if (exists)
            {
                await connection.ExecuteAsync(
                    "UPDATE ClassSchedule SET Day=$day, Time=$time, IsActive=$isActive WHERE ClassScheduleId=$id",
                    new { id = model.Id, day, time, isActive }, transaction);
            }
            else
            {
                var id = await connection.ExecuteScalarAsync<long>(
                    """
                    INSERT INTO ClassSchedule(Day, Time, IsActive) VALUES ($day, $time, $isActive);
                    SELECT last_insert_rowid();
                    """,
                    new { day, time, isActive }, transaction);
                model.Id = (int)id;
            }
        }, cancellationToken);
    }

    public Task Delete(ClassSchedule model, CancellationToken cancellationToken = default)
    {
        return store.ExecuteInTransactionAsync((connection, transaction) =>
            connection.ExecuteAsync("DELETE FROM ClassSchedule WHERE ClassScheduleId=$id", new { id = model.Id },
                transaction), cancellationToken);
    }
}
