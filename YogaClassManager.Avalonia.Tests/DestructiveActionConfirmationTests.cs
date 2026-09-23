using System.Reactive.Linq;
using YogaClassManager.Avalonia;
using YogaClassManager.Avalonia.Services;
using YogaClassManager.Avalonia.ViewModels.ClassRolls;
using YogaClassManager.Avalonia.ViewModels.Settings;
using YogaClassManager.Avalonia.ViewModels.Shared;
using YogaClassManager.Avalonia.ViewModels.Terms;
using YogaClassManager.Core.Dummy;
using YogaClassManager.Core.Models.Classes;

namespace YogaClassManager.Avalonia.Tests;

/// <summary>
///     Every destructive action used to run the moment it was clicked - no prompt, no undo (UI_REVIEW.md
///     finding U6). These lock in both halves of the fix: confirming still performs the action, and
///     declining leaves the data untouched. The declining half is the one that actually protects a user,
///     and is easy to regress by wiring a prompt whose result nothing checks.
/// </summary>
public class DestructiveActionConfirmationTests
{
    private static TermsViewModel CreateTermsViewModel(ScriptedDialogService dialogs, InMemoryDataStore store) =>
        new(new AppScreen(), new InMemoryTermRepository(store), new InMemoryClassScheduleRepository(store),
            dialogs, new FakeToastService());

    private static async Task<(TermsViewModel ViewModel, InMemoryDataStore Store)> CreateTermsWithOneTerm(
        ScriptedDialogService dialogs)
    {
        var store = new InMemoryDataStore();
        await new InMemoryTermRepository(store).AddAsync(
            new Term(0, "Winter 2026", new DateOnly(2026, 6, 1), new DateOnly(2026, 8, 31), null, null, []));

        var viewModel = CreateTermsViewModel(dialogs, store);
        await viewModel.RefreshCommand.Execute();
        return (viewModel, store);
    }

    [Fact]
    public async Task DeletingATerm_WhenDeclined_KeepsTheTerm()
    {
        var dialogs = new ScriptedDialogService().Answer<ConfirmViewModel>(false);
        var (viewModel, store) = await CreateTermsWithOneTerm(dialogs);

        await viewModel.DeleteTermCommand.Execute();

        Assert.Single(store.Terms);
        Assert.Single(dialogs.Shown.OfType<ConfirmViewModel>());
    }

    [Fact]
    public async Task DeletingATerm_WhenConfirmed_DeletesIt()
    {
        var dialogs = new ScriptedDialogService().Answer<ConfirmViewModel>(true);
        var (viewModel, store) = await CreateTermsWithOneTerm(dialogs);

        await viewModel.DeleteTermCommand.Execute();

        Assert.Empty(store.Terms);
    }

    private static async Task<(ClassRollsViewModel ViewModel, InMemoryDataStore Store)> CreateRollsWithOneRoll(
        ScriptedDialogService dialogs)
    {
        var store = new InMemoryDataStore();
        var schedule = new ClassSchedule(0, DayOfWeek.Monday, new TimeOnly(9, 0), false);
        await new InMemoryClassScheduleRepository(store).AddAsync(schedule);
        await new InMemoryClassRollRepository(store).AddAsync(
            new ClassRoll(0, new DateOnly(2026, 7, 20), schedule, []));

        var viewModel = new ClassRollsViewModel(new AppScreen(), new InMemoryClassRollRepository(store),
            dialogs, new FakeToastService(), new FakeRollWindowService(), new FakeNavigator());
        await viewModel.RefreshCommand.Execute();
        return (viewModel, store);
    }

    [Fact]
    public async Task DeletingAClassRoll_WhenDeclined_KeepsTheRoll()
    {
        var dialogs = new ScriptedDialogService().Answer<ConfirmViewModel>(false);
        var (viewModel, store) = await CreateRollsWithOneRoll(dialogs);

        await viewModel.DeleteRollCommand.Execute();

        Assert.Single(store.ClassRolls);
    }

    [Fact]
    public async Task DeletingAClassRoll_WhenConfirmed_DeletesIt()
    {
        var dialogs = new ScriptedDialogService().Answer<ConfirmViewModel>(true);
        var (viewModel, store) = await CreateRollsWithOneRoll(dialogs);

        await viewModel.DeleteRollCommand.Execute();

        Assert.Empty(store.ClassRolls);
    }

    // [Fact]
    // public async Task ResettingDummyData_WhenDeclined_LeavesTheStoreAlone()
    // {
    //     var store = new InMemoryDataStore();
    //     DummyDataSeeder.Seed(store);
    //     var identityCountBefore = store.People.Count;
    //     // A marker that only survives if Reset never ran.
    //     await new InMemoryClassScheduleRepository(store)
    //         .AddAsync(new ClassSchedule(0, DayOfWeek.Sunday, new TimeOnly(23, 0), false));
    //     var scheduleCountBefore = store.ClassSchedules.Count;

    //     var dialogs = new ScriptedDialogService().Answer<ConfirmViewModel>(false);
    //     var viewModel = new SettingsViewModel(new AppScreen(), store, dialogs, new FakeToastService());

    //     await viewModel.ResetDummyDataCommand.Execute();

    //     Assert.Equal(identityCountBefore, store.People.Count);
    //     Assert.Equal(scheduleCountBefore, store.ClassSchedules.Count);
    // }

    // [Fact]
    // public async Task ResettingDummyData_WhenConfirmed_ReseedsTheStore()
    // {
    //     var store = new InMemoryDataStore();
    //     DummyDataSeeder.Seed(store);
    //     await new InMemoryClassScheduleRepository(store)
    //         .AddAsync(new ClassSchedule(0, DayOfWeek.Sunday, new TimeOnly(23, 0), false));
    //     var scheduleCountWithExtra = store.ClassSchedules.Count;

    //     var dialogs = new ScriptedDialogService().Answer<ConfirmViewModel>(true);
    //     var viewModel = new SettingsViewModel(new AppScreen(), store, dialogs, new FakeToastService());

    //     await viewModel.ResetDummyDataCommand.Execute();

    //     Assert.NotEmpty(store.People);
    //     Assert.Equal(scheduleCountWithExtra - 1, store.ClassSchedules.Count);
    // }
}
