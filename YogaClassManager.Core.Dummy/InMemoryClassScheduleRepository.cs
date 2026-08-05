using YogaClassManager.Core.Data;
using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models.Classes;
using YogaClassManager.Core.Repositories;

namespace YogaClassManager.Core.Dummy;

public class InMemoryClassScheduleRepository : IClassScheduleRepository
{
    private readonly InMemoryDataStore store;

    public InMemoryClassScheduleRepository(InMemoryDataStore store)
    {
        this.store = store;
    }

    public IDbModel<ClassSchedule, ClassScheduleFilter> Query(ClassScheduleFilter filter)
    {
        return new InMemoryClassScheduleDbModel(filter, store);
    }

    public Task<int> AddAsync(ClassSchedule schedule, CancellationToken cancellationToken = default)
    {
        EnsureNoDayTimeCollision(schedule, excludeId: null);

        if (schedule.Id <= 0)
            schedule.Id = store.NextId();

        store.ClassSchedules[schedule.Id] = schedule;
        return Task.FromResult(schedule.Id);
    }

    public Task UpdateAsync(ClassSchedule schedule, CancellationToken cancellationToken = default)
    {
        EnsureNoDayTimeCollision(schedule, schedule.Id);

        if (store.ClassSchedules.TryGetValue(schedule.Id, out var existing))
            existing.Update(schedule);

        return Task.CompletedTask;
    }

    private void EnsureNoDayTimeCollision(ClassSchedule schedule, int? excludeId)
    {
        var collides = store.ClassSchedules.Values.Any(s =>
            s.Id != excludeId && s.Day == schedule.Day && s.Time == schedule.Time);

        if (collides)
            throw new InvalidOperationException(
                $"A class schedule already exists for {schedule.Day} at {schedule.Time}.");
    }

    public Task ArchiveAsync(int scheduleId, CancellationToken cancellationToken = default)
    {
        if (store.ClassSchedules.TryGetValue(scheduleId, out var schedule))
            schedule.IsArchived = true;
        return Task.CompletedTask;
    }

    public Task UnarchiveAsync(int scheduleId, CancellationToken cancellationToken = default)
    {
        if (store.ClassSchedules.TryGetValue(scheduleId, out var schedule))
            schedule.IsArchived = false;
        return Task.CompletedTask;
    }

    public Task<bool> TryDeleteAsync(int scheduleId, CancellationToken cancellationToken = default)
    {
        var referencedByRoll = store.ClassRolls.Values.Any(r => r.ClassSchedule.Id == scheduleId);
        var referencedByTerm = store.Terms.Values.Any(t => t.Classes.Any(c => c.ClassSchedule.Id == scheduleId));

        if (referencedByRoll || referencedByTerm)
            return Task.FromResult(false);

        store.ClassSchedules.Remove(scheduleId);
        return Task.FromResult(true);
    }
}

internal class InMemoryClassScheduleDbModel : IDbModel<ClassSchedule, ClassScheduleFilter>
{
    private readonly InMemoryDataStore store;

    public InMemoryClassScheduleDbModel(ClassScheduleFilter filter, InMemoryDataStore store)
    {
        Filter = filter;
        this.store = store;
    }

    public ClassScheduleFilter Filter { get; init; }

    public Task<ClassSchedule?> LoadSingle(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Matching().FirstOrDefault());
    }

    public Task<IReadOnlyList<ClassSchedule>> LoadMultiple(uint count = uint.MaxValue, uint skip = 0,
        CancellationToken cancellationToken = default)
    {
        var take = count > int.MaxValue ? int.MaxValue : (int)count;
        IReadOnlyList<ClassSchedule> result = Matching().Skip((int)skip).Take(take).ToList();
        return Task.FromResult(result);
    }

    public Task<bool> Refresh(ClassSchedule model, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(store.ClassSchedules.ContainsKey(model.Id));
    }

    public Task Save(ClassSchedule model, SaveOptions saveOptions = SaveOptions.CreateOrReplace,
        CancellationToken cancellationToken = default)
    {
        var exists = store.ClassSchedules.ContainsKey(model.Id);

        if (exists && saveOptions == SaveOptions.Create)
            throw new ModelExistsException();
        if (!exists && saveOptions == SaveOptions.Replace)
            throw new ModelDoesNotExistException();

        store.ClassSchedules[model.Id <= 0 ? store.NextId() : model.Id] = model;
        return Task.CompletedTask;
    }

    public Task Delete(ClassSchedule model, CancellationToken cancellationToken = default)
    {
        store.ClassSchedules.Remove(model.Id);
        return Task.CompletedTask;
    }

    private IEnumerable<ClassSchedule> Matching()
    {
        var filter = Filter;

        var query = store.ClassSchedules.Values.Where(s =>
            (filter.Id is null || s.Id == filter.Id.Value) &&
            (filter.Day is null || s.Day == filter.Day.Value) &&
            (filter.TimeFrom is null || s.Time >= filter.TimeFrom.Value) &&
            (filter.TimeTo is null || s.Time <= filter.TimeTo.Value) &&
            (filter.IncludeArchived || !s.IsArchived));

        return Sort(query, filter.SortBy);
    }

    private static IEnumerable<ClassSchedule> Sort(IEnumerable<ClassSchedule> schedules,
        KeyValuePair<ClassScheduleSortOptions, Order>? sortBy)
    {
        if (sortBy is null)
            return schedules.OrderBy(s => s.Day).ThenBy(s => s.Time);

        var (key, order) = sortBy.Value;

        Func<ClassSchedule, IComparable> selector = key switch
        {
            ClassScheduleSortOptions.Day => s => s.Day,
            ClassScheduleSortOptions.Time => s => s.Time,
            _ => s => s.Id
        };

        return order == Order.Ascending ? schedules.OrderBy(selector) : schedules.OrderByDescending(selector);
    }
}
