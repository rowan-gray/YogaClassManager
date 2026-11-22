using System.Reactive.Linq;
using YogaClassManager.Core.Data;
using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models.People;
using YogaClassManager.Core.Repositories;
using YogaClassManager.Avalonia.ViewModels.Shared;
using YogaClassManager.Core.Dummy;
using YogaClassManager.Core.Models.Classes;

namespace YogaClassManager.Avalonia.Tests;

/// <summary>
///     Covers the save/cancel paradigm for a roll that has never been persisted. "Mark new roll" used
///     to INSERT an empty roll before opening the window, so cancelling orphaned it; the roll is now
///     only created on save (UI_REVIEW.md finding U3).
/// </summary>
public class MarkRollViewModelTests
{
    private static (MarkRollViewModel ViewModel, InMemoryDataStore Store) Create(ClassRoll roll)
    {
        var store = new InMemoryDataStore();
        var identityRepository = new InMemoryIdentityRepository(store);
        var passRepository = new InMemoryPassRepository(store);
        var emergencyContactRepository = new InMemoryEmergencyContactRepository(store);
        var studentRepository = new InMemoryStudentRepository(store, identityRepository, passRepository,
            emergencyContactRepository);

        var viewModel = new MarkRollViewModel(roll, new InMemoryClassRollRepository(store), studentRepository,
            passRepository, new InMemoryTermRepository(store), new FakeToastService());

        return (viewModel, store);
    }

    private static ClassRoll UnsavedRoll() =>
        new(0, new DateOnly(2026, 7, 20), new ClassSchedule(1, DayOfWeek.Monday, new TimeOnly(9, 0), false), []);

    [Fact]
    public void AnUnsavedRoll_StartsClean_SoItCanBeCancelledWithoutAPrompt()
    {
        var (viewModel, store) = Create(UnsavedRoll());

        Assert.False(viewModel.IsDirty);
        Assert.Empty(store.ClassRolls);
    }

    [Fact]
    public async Task SavingAnUnsavedRoll_CreatesItExactlyOnce()
    {
        var (viewModel, store) = Create(UnsavedRoll());

        viewModel.Date = new DateOnly(2026, 7, 27);
        Assert.True(viewModel.IsDirty);

        await viewModel.SaveCommand.Execute();

        var created = Assert.Single(store.ClassRolls.Values);
        Assert.Equal(new DateOnly(2026, 7, 27), created.Date);
        Assert.False(viewModel.IsDirty);
    }

    [Fact]
    public async Task SavingTwice_UpdatesTheSameRoll_RatherThanCreatingASecond()
    {
        var (viewModel, store) = Create(UnsavedRoll());

        viewModel.Date = new DateOnly(2026, 7, 27);
        await viewModel.SaveCommand.Execute();

        // The second save must take the UPDATE path - the view model now holds a real id.
        viewModel.Date = new DateOnly(2026, 7, 28);
        await viewModel.SaveCommand.Execute();

        var roll = Assert.Single(store.ClassRolls.Values);
        Assert.Equal(new DateOnly(2026, 7, 28), roll.Date);
    }

    [Fact]
    public async Task SavingAnAlreadyPersistedRoll_DoesNotCreateADuplicate()
    {
        var store = new InMemoryDataStore();
        var schedule = new ClassSchedule(1, DayOfWeek.Monday, new TimeOnly(9, 0), false);
        var rollRepository = new InMemoryClassRollRepository(store);
        var existingId = await rollRepository.AddAsync(new ClassRoll(0, new DateOnly(2026, 7, 20), schedule, []));

        var identityRepository = new InMemoryIdentityRepository(store);
        var passRepository = new InMemoryPassRepository(store);
        var studentRepository = new InMemoryStudentRepository(store, identityRepository, passRepository,
            new InMemoryEmergencyContactRepository(store));

        var viewModel = new MarkRollViewModel(store.ClassRolls[existingId], rollRepository, studentRepository,
            passRepository, new InMemoryTermRepository(store), new FakeToastService());

        viewModel.Date = new DateOnly(2026, 7, 21);
        await viewModel.SaveCommand.Execute();

        var roll = Assert.Single(store.ClassRolls.Values);
        Assert.Equal(existingId, roll.Id);
        Assert.Equal(new DateOnly(2026, 7, 21), roll.Date);
    }

    // ---- Saving changes to an existing roll ----

    private static async Task<(InMemoryDataStore Store, InMemoryStudentRepository Students, InMemoryPassRepository Passes,
        ClassRoll Roll, Student Alice, Student Bob, Student Carol)> CreatePersistedRollAsync()
    {
        var store = new InMemoryDataStore();
        var passRepository = new InMemoryPassRepository(store);
        var studentRepository = new InMemoryStudentRepository(store, new InMemoryIdentityRepository(store), passRepository,
            new InMemoryEmergencyContactRepository(store));

        async Task<Student> AddStudent(string firstName)
        {
            var id = await studentRepository.AddAsync(new Student(0, firstName, "Tester", "0400000000", null, true));
            return (await studentRepository.Query(new StudentFilter { Id = (uint)id }).LoadSingle())!;
        }

        var alice = await AddStudent("Alice");
        var bob = await AddStudent("Bob");
        var carol = await AddStudent("Carol");

        var schedule = new ClassSchedule(1, DayOfWeek.Monday, new TimeOnly(9, 0), false);
        var roll = new ClassRoll(0, new DateOnly(2026, 7, 20), schedule,
            [new ClassRollEntry(alice, null), new ClassRollEntry(bob, null)]);
        await new InMemoryClassRollRepository(store).AddAsync(roll);

        return (store, studentRepository, passRepository, roll, alice, bob, carol);
    }

    /// <summary>The window is non-modal: someone can add a student to this roll from the Class Rolls page
    /// while it's open. Saving the window applies only its own changes, so that attendee survives.</summary>
    [Fact]
    public async Task Saving_KeepsAnAttendeeAddedElsewhereWhileTheWindowWasOpen()
    {
        var (store, students, passes, roll, alice, bob, carol) = await CreatePersistedRollAsync();
        var rollRepository = new InMemoryClassRollRepository(store);
        // The window works on its own snapshot of the roll, as RollWindowService hands it a freshly loaded one.
        var snapshot = new ClassRoll(roll.Id, roll.Date, roll.ClassSchedule, roll.StudentEntries.ToList());
        var viewModel = new MarkRollViewModel(snapshot, rollRepository, students, passes,
            new InMemoryTermRepository(store), new FakeToastService());

        await rollRepository.AddStudentEntryAsync(roll.Id, new ClassRollEntry(carol, null));
        await viewModel.RemoveStudentCommand.Execute(viewModel.Entries.Single(e => e.Student.Id == bob.Id));
        await viewModel.SaveCommand.Execute();

        var saved = store.ClassRolls[roll.Id];
        Assert.Equal([alice.Id, carol.Id], saved.StudentEntries.Select(e => e.Student.Id).Order().ToArray());
        Assert.False(viewModel.IsDirty);
    }

    /// <summary>A failed save goes through one atomic repository call - never a partial run of per-attendee
    /// calls - and leaves the window dirty so the instructor can retry rather than lose their edits.</summary>
    [Fact]
    public async Task AFailedSave_ChangesNothing_AndLeavesTheWindowDirty()
    {
        var (store, students, passes, roll, alice, bob, _) = await CreatePersistedRollAsync();
        var rollRepository = new FailingApplyChangesRepository(new InMemoryClassRollRepository(store));
        var snapshot = new ClassRoll(roll.Id, roll.Date, roll.ClassSchedule, roll.StudentEntries.ToList());
        var viewModel = new MarkRollViewModel(snapshot, rollRepository, students, passes,
            new InMemoryTermRepository(store), new FakeToastService());
        var errors = new List<Exception>();
        viewModel.SaveCommand.ThrownExceptions.Subscribe(errors.Add);

        viewModel.Date = new DateOnly(2026, 7, 27);
        await viewModel.RemoveStudentCommand.Execute(viewModel.Entries.Single(e => e.Student.Id == bob.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await viewModel.SaveCommand.Execute());

        Assert.Single(errors);
        Assert.Equal(0, rollRepository.PerEntryCalls);
        var stored = store.ClassRolls[roll.Id];
        Assert.Equal(new DateOnly(2026, 7, 20), stored.Date);
        Assert.Equal([alice.Id, bob.Id], stored.StudentEntries.Select(e => e.Student.Id).Order().ToArray());
        Assert.True(viewModel.IsDirty);
    }

    /// <summary>Delegates to a real repository, but ApplyChangesAsync fails (as a database error mid-save
    /// would) and the per-entry write methods are counted, to prove the save no longer uses them.</summary>
    private sealed class FailingApplyChangesRepository(IClassRollRepository inner) : IClassRollRepository
    {
        public int PerEntryCalls { get; private set; }

        public IReadOnlyDbModel<ClassRoll, ClassRollFilter> Query(ClassRollFilter filter) => inner.Query(filter);
        public Task<int> AddAsync(ClassRoll roll, CancellationToken cancellationToken = default) => inner.AddAsync(roll, cancellationToken);
        public Task DeleteAsync(int classRollId, CancellationToken cancellationToken = default) => inner.DeleteAsync(classRollId, cancellationToken);

        public Task<IReadOnlyList<ClassAttendanceRecord>> GetAttendanceHistoryAsync(int studentId,
            ClassRollFilter filter = default, CancellationToken cancellationToken = default) =>
            inner.GetAttendanceHistoryAsync(studentId, filter, cancellationToken);

        public Task UpdateDetailsAsync(ClassRoll roll, CancellationToken cancellationToken = default)
        {
            PerEntryCalls++;
            return inner.UpdateDetailsAsync(roll, cancellationToken);
        }

        public Task AddStudentEntryAsync(int classRollId, ClassRollEntry entry, CancellationToken cancellationToken = default)
        {
            PerEntryCalls++;
            return inner.AddStudentEntryAsync(classRollId, entry, cancellationToken);
        }

        public Task RemoveStudentEntryAsync(int classRollId, int studentId, CancellationToken cancellationToken = default)
        {
            PerEntryCalls++;
            return inner.RemoveStudentEntryAsync(classRollId, studentId, cancellationToken);
        }

        public Task UpdateStudentEntryPassAsync(int classRollId, int studentId, int? passId,
            CancellationToken cancellationToken = default)
        {
            PerEntryCalls++;
            return inner.UpdateStudentEntryPassAsync(classRollId, studentId, passId, cancellationToken);
        }

        public Task ApplyChangesAsync(ClassRoll details, IReadOnlyCollection<int> removedStudentIds,
            IReadOnlyCollection<ClassRollEntry> addedOrChangedEntries, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Simulated database failure");
    }
}
