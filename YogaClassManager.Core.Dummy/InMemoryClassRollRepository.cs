using YogaClassManager.Core.Data;
using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models.Classes;
using YogaClassManager.Core.Models.People;
using YogaClassManager.Core.Repositories;

namespace YogaClassManager.Core.Dummy;

public class InMemoryClassRollRepository : IClassRollRepository
{
    private readonly InMemoryDataStore store;

    public InMemoryClassRollRepository(InMemoryDataStore store)
    {
        this.store = store;
    }

    public IReadOnlyDbModel<ClassRoll, ClassRollFilter> Query(ClassRollFilter filter)
    {
        return new InMemoryClassRollDbModel(filter, store);
    }

    public Task<int> AddAsync(ClassRoll roll, CancellationToken cancellationToken = default)
    {
        if (roll.Id <= 0)
            roll.Id = store.NextId();

        store.ClassRolls[roll.Id] = roll;
        return Task.FromResult(roll.Id);
    }

    public Task UpdateDetailsAsync(ClassRoll roll, CancellationToken cancellationToken = default)
    {
        if (store.ClassRolls.TryGetValue(roll.Id, out var existing))
        {
            existing.Date = roll.Date;
            existing.ClassSchedule = roll.ClassSchedule;
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(int classRollId, CancellationToken cancellationToken = default)
    {
        store.ClassRolls.Remove(classRollId);
        return Task.CompletedTask;
    }

    public Task AddStudentEntryAsync(int classRollId, ClassRollEntry entry, CancellationToken cancellationToken = default)
    {
        if (!store.ClassRolls.TryGetValue(classRollId, out var roll))
            return Task.CompletedTask;

        roll.StudentEntries.RemoveAll(e => e.Student.Id == entry.Student.Id);
        roll.StudentEntries.Add(entry);

        return Task.CompletedTask;
    }

    public Task RemoveStudentEntryAsync(int classRollId, int studentId, CancellationToken cancellationToken = default)
    {
        if (store.ClassRolls.TryGetValue(classRollId, out var roll))
            roll.StudentEntries.RemoveAll(e => e.Student.Id == studentId);

        return Task.CompletedTask;
    }

    public Task UpdateStudentEntryPassAsync(int classRollId, int studentId, int? passId,
        CancellationToken cancellationToken = default)
    {
        if (!store.ClassRolls.TryGetValue(classRollId, out var roll))
            return Task.CompletedTask;

        var entry = roll.StudentEntries.FirstOrDefault(e => e.Student.Id == studentId);
        if (entry is not null)
            entry.Pass = passId is not null ? store.Passes.GetValueOrDefault(passId.Value) : null;

        return Task.CompletedTask;
    }

    public Task ApplyChangesAsync(ClassRoll details, IReadOnlyCollection<int> removedStudentIds,
        IReadOnlyCollection<ClassRollEntry> addedOrChangedEntries, CancellationToken cancellationToken = default)
    {
        // Synchronous with nothing that can fail part-way, so all-or-nothing by construction.
        if (!store.ClassRolls.TryGetValue(details.Id, out var roll))
            return Task.CompletedTask;

        roll.Date = details.Date;
        roll.ClassSchedule = details.ClassSchedule;
        roll.StudentEntries.RemoveAll(e => removedStudentIds.Contains(e.Student.Id));

        foreach (var entry in addedOrChangedEntries)
        {
            roll.StudentEntries.RemoveAll(e => e.Student.Id == entry.Student.Id);
            roll.StudentEntries.Add(entry);
        }

        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<ClassAttendanceRecord>> GetAttendanceHistoryAsync(int studentId,
        ClassRollFilter filter = default, CancellationToken cancellationToken = default)
    {
        // Routed through Query/Matching/Sort - the exact same filtering/ordering the Class Rolls list
        // uses - rather than a parallel implementation, so the two can't drift apart.
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

internal class InMemoryClassRollDbModel : IReadOnlyDbModel<ClassRoll, ClassRollFilter>
{
    private readonly InMemoryDataStore store;

    public InMemoryClassRollDbModel(ClassRollFilter filter, InMemoryDataStore store)
    {
        Filter = filter;
        this.store = store;
    }

    public ClassRollFilter Filter { get; init; }

    public Task<ClassRoll?> LoadSingle(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Matching().FirstOrDefault());
    }

    public Task<IReadOnlyList<ClassRoll>> LoadMultiple(uint count = uint.MaxValue, uint skip = 0,
        CancellationToken cancellationToken = default)
    {
        var take = count > int.MaxValue ? int.MaxValue : (int)count;
        IReadOnlyList<ClassRoll> result = Matching().Skip((int)skip).Take(take).ToList();
        return Task.FromResult(result);
    }

    public Task<bool> Refresh(ClassRoll model, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(store.ClassRolls.ContainsKey(model.Id));
    }

    private IEnumerable<ClassRoll> Matching()
    {
        var filter = Filter;

        var query = store.ClassRolls.Values.Where(r =>
            (filter.Id is null || r.Id == filter.Id.Value) &&
            (filter.ClassScheduleId is null || r.ClassSchedule.Id == filter.ClassScheduleId.Value) &&
            (filter.DateFrom is null || r.Date >= filter.DateFrom.Value) &&
            (filter.DateTo is null || r.Date <= filter.DateTo.Value) &&
            (filter.TimeFrom is null || r.ClassSchedule.Time >= filter.TimeFrom.Value) &&
            (filter.TimeTo is null || r.ClassSchedule.Time <= filter.TimeTo.Value) &&
            (filter.DayOfWeek is null || r.ClassSchedule.Day == filter.DayOfWeek.Value));

        return Sort(query, filter.SortBy);
    }

    private static IEnumerable<ClassRoll> Sort(IEnumerable<ClassRoll> rolls,
        KeyValuePair<ClassRollSortOptions, Order>? sortBy)
    {
        if (sortBy is null)
            return rolls.OrderByDescending(r => r.Date).ThenBy(r => r.ClassSchedule.Time);

        var (key, order) = sortBy.Value;
        var ascending = order == Order.Ascending;

        // Ties break so each sort still reads chronologically (must match SqliteClassRollRepository):
        // Date - same-day classes by time in the same direction (newest first = latest class first);
        // Time - most recent date first; Day of week - time of day, then most recent date first.
        return key switch
        {
            ClassRollSortOptions.Time =>
                (ascending ? rolls.OrderBy(r => r.ClassSchedule.Time) : rolls.OrderByDescending(r => r.ClassSchedule.Time))
                .ThenByDescending(r => r.Date).ThenBy(r => r.Id),
            ClassRollSortOptions.DayOfWeek =>
                (ascending ? rolls.OrderBy(r => r.ClassSchedule.Day) : rolls.OrderByDescending(r => r.ClassSchedule.Day))
                .ThenBy(r => r.ClassSchedule.Time).ThenByDescending(r => r.Date).ThenBy(r => r.Id),
            _ => ascending
                ? rolls.OrderBy(r => r.Date).ThenBy(r => r.ClassSchedule.Time).ThenBy(r => r.Id)
                : rolls.OrderByDescending(r => r.Date).ThenByDescending(r => r.ClassSchedule.Time).ThenBy(r => r.Id)
        };
    }
}
