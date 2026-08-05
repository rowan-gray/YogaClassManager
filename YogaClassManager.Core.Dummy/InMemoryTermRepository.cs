using YogaClassManager.Core.Data;
using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models.Classes;
using YogaClassManager.Core.Repositories;

namespace YogaClassManager.Core.Dummy;

public class InMemoryTermRepository : ITermRepository
{
    private readonly InMemoryDataStore store;

    public InMemoryTermRepository(InMemoryDataStore store)
    {
        this.store = store;
    }

    public IDbModel<Term, TermFilter> Query(TermFilter filter)
    {
        return new InMemoryTermDbModel(filter, store);
    }

    public Task<int> AddAsync(Term term, CancellationToken cancellationToken = default)
    {
        if (term.Id <= 0)
            term.Id = store.NextId();

        store.Terms[term.Id] = term;
        return Task.FromResult(term.Id);
    }

    public Task UpdateAsync(Term term, CancellationToken cancellationToken = default)
    {
        if (store.Terms.TryGetValue(term.Id, out var existing))
            existing.Update(term);
        return Task.CompletedTask;
    }

    public Task<bool> TryDeleteAsync(int termId, CancellationToken cancellationToken = default)
    {
        if (!store.Terms.TryGetValue(termId, out var term) || term.Classes.Count > 0)
            return Task.FromResult(false);

        store.Terms.Remove(termId);
        return Task.FromResult(true);
    }

    public Task LinkClassAsync(int termId, int classScheduleId, int classCount,
        CancellationToken cancellationToken = default)
    {
        if (!store.Terms.TryGetValue(termId, out var term) ||
            !store.ClassSchedules.TryGetValue(classScheduleId, out var schedule))
            return Task.CompletedTask;

        var existing = term.Classes.FirstOrDefault(c => c.ClassSchedule.Id == classScheduleId);
        if (existing is not null)
            existing.ClassCount = classCount;
        else
            term.Classes.Add(new TermClassSchedule(schedule, classCount, 0));

        return Task.CompletedTask;
    }

    public Task<bool> UnlinkClassAsync(int termId, int classScheduleId, CancellationToken cancellationToken = default)
    {
        if (!store.Terms.TryGetValue(termId, out var term))
            return Task.FromResult(false);

        var existing = term.Classes.FirstOrDefault(c => c.ClassSchedule.Id == classScheduleId);
        if (existing is null || existing.Uses > 0)
            return Task.FromResult(false);

        term.Classes.Remove(existing);
        return Task.FromResult(true);
    }
}

internal class InMemoryTermDbModel : IDbModel<Term, TermFilter>
{
    private readonly InMemoryDataStore store;

    public InMemoryTermDbModel(TermFilter filter, InMemoryDataStore store)
    {
        Filter = filter;
        this.store = store;
    }

    public TermFilter Filter { get; init; }

    public Task<Term?> LoadSingle(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Matching().FirstOrDefault());
    }

    public Task<IReadOnlyList<Term>> LoadMultiple(uint count = uint.MaxValue, uint skip = 0,
        CancellationToken cancellationToken = default)
    {
        var take = count > int.MaxValue ? int.MaxValue : (int)count;
        IReadOnlyList<Term> result = Matching().Skip((int)skip).Take(take).ToList();
        return Task.FromResult(result);
    }

    public Task<bool> Refresh(Term model, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(store.Terms.ContainsKey(model.Id));
    }

    public Task Save(Term model, SaveOptions saveOptions = SaveOptions.CreateOrReplace,
        CancellationToken cancellationToken = default)
    {
        var exists = store.Terms.ContainsKey(model.Id);

        if (exists && saveOptions == SaveOptions.Create)
            throw new ModelExistsException();
        if (!exists && saveOptions == SaveOptions.Replace)
            throw new ModelDoesNotExistException();

        store.Terms[model.Id <= 0 ? store.NextId() : model.Id] = model;
        return Task.CompletedTask;
    }

    public Task Delete(Term model, CancellationToken cancellationToken = default)
    {
        store.Terms.Remove(model.Id);
        return Task.CompletedTask;
    }

    private IEnumerable<Term> Matching()
    {
        var filter = Filter;

        var query = store.Terms.Values.Where(t =>
            (filter.Id is null || t.Id == filter.Id.Value) &&
            (filter.NameFilter is null || t.Name.Contains(filter.NameFilter, StringComparison.OrdinalIgnoreCase)) &&
            (filter.IncludeCompleted || !t.IsCompleted));

        return Sort(query, filter.SortBy);
    }

    private static IEnumerable<Term> Sort(IEnumerable<Term> terms, KeyValuePair<TermSortOptions, Order>? sortBy)
    {
        if (sortBy is null)
            return terms.OrderByDescending(t => t.StartDate);

        var (key, order) = sortBy.Value;

        Func<Term, IComparable> selector = key switch
        {
            TermSortOptions.Name => t => t.Name,
            TermSortOptions.EndDate => t => t.EndDate,
            TermSortOptions.StartDate => t => t.StartDate,
            _ => t => t.Id
        };

        return order == Order.Ascending ? terms.OrderBy(selector) : terms.OrderByDescending(selector);
    }
}
