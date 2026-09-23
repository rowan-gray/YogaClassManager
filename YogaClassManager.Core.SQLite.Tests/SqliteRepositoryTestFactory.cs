using System.Collections.ObjectModel;
using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models.Classes;
using YogaClassManager.Core.Models.Passes;
using YogaClassManager.Core.Models.People;
using YogaClassManager.Core.Repositories;
using YogaClassManager.Core.SQLite.Data;
using YogaClassManager.Core.SQLite.Repositories;
using YogaClassManager.Core.Tests.Shared;

namespace YogaClassManager.Core.SQLite.Tests;

/// <summary>Builds a fresh set of SQLite-backed repositories, against a uniquely-named in-memory
/// shared-cache database with the schema migrator already run, for one shared-test-base test.</summary>
public sealed class SqliteRepositoryTestFactory : IRepositoryTestFactory
{
    private SqliteDataStore store = null!;

    public async Task InitializeAsync()
    {
        store = await SqliteTestDatabaseFactory.CreateAsync();
        var dataStoreProvider = new FixedDataStoreProvider(store);

        var identityRepository = new SqliteIdentityRepository(dataStoreProvider);
        var passRepository = new SqlitePassRepository(dataStoreProvider);
        var emergencyContactRepository = new SqliteEmergencyContactRepository(dataStoreProvider);

        Identities = identityRepository;
        Passes = passRepository;
        EmergencyContacts = emergencyContactRepository;
        Students = new SqliteStudentRepository(dataStoreProvider, identityRepository, passRepository, emergencyContactRepository);
        ClassSchedules = new SqliteClassScheduleRepository(dataStoreProvider);
        ClassRolls = new SqliteClassRollRepository(dataStoreProvider);
        Terms = new SqliteTermRepository(dataStoreProvider);
    }

    public IClassRollRepository ClassRolls { get; private set; } = null!;
    public IClassScheduleRepository ClassSchedules { get; private set; } = null!;
    public IEmergencyContactRepository EmergencyContacts { get; private set; } = null!;
    public IIdentityRepository Identities { get; private set; } = null!;
    public IPassRepository Passes { get; private set; } = null!;
    public IStudentRepository Students { get; private set; } = null!;
    public ITermRepository Terms { get; private set; } = null!;

    public async Task SeedTermClassUsageAsync(int termId, int classScheduleId, int usesCount)
    {
        var term = (await Terms.Query(new TermFilter { Id = (uint)termId, IncludeCompleted = true }).LoadSingle())!;
        var termClassSchedule = term.Classes.First(c => c.ClassSchedule.Id == classScheduleId);

        for (var i = 0; i < usesCount; i++)
        {
            var studentId = await Students.AddAsync(
                new Student(0, $"Filler{i}", "Student", null, $"filler{Guid.NewGuid():N}@example.com", true));

            var pass = new TermPass(0, studentId, 0, new ObservableCollection<PassAlteration>(), term, termClassSchedule);
            await Passes.AddAsync(pass);
        }
    }

    public async Task SeedPassUsageAsync(int passId, int studentId, int usesCount)
    {
        if (usesCount <= 0)
            return;

        var scheduleId = await ClassSchedules.AddAsync(
            new ClassSchedule(0, (DayOfWeek)Random.Shared.Next(0, 7), new TimeOnly(Random.Shared.Next(0, 1439)), false));
        var schedule = (await ClassSchedules.Query(new ClassScheduleFilter { Id = (uint)scheduleId }).LoadSingle())!;
        var student = (await Students.Query(new StudentFilter { Id = (uint)studentId }).LoadSingle())!;
        var pass = await Passes.GetByIdAsync(passId);

        var today = DateOnly.FromDateTime(DateTime.Now);
        for (var i = 0; i < usesCount; i++)
            await ClassRolls.AddAsync(new ClassRoll(0, today.AddDays(-i), schedule, [new ClassRollEntry(student, pass)]));
    }

    public ValueTask DisposeAsync()
    {
        return store.DisposeAsync();
    }
}
