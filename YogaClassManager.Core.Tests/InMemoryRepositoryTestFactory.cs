using YogaClassManager.Core.Dummy;
using YogaClassManager.Core.Repositories;
using YogaClassManager.Core.Tests.Shared;

namespace YogaClassManager.Core.Tests;

/// <summary>Builds a fresh set of in-memory repositories (backed by a fresh InMemoryDataStore) for
/// one shared-test-base test. Lives here (not in Core.Tests.Shared) since it's the only place that
/// references Core.Dummy - Core.SQLite.Tests must not pick up that dependency transitively.</summary>
public sealed class InMemoryRepositoryTestFactory : IRepositoryTestFactory
{
    private InMemoryDataStore store = null!;

    public Task InitializeAsync()
    {
        store = new InMemoryDataStore();

        var identityRepository = new InMemoryIdentityRepository(store);
        var passRepository = new InMemoryPassRepository(store);
        var emergencyContactRepository = new InMemoryEmergencyContactRepository(store);

        Identities = identityRepository;
        Passes = passRepository;
        EmergencyContacts = emergencyContactRepository;
        Students = new InMemoryStudentRepository(store, identityRepository, passRepository, emergencyContactRepository);
        ClassSchedules = new InMemoryClassScheduleRepository(store);
        ClassRolls = new InMemoryClassRollRepository(store);
        Terms = new InMemoryTermRepository(store);

        return Task.CompletedTask;
    }

    public IClassRollRepository ClassRolls { get; private set; } = null!;
    public IClassScheduleRepository ClassSchedules { get; private set; } = null!;
    public IEmergencyContactRepository EmergencyContacts { get; private set; } = null!;
    public IIdentityRepository Identities { get; private set; } = null!;
    public IPassRepository Passes { get; private set; } = null!;
    public IStudentRepository Students { get; private set; } = null!;
    public ITermRepository Terms { get; private set; } = null!;

    public Task SeedTermClassUsageAsync(int termId, int classScheduleId, int usesCount)
    {
        var term = store.Terms[termId];
        var termClassSchedule = term.Classes.First(c => c.ClassSchedule.Id == classScheduleId);
        termClassSchedule.Uses = usesCount;
        return Task.CompletedTask;
    }

    public Task SeedPassUsageAsync(int passId, int studentId, int usesCount)
    {
        store.Passes[passId].ClassesUsed = usesCount;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
