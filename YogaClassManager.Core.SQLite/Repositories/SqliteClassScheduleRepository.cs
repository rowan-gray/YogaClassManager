using Dapper;
using YogaClassManager.Core.Data;
using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models.Classes;
using YogaClassManager.Core.Repositories;
using YogaClassManager.Core.SQLite.Data;

namespace YogaClassManager.Core.SQLite.Repositories;

public class SqliteClassScheduleRepository : IClassScheduleRepository
{
    private readonly IDataStoreProvider dataStoreProvider;

    public SqliteClassScheduleRepository(IDataStoreProvider dataStoreProvider)
    {
        this.dataStoreProvider = dataStoreProvider;
    }

    public IReadOnlyDbModel<ClassSchedule, ClassScheduleFilter> Query(ClassScheduleFilter filter)
    {
        return new SqliteClassScheduleDbModel(filter, dataStoreProvider);
    }

    public async Task<int> AddAsync(ClassSchedule schedule, CancellationToken cancellationToken = default)
    {
        var store = dataStoreProvider.RetrieveDataStore();
        return await store.ExecuteInTransactionAsync(async (connection, transaction) =>
        {
            await EnsureNoDayTimeCollisionAsync(connection, transaction, schedule.Day, schedule.Time, excludeId: null);

            var id = await connection.ExecuteScalarAsync<long>(
                """
                INSERT INTO ClassSchedule(Day, Time, IsArchived) VALUES ($day, $time, $isArchived);
                SELECT last_insert_rowid();
                """,
                new
                {
                    day = (int)schedule.Day,
                    time = schedule.Time.Hour * 60 + schedule.Time.Minute,
                    isArchived = schedule.IsArchived ? 1 : 0
                }, transaction);

            schedule.Id = (int)id;
            return schedule.Id;
        }, cancellationToken);
    }

    public Task UpdateAsync(ClassSchedule schedule, CancellationToken cancellationToken = default)
    {
        var store = dataStoreProvider.RetrieveDataStore();
        return store.ExecuteInTransactionAsync(async (connection, transaction) =>
        {
            await EnsureNoDayTimeCollisionAsync(connection, transaction, schedule.Day, schedule.Time, schedule.Id);

            await connection.ExecuteAsync(
                "UPDATE ClassSchedule SET Day=$day, Time=$time, IsArchived=$isArchived WHERE ClassScheduleId=$id",
                new
                {
                    id = schedule.Id,
                    day = (int)schedule.Day,
                    time = schedule.Time.Hour * 60 + schedule.Time.Minute,
                    isArchived = schedule.IsArchived ? 1 : 0
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
        var store = dataStoreProvider.RetrieveDataStore();
        return store.ExecuteInTransactionAsync((connection, transaction) =>
            connection.ExecuteAsync("UPDATE ClassSchedule SET IsArchived=1 WHERE ClassScheduleId=$id", new { id = scheduleId },
                transaction), cancellationToken);
    }

    public Task UnarchiveAsync(int scheduleId, CancellationToken cancellationToken = default)
    {
        var store = dataStoreProvider.RetrieveDataStore();
        return store.ExecuteInTransactionAsync((connection, transaction) =>
            connection.ExecuteAsync("UPDATE ClassSchedule SET IsArchived=0 WHERE ClassScheduleId=$id", new { id = scheduleId },
                transaction), cancellationToken);
    }

    public Task<bool> TryDeleteAsync(int scheduleId, CancellationToken cancellationToken = default)
    {
        var store = dataStoreProvider.RetrieveDataStore();
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

/// <summary>
///     ClassSchedule.IsArchived: 1 = archived, 0 = active. Named IsActive before migration 006, though
///     it always held IsArchived (the legacy MAUI app's encoding) - see that migration. Unlike Person,
///     whose IsActive column means what it says.
/// </summary>
internal sealed class ClassScheduleRow
{
    public int ClassScheduleId { get; set; }
    public int Day { get; set; }
    public int Time { get; set; }
    public int IsArchived { get; set; }

    public ClassSchedule ToModel()
    {
        return new ClassSchedule(ClassScheduleId, (DayOfWeek)Day, TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(Time)),
            isArchived: IsArchived != 0);
    }
}

internal class SqliteClassScheduleDbModel : IReadOnlyDbModel<ClassSchedule, ClassScheduleFilter>
{
    private readonly IDataStoreProvider dataStoreProvider;

    public SqliteClassScheduleDbModel(ClassScheduleFilter filter, IDataStoreProvider dataStoreProvider)
    {
        Filter = filter;
        this.dataStoreProvider = dataStoreProvider;
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
        var store = dataStoreProvider.RetrieveDataStore();
        var filter = Filter;
        var take = count > int.MaxValue ? int.MaxValue : (int)count;

        var orderBy = BuildOrderBy(filter.SortBy);

        var sql = $"""
                   SELECT ClassScheduleId, Day, Time, IsArchived
                   FROM ClassSchedule
                   WHERE ($id IS NULL OR ClassScheduleId = $id)
                     AND ($day IS NULL OR Day = $day)
                     AND ($timeFrom IS NULL OR Time >= $timeFrom)
                     AND ($timeTo IS NULL OR Time <= $timeTo)
                     AND ($includeArchived = 1 OR IsArchived = 0)
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
        var store = dataStoreProvider.RetrieveDataStore();
        var row = await store.QueryAsync(connection => connection.QuerySingleOrDefaultAsync<ClassScheduleRow>(
            "SELECT ClassScheduleId, Day, Time, IsArchived FROM ClassSchedule WHERE ClassScheduleId=$id",
            new { id = model.Id }), cancellationToken);

        if (row is null)
            return false;

        model.Day = (DayOfWeek)row.Day;
        model.Time = TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(row.Time));
        model.IsArchived = row.IsArchived != 0;
        return true;
    }
}
