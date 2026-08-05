using System.Reactive.Linq;
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
}
