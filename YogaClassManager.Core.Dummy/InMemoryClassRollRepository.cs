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

    public IDbModel<ClassRoll, ClassRollFilter> Query(ClassRollFilter filter)
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

    public Task<IReadOnlyList<ClassAttendanceRecord>> GetAttendanceHistoryAsync(int studentId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ClassAttendanceRecord> history = store.ClassRolls.Values
            .SelectMany(roll => roll.StudentEntries
                .Where(entry => entry.Student.Id == studentId)
                .Select(entry => new ClassAttendanceRecord(roll.Id, roll.Date, roll.ClassSchedule, studentId,
                    entry.Pass?.Id)))
            .OrderByDescending(r => r.Date)
            .ToList();

        return Task.FromResult(history);
    }
}

internal class InMemoryClassRollDbModel : IDbModel<ClassRoll, ClassRollFilter>
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

    public Task Save(ClassRoll model, SaveOptions saveOptions = SaveOptions.CreateOrReplace,
        CancellationToken cancellationToken = default)
    {
        var exists = store.ClassRolls.ContainsKey(model.Id);

        if (exists && saveOptions == SaveOptions.Create)
            throw new ModelExistsException();
        if (!exists && saveOptions == SaveOptions.Replace)
            throw new ModelDoesNotExistException();

        store.ClassRolls[model.Id <= 0 ? store.NextId() : model.Id] = model;
        return Task.CompletedTask;
    }

    public Task Delete(ClassRoll model, CancellationToken cancellationToken = default)
    {
        store.ClassRolls.Remove(model.Id);
        return Task.CompletedTask;
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

        Func<ClassRoll, IComparable> selector = key switch
        {
            ClassRollSortOptions.Time => r => r.ClassSchedule.Time,
            ClassRollSortOptions.DayOfWeek => r => r.ClassSchedule.Day,
            _ => r => r.Date
        };

        return order == Order.Ascending ? rolls.OrderBy(selector) : rolls.OrderByDescending(selector);
    }
}
