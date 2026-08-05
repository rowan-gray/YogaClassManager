using YogaClassManager.Core.Data;
using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models;
using YogaClassManager.Core.Models.People;
using YogaClassManager.Core.Repositories;

namespace YogaClassManager.Core.Dummy;

public class InMemoryIdentityRepository : IIdentityRepository
{
    private readonly InMemoryDataStore store;

    public InMemoryIdentityRepository(InMemoryDataStore store)
    {
        this.store = store;
    }

    public IDbModel<Identity, IdentityFilter> Query(IdentityFilter filter)
    {
        return new InMemoryIdentityDbModel(filter, store);
    }

    public Task<int> AddAsync(Identity identity, CancellationToken cancellationToken = default)
    {
        var id = store.NextId();
        identity.Id = id;
        store.People[id] = identity;
        return Task.FromResult(id);
    }

    public Task UpdateAsync(Identity identity, CancellationToken cancellationToken = default)
    {
        if (store.People.TryGetValue(identity.Id, out var existing))
            existing.Update(identity);
        return Task.CompletedTask;
    }

    public Task<IdentityLinkageSummary> GetLinkageSummaryAsync(int identityId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(BuildLinkageSummary(identityId));
    }

    private IdentityLinkageSummary BuildLinkageSummary(int identityId)
    {
        var isStudent = store.People.TryGetValue(identityId, out var identity) && identity is Student;

        var emergencyContactLinkCount =
            store.EmergencyContactLinks.Count(l => l.EmergencyContactIdentityId == identityId);

        var passCount = store.Passes.Values.Count(p => p.StudentId == identityId);

        var attendanceCount = store.ClassRolls.Values
            .SelectMany(r => r.StudentEntries)
            .Count(e => e.Student.Id == identityId);

        return new IdentityLinkageSummary(isStudent, emergencyContactLinkCount, passCount, attendanceCount);
    }

    public Task<ArchiveResult> ArchiveOrDeleteAsync(int identityId, CancellationToken cancellationToken = default)
    {
        var summary = BuildLinkageSummary(identityId);

        if (!summary.HasAnyLinks)
        {
            store.People.Remove(identityId);
            return Task.FromResult(ArchiveResult.Deleted);
        }

        if (store.People.TryGetValue(identityId, out var identity))
            identity.IsActive = false;

        return Task.FromResult(ArchiveResult.Archived);
    }

    public Task UnarchiveAsync(int identityId, CancellationToken cancellationToken = default)
    {
        if (store.People.TryGetValue(identityId, out var identity))
            identity.IsActive = true;
        return Task.CompletedTask;
    }

    public async Task<MergeResult> MergeAsync(int survivingIdentityId, int duplicateIdentityId,
        CancellationToken cancellationToken = default)
    {
        if (!store.People.TryGetValue(survivingIdentityId, out var survivor))
            throw new InvalidOperationException($"Identity {survivingIdentityId} does not exist.");
        if (!store.People.TryGetValue(duplicateIdentityId, out var duplicate))
            throw new InvalidOperationException($"Identity {duplicateIdentityId} does not exist.");

        if (survivor is Student && duplicate is Student)
            throw new InvalidOperationException(
                "Cannot merge two Students - archive one and re-link their relationships manually instead.");

        var repointedEmergencyContactLinks = RepointEmergencyContactLinks(survivingIdentityId, duplicateIdentityId);

        var repointedPasses = 0;
        var repointedAttendance = 0;

        if (duplicate is Student)
        {
            if (survivor is not Student)
            {
                survivor = new Student(survivor);
                store.People[survivingIdentityId] = survivor;
            }

            var survivorStudent = (Student)survivor;

            foreach (var pass in store.Passes.Values.Where(p => p.StudentId == duplicateIdentityId))
            {
                pass.StudentId = survivingIdentityId;
                repointedPasses++;
            }

            foreach (var roll in store.ClassRolls.Values)
            {
                var matchingEntries = roll.StudentEntries.Where(e => e.Student.Id == duplicateIdentityId).ToList();
                foreach (var entry in matchingEntries)
                {
                    roll.StudentEntries[roll.StudentEntries.IndexOf(entry)] =
                        new ClassRollEntry(survivorStudent, entry.Pass);
                    repointedAttendance++;
                }
            }

            MergeHealthConcerns(survivingIdentityId, duplicateIdentityId);

            // The duplicate's Student-specific data has all been repointed away - demote it back to a
            // plain Identity so the delete-if-unlinked check below can actually delete it.
            store.People[duplicateIdentityId] = Identity.Copy(duplicate);
        }

        await ArchiveOrDeleteAsync(duplicateIdentityId, cancellationToken);

        return new MergeResult(survivingIdentityId, duplicateIdentityId, repointedEmergencyContactLinks,
            repointedAttendance, repointedPasses);
    }

    private int RepointEmergencyContactLinks(int survivingIdentityId, int duplicateIdentityId)
    {
        var touched = 0;
        var updated = new List<EmergencyContactLink>();

        foreach (var link in store.EmergencyContactLinks)
        {
            var newLink = link;

            if (link.EmergencyContactIdentityId == duplicateIdentityId)
                newLink = link with { EmergencyContactIdentityId = survivingIdentityId };
            if (link.StudentId == duplicateIdentityId)
                newLink = newLink with { StudentId = survivingIdentityId };

            if (newLink != link)
                touched++;

            updated.Add(newLink);
        }

        store.EmergencyContactLinks.Clear();
        store.EmergencyContactLinks.AddRange(updated
            .DistinctBy(l => (l.StudentId, l.EmergencyContactIdentityId)));

        return touched;
    }

    private void MergeHealthConcerns(int survivingIdentityId, int duplicateIdentityId)
    {
        var duplicateConcerns = store.HealthConcernLinks.Where(l => l.StudentId == duplicateIdentityId).ToList();
        foreach (var link in duplicateConcerns)
        {
            store.HealthConcernLinks.Remove(link);
            if (store.HealthConcernLinks.All(l => l.StudentId != survivingIdentityId || l.Concern != link.Concern))
                store.HealthConcernLinks.Add(link with { StudentId = survivingIdentityId });
        }
    }
}

internal class InMemoryIdentityDbModel : IDbModel<Identity, IdentityFilter>
{
    private readonly InMemoryDataStore store;

    public InMemoryIdentityDbModel(IdentityFilter filter, InMemoryDataStore store)
    {
        Filter = filter;
        this.store = store;
    }

    public IdentityFilter Filter { get; init; }

    public Task<Identity?> LoadSingle(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Matching().FirstOrDefault());
    }

    public Task<IReadOnlyList<Identity>> LoadMultiple(uint count = uint.MaxValue, uint skip = 0,
        CancellationToken cancellationToken = default)
    {
        var take = count > int.MaxValue ? int.MaxValue : (int)count;
        IReadOnlyList<Identity> result = Matching().Skip((int)skip).Take(take).ToList();
        return Task.FromResult(result);
    }

    public Task<bool> Refresh(Identity model, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(store.People.ContainsKey(model.Id));
    }

    public Task Save(Identity model, SaveOptions saveOptions = SaveOptions.CreateOrReplace,
        CancellationToken cancellationToken = default)
    {
        var exists = store.People.ContainsKey(model.Id);

        if (exists && saveOptions == SaveOptions.Create)
            throw new ModelExistsException();
        if (!exists && saveOptions == SaveOptions.Replace)
            throw new ModelDoesNotExistException();

        if (exists)
            store.People[model.Id].Update(model);
        else
            store.People[model.Id == 0 ? store.NextId() : model.Id] = model;

        return Task.CompletedTask;
    }

    public Task Delete(Identity model, CancellationToken cancellationToken = default)
    {
        store.People.Remove(model.Id);
        return Task.CompletedTask;
    }

    private IEnumerable<Identity> Matching()
    {
        var filter = Filter;

        var query = store.People.Values.Where(p =>
            (filter.Id is null || p.Id == filter.Id.Value) &&
            (filter.NameFilter is null ||
             p.FullName.Contains(filter.NameFilter, StringComparison.OrdinalIgnoreCase) ||
             p.FirstName.StartsWith(filter.NameFilter, StringComparison.OrdinalIgnoreCase) ||
             (p.LastName?.StartsWith(filter.NameFilter, StringComparison.OrdinalIgnoreCase) ?? false)) &&
            (filter.FirstNameFilter is null ||
             p.FirstName.StartsWith(filter.FirstNameFilter, StringComparison.OrdinalIgnoreCase)) &&
            (filter.LastNameFilter is null ||
             (p.LastName?.StartsWith(filter.LastNameFilter, StringComparison.OrdinalIgnoreCase) ?? false)) &&
            (filter.EmailFilter is null ||
             (p.Email?.StartsWith(filter.EmailFilter, StringComparison.OrdinalIgnoreCase) ?? false)) &&
            (filter.PhoneNumberFilter is null ||
             (p.PhoneNumber?.StartsWith(filter.PhoneNumberFilter, StringComparison.OrdinalIgnoreCase) ?? false)) &&
            (filter.IsActive is null || p.IsActive == filter.IsActive.Value));

        return Sort(query, filter.SortBy);
    }

    private static IEnumerable<Identity> Sort(IEnumerable<Identity> identities,
        KeyValuePair<IdentitySortOptions, Order>? sortBy)
    {
        if (sortBy is null)
            return identities.OrderBy(p => p.Id);

        var (key, order) = sortBy.Value;

        Func<Identity, IComparable> selector = key switch
        {
            IdentitySortOptions.FirstName => p => p.FirstName,
            IdentitySortOptions.LastName => p => p.LastName ?? "",
            IdentitySortOptions.PhoneNumber => p => p.PhoneNumber ?? "",
            IdentitySortOptions.Email => p => p.Email ?? "",
            _ => p => p.Id
        };

        return order == Order.Ascending ? identities.OrderBy(selector) : identities.OrderByDescending(selector);
    }
}
