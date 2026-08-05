using YogaClassManager.Core.Data;
using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models;
using YogaClassManager.Core.Models.Passes;
using YogaClassManager.Core.Models.People;
using YogaClassManager.Core.Repositories;

namespace YogaClassManager.Core.Dummy;

public class InMemoryStudentRepository : IStudentRepository
{
    private readonly InMemoryDataStore store;
    private readonly IIdentityRepository identityRepository;
    private readonly IPassRepository passRepository;
    private readonly IEmergencyContactRepository emergencyContactRepository;

    public InMemoryStudentRepository(InMemoryDataStore store, IIdentityRepository identityRepository,
        IPassRepository passRepository, IEmergencyContactRepository emergencyContactRepository)
    {
        this.store = store;
        this.identityRepository = identityRepository;
        this.passRepository = passRepository;
        this.emergencyContactRepository = emergencyContactRepository;
    }

    public IDbModel<Student, StudentFilter> Query(StudentFilter filter)
    {
        return new InMemoryStudentDbModel(filter, store, passRepository, emergencyContactRepository);
    }

    public Task<int> AddAsync(Student student, CancellationToken cancellationToken = default)
    {
        if (student.Id <= 0)
            student.Id = store.NextId();

        store.People[student.Id] = student;
        student.AttachChildRepositories(passRepository, emergencyContactRepository);
        return Task.FromResult(student.Id);
    }

    public Task UpdateAsync(Student student, CancellationToken cancellationToken = default)
    {
        if (store.People.TryGetValue(student.Id, out var existing) && existing is Student existingStudent)
            existingStudent.Update(student);
        return Task.CompletedTask;
    }

    public Task<int> PromoteToStudentAsync(Student student, CancellationToken cancellationToken = default)
    {
        if (!store.People.TryGetValue(student.Id, out var existing))
            throw new InvalidOperationException($"Identity {student.Id} does not exist.");
        if (existing is Student)
            throw new InvalidOperationException($"Identity {student.Id} is already a Student.");

        store.People[student.Id] = student;
        student.AttachChildRepositories(passRepository, emergencyContactRepository);
        return Task.FromResult(student.Id);
    }

    public Task<ArchiveResult> ArchiveOrDeleteAsync(int studentId, CancellationToken cancellationToken = default)
    {
        return identityRepository.ArchiveOrDeleteAsync(studentId, cancellationToken);
    }

    public Task UnarchiveAsync(int studentId, CancellationToken cancellationToken = default)
    {
        return identityRepository.UnarchiveAsync(studentId, cancellationToken);
    }

    public Task LinkEmergencyContactAsync(int studentId, int identityId, Relationship relationship,
        CancellationToken cancellationToken = default)
    {
        store.EmergencyContactLinks.RemoveAll(l => l.StudentId == studentId && l.EmergencyContactIdentityId == identityId);
        store.EmergencyContactLinks.Add(new EmergencyContactLink(studentId, identityId, relationship));
        return Task.CompletedTask;
    }

    public Task UnlinkEmergencyContactAsync(int studentId, int identityId, CancellationToken cancellationToken = default)
    {
        store.EmergencyContactLinks.RemoveAll(l => l.StudentId == studentId && l.EmergencyContactIdentityId == identityId);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<EmergencyContact>> GetEmergencyContactsAsync(int studentId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<EmergencyContact> contacts = store.EmergencyContactLinks
            .Where(l => l.StudentId == studentId)
            .Where(l => store.People.ContainsKey(l.EmergencyContactIdentityId))
            .Select(l => new EmergencyContact(store.People[l.EmergencyContactIdentityId], studentId, l.Relationship))
            .ToList();

        return Task.FromResult(contacts);
    }

    public Task AddHealthConcernAsync(int studentId, string concern, CancellationToken cancellationToken = default)
    {
        if (store.HealthConcernLinks.All(l => l.StudentId != studentId || l.Concern != concern))
            store.HealthConcernLinks.Add(new HealthConcernLink(studentId, concern));
        return Task.CompletedTask;
    }

    public Task RemoveHealthConcernAsync(int studentId, string concern, CancellationToken cancellationToken = default)
    {
        store.HealthConcernLinks.RemoveAll(l => l.StudentId == studentId && l.Concern == concern);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> GetHealthConcernsAsync(int studentId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> concerns = store.HealthConcernLinks
            .Where(l => l.StudentId == studentId)
            .Select(l => l.Concern)
            .ToList();

        return Task.FromResult(concerns);
    }

    public Task<IReadOnlyList<Pass>> GetPassesAsync(int studentId, bool includeExpired = false,
        bool includeDepleted = false, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Pass> passes = store.Passes.Values
            .Where(p => p.StudentId == studentId)
            .Where(p => includeExpired || !p.IsExpired)
            .Where(p => includeDepleted || !p.IsDepleted)
            .ToList();

        return Task.FromResult(passes);
    }
}

internal class InMemoryStudentDbModel : IDbModel<Student, StudentFilter>
{
    private readonly InMemoryDataStore store;
    private readonly IPassRepository passRepository;
    private readonly IEmergencyContactRepository emergencyContactRepository;

    public InMemoryStudentDbModel(StudentFilter filter, InMemoryDataStore store, IPassRepository passRepository,
        IEmergencyContactRepository emergencyContactRepository)
    {
        Filter = filter;
        this.store = store;
        this.passRepository = passRepository;
        this.emergencyContactRepository = emergencyContactRepository;
    }

    public StudentFilter Filter { get; init; }

    public Task<Student?> LoadSingle(CancellationToken cancellationToken = default)
    {
        var student = Matching().FirstOrDefault();
        student?.AttachChildRepositories(passRepository, emergencyContactRepository);
        return Task.FromResult(student);
    }

    public Task<IReadOnlyList<Student>> LoadMultiple(uint count = uint.MaxValue, uint skip = 0,
        CancellationToken cancellationToken = default)
    {
        var take = count > int.MaxValue ? int.MaxValue : (int)count;
        IReadOnlyList<Student> result = Matching().Skip((int)skip).Take(take).ToList();

        foreach (var student in result)
            student.AttachChildRepositories(passRepository, emergencyContactRepository);

        return Task.FromResult(result);
    }

    public Task<bool> Refresh(Student model, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(store.People.ContainsKey(model.Id));
    }

    public Task Save(Student model, SaveOptions saveOptions = SaveOptions.CreateOrReplace,
        CancellationToken cancellationToken = default)
    {
        var exists = store.People.ContainsKey(model.Id);

        if (exists && saveOptions == SaveOptions.Create)
            throw new ModelExistsException();
        if (!exists && saveOptions == SaveOptions.Replace)
            throw new ModelDoesNotExistException();

        store.People[model.Id <= 0 ? store.NextId() : model.Id] = model;
        return Task.CompletedTask;
    }

    public Task Delete(Student model, CancellationToken cancellationToken = default)
    {
        store.People.Remove(model.Id);
        return Task.CompletedTask;
    }

    private IEnumerable<Student> Matching()
    {
        var filter = Filter;
        var lastAttendance = filter.LastAttendedFrom is not null || filter.LastAttendedTo is not null
            ? BuildLastAttendanceLookup()
            : null;

        var query = store.People.Values.OfType<Student>().Where(s =>
            (filter.Id is null || s.Id == filter.Id.Value) &&
            (filter.IsActive is null || s.IsActive == filter.IsActive.Value) &&
            (filter.ExcludeIds is null || !filter.ExcludeIds.Contains(s.Id)) &&
            (filter.NameFilter is null ||
             s.FullName.Contains(filter.NameFilter, StringComparison.OrdinalIgnoreCase) ||
             s.FirstName.StartsWith(filter.NameFilter, StringComparison.OrdinalIgnoreCase) ||
             (s.LastName?.StartsWith(filter.NameFilter, StringComparison.OrdinalIgnoreCase) ?? false)) &&
            (lastAttendance is null ||
             (lastAttendance.TryGetValue(s.Id, out var lastDate) &&
              (filter.LastAttendedFrom is null || lastDate >= filter.LastAttendedFrom.Value) &&
              (filter.LastAttendedTo is null || lastDate <= filter.LastAttendedTo.Value))));

        return Sort(query, filter.SortBy);
    }

    /// <summary>Most recent attendance date per student, computed from every ClassRoll's
    /// StudentEntries - there's no dedicated "last attendance" query yet, only full history per
    /// student (IClassRollRepository.GetAttendanceHistoryAsync), so this scans the store directly,
    /// the same way InMemoryIdentityRepository.BuildLinkageSummary derives its counts.</summary>
    private Dictionary<int, DateOnly> BuildLastAttendanceLookup()
    {
        return store.ClassRolls.Values
            .SelectMany(r => r.StudentEntries.Select(e => (StudentId: e.Student.Id, r.Date)))
            .GroupBy(x => x.StudentId)
            .ToDictionary(g => g.Key, g => g.Max(x => x.Date));
    }

    private IEnumerable<Student> Sort(IEnumerable<Student> students,
        KeyValuePair<StudentSortOptions, Order>? sortBy)
    {
        if (sortBy is null)
            return students.OrderBy(s => s.FirstName);

        var (key, order) = sortBy.Value;

        if (key == StudentSortOptions.LastAttendance)
        {
            var lastAttendance = BuildLastAttendanceLookup();

            // Students who've never attended always sort last, regardless of direction - there's no
            // meaningful "ascending"/"descending" position for "never".
            var withDate = students.Where(s => lastAttendance.ContainsKey(s.Id));
            var withoutDate = students.Where(s => !lastAttendance.ContainsKey(s.Id));

            var sortedWithDate = order == Order.Ascending
                ? withDate.OrderBy(s => lastAttendance[s.Id])
                : withDate.OrderByDescending(s => lastAttendance[s.Id]);

            return sortedWithDate.Concat(withoutDate);
        }

        Func<Student, IComparable> selector = key switch
        {
            StudentSortOptions.FirstName => s => s.FirstName,
            StudentSortOptions.LastName => s => s.LastName ?? "",
            _ => s => s.Id
        };

        return order == Order.Ascending ? students.OrderBy(selector) : students.OrderByDescending(selector);
    }
}
