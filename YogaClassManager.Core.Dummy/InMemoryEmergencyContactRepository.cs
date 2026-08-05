using YogaClassManager.Core.Data;
using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models.People;
using YogaClassManager.Core.Repositories;

namespace YogaClassManager.Core.Dummy;

public class InMemoryEmergencyContactRepository : IEmergencyContactRepository
{
    private readonly InMemoryDataStore store;

    public InMemoryEmergencyContactRepository(InMemoryDataStore store)
    {
        this.store = store;
    }

    public IDbModel<EmergencyContact, EmergencyContactFilter> Query(EmergencyContactFilter filter)
    {
        return new InMemoryEmergencyContactDbModel(filter, store);
    }
}

internal class InMemoryEmergencyContactDbModel : IDbModel<EmergencyContact, EmergencyContactFilter>
{
    private readonly InMemoryDataStore store;

    public InMemoryEmergencyContactDbModel(EmergencyContactFilter filter, InMemoryDataStore store)
    {
        Filter = filter;
        this.store = store;
    }

    public EmergencyContactFilter Filter { get; init; }

    public Task<EmergencyContact?> LoadSingle(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Matching().FirstOrDefault());
    }

    public Task<IReadOnlyList<EmergencyContact>> LoadMultiple(uint count = uint.MaxValue, uint skip = 0,
        CancellationToken cancellationToken = default)
    {
        var take = count > int.MaxValue ? int.MaxValue : (int)count;
        IReadOnlyList<EmergencyContact> result = Matching().Skip((int)skip).Take(take).ToList();
        return Task.FromResult(result);
    }

    public Task<bool> Refresh(EmergencyContact model, CancellationToken cancellationToken = default)
    {
        var exists = store.EmergencyContactLinks.Any(l =>
            l.StudentId == model.StudentId && l.EmergencyContactIdentityId == model.Id);
        return Task.FromResult(exists);
    }

    public Task Save(EmergencyContact model, SaveOptions saveOptions = SaveOptions.CreateOrReplace,
        CancellationToken cancellationToken = default)
    {
        var exists = store.EmergencyContactLinks.Any(l =>
            l.StudentId == model.StudentId && l.EmergencyContactIdentityId == model.Id);

        if (exists && saveOptions == SaveOptions.Create)
            throw new ModelExistsException();
        if (!exists && saveOptions == SaveOptions.Replace)
            throw new ModelDoesNotExistException();

        store.EmergencyContactLinks.RemoveAll(l =>
            l.StudentId == model.StudentId && l.EmergencyContactIdentityId == model.Id);
        store.EmergencyContactLinks.Add(new EmergencyContactLink(model.StudentId, model.Id, model.Relationship));

        return Task.CompletedTask;
    }

    public Task Delete(EmergencyContact model, CancellationToken cancellationToken = default)
    {
        store.EmergencyContactLinks.RemoveAll(l =>
            l.StudentId == model.StudentId && l.EmergencyContactIdentityId == model.Id);
        return Task.CompletedTask;
    }

    private IEnumerable<EmergencyContact> Matching()
    {
        var filter = Filter;

        var query = store.EmergencyContactLinks
            .Where(l => filter.StudentId is null || l.StudentId == filter.StudentId.Value)
            .Where(l => filter.Id is null || l.EmergencyContactIdentityId == filter.Id.Value)
            .Where(l => store.People.ContainsKey(l.EmergencyContactIdentityId))
            .Select(l => new EmergencyContact(store.People[l.EmergencyContactIdentityId], l.StudentId, l.Relationship));

        return Sort(query, filter.SortBy);
    }

    private static IEnumerable<EmergencyContact> Sort(IEnumerable<EmergencyContact> contacts,
        KeyValuePair<EmergencyContactSortOptions, Order>? sortBy)
    {
        if (sortBy is null)
            return contacts.OrderBy(c => c.Id);

        var (key, order) = sortBy.Value;

        Func<EmergencyContact, IComparable> selector = key switch
        {
            EmergencyContactSortOptions.FirstName => c => c.FirstName,
            EmergencyContactSortOptions.LastName => c => c.LastName ?? "",
            _ => c => c.Id
        };

        return order == Order.Ascending ? contacts.OrderBy(selector) : contacts.OrderByDescending(selector);
    }
}
