using YogaClassManager.Core.Data;
using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models.Classes;
using YogaClassManager.Core.Models.Passes;
using YogaClassManager.Core.Repositories;

namespace YogaClassManager.Core.Dummy;

public class InMemoryPassRepository : IPassRepository
{
    private readonly InMemoryDataStore store;

    public InMemoryPassRepository(InMemoryDataStore store)
    {
        this.store = store;
    }

    public IDbModel<Pass, PassFilter> Query(PassFilter filter)
    {
        return new InMemoryPassDbModel(filter, store);
    }

    public Task<int> AddAsync(Pass pass, CancellationToken cancellationToken = default)
    {
        if (pass.Id <= 0)
            pass.Id = store.NextId();

        store.Passes[pass.Id] = pass;
        foreach (var alteration in pass.Alterations)
        {
            if (alteration.Id <= 0)
                alteration.Id = store.NextId();
            store.Alterations[alteration.Id] = alteration;
        }

        return Task.FromResult(pass.Id);
    }

    public Task UpdateAsync(Pass pass, CancellationToken cancellationToken = default)
    {
        if (!store.Passes.TryGetValue(pass.Id, out var existing) || existing.GetType() != pass.GetType())
        {
            store.Passes[pass.Id] = pass;
            return Task.CompletedTask;
        }

        existing.ClassesUsed = pass.ClassesUsed;
        existing.Alterations.Clear();
        foreach (var alteration in pass.Alterations)
            existing.Alterations.Add(alteration);

        switch (existing)
        {
            case CasualPass existingCasual when pass is CasualPass incomingCasual:
                existingCasual.ClassCount = incomingCasual.ClassCount;
                break;
            case DatedPass existingDated when pass is DatedPass incomingDated:
                existingDated.ClassCount = incomingDated.ClassCount;
                existingDated.StartDate = incomingDated.StartDate;
                existingDated.EndDate = incomingDated.EndDate;
                break;
            case TermPass existingTerm when pass is TermPass incomingTerm:
                existingTerm.Term = incomingTerm.Term;
                existingTerm.TermClassSchedule = incomingTerm.TermClassSchedule;
                break;
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(int passId, CancellationToken cancellationToken = default)
    {
        store.Passes.Remove(passId);

        foreach (var alterationId in store.Alterations.Where(kvp => kvp.Value.PassId == passId).Select(kvp => kvp.Key)
                     .ToList())
            store.Alterations.Remove(alterationId);

        foreach (var roll in store.ClassRolls.Values)
        foreach (var entry in roll.StudentEntries.Where(e => e.Pass?.Id == passId))
            entry.Pass = null;

        return Task.CompletedTask;
    }

    public Task AddAlterationAsync(PassAlteration alteration, CancellationToken cancellationToken = default)
    {
        if (alteration.Id <= 0)
            alteration.Id = store.NextId();

        store.Alterations[alteration.Id] = alteration;

        if (store.Passes.TryGetValue(alteration.PassId, out var pass))
            pass.Alterations.Add(alteration);

        return Task.CompletedTask;
    }

    public Task RemoveAlterationAsync(int alterationId, CancellationToken cancellationToken = default)
    {
        if (!store.Alterations.TryGetValue(alterationId, out var alteration))
            return Task.CompletedTask;

        store.Alterations.Remove(alterationId);

        if (store.Passes.TryGetValue(alteration.PassId, out var pass))
        {
            var owned = pass.Alterations.FirstOrDefault(a => a.Id == alterationId);
            if (owned is not null)
                pass.Alterations.Remove(owned);
        }

        return Task.CompletedTask;
    }

    public Task<Pass?> GetByIdAsync(int passId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(store.Passes.GetValueOrDefault(passId));
    }

    public Task<IReadOnlyList<ClassAttendanceRecord>> GetUsageHistoryAsync(int passId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ClassAttendanceRecord> history = store.ClassRolls.Values
            .SelectMany(roll => roll.StudentEntries
                .Where(entry => entry.Pass?.Id == passId)
                .Select(entry => new ClassAttendanceRecord(roll.Id, roll.Date, roll.ClassSchedule, entry.Student.Id,
                    entry.Pass?.Id)))
            .OrderByDescending(r => r.Date)
            .ToList();

        return Task.FromResult(history);
    }
}

internal class InMemoryPassDbModel : IDbModel<Pass, PassFilter>
{
    private readonly InMemoryDataStore store;

    public InMemoryPassDbModel(PassFilter filter, InMemoryDataStore store)
    {
        Filter = filter;
        this.store = store;
    }

    public PassFilter Filter { get; init; }

    public Task<Pass?> LoadSingle(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Matching().FirstOrDefault());
    }

    public Task<IReadOnlyList<Pass>> LoadMultiple(uint count = uint.MaxValue, uint skip = 0,
        CancellationToken cancellationToken = default)
    {
        var take = count > int.MaxValue ? int.MaxValue : (int)count;
        IReadOnlyList<Pass> result = Matching().Skip((int)skip).Take(take).ToList();
        return Task.FromResult(result);
    }

    public Task<bool> Refresh(Pass model, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(store.Passes.ContainsKey(model.Id));
    }

    public Task Save(Pass model, SaveOptions saveOptions = SaveOptions.CreateOrReplace,
        CancellationToken cancellationToken = default)
    {
        var exists = store.Passes.ContainsKey(model.Id);

        if (exists && saveOptions == SaveOptions.Create)
            throw new ModelExistsException();
        if (!exists && saveOptions == SaveOptions.Replace)
            throw new ModelDoesNotExistException();

        store.Passes[model.Id <= 0 ? store.NextId() : model.Id] = model;
        return Task.CompletedTask;
    }

    public Task Delete(Pass model, CancellationToken cancellationToken = default)
    {
        store.Passes.Remove(model.Id);
        return Task.CompletedTask;
    }

    private IEnumerable<Pass> Matching()
    {
        var filter = Filter;

        var query = store.Passes.Values.Where(p =>
            (filter.Id is null || p.Id == filter.Id.Value) &&
            (filter.StudentId is null || p.StudentId == filter.StudentId.Value) &&
            filter.Kind switch
            {
                PassKind.Dated => p is DatedPass,
                PassKind.Casual => p is CasualPass,
                PassKind.Term => p is TermPass,
                _ => true
            } &&
            (filter.IncludeExpired || !p.IsExpired) &&
            (filter.IncludeDepleted || !p.IsDepleted));

        return Sort(query, filter.SortBy);
    }

    private static IEnumerable<Pass> Sort(IEnumerable<Pass> passes, KeyValuePair<PassSortOptions, Order>? sortBy)
    {
        if (sortBy is null)
            return passes.OrderBy(p => p.Id);

        var (_, order) = sortBy.Value;
        return order == Order.Ascending ? passes.OrderBy(p => p.Id) : passes.OrderByDescending(p => p.Id);
    }
}
