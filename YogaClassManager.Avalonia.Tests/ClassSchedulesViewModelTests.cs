using System.Reactive.Linq;
using YogaClassManager.Avalonia;
using YogaClassManager.Avalonia.Services;
using YogaClassManager.Avalonia.ViewModels.ClassSchedules;
using YogaClassManager.Core.Data;
using YogaClassManager.Core.Dummy;
using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models.Classes;

namespace YogaClassManager.Avalonia.Tests;

/// <summary>
///     Covers the Class Schedules page's own list controls (sort/filter/add), which previously had no
///     UI surface at all beyond a "Show archived" checkbox - and the "Mark new roll" flow, which used
///     to persist a roll before the window opened, orphaning an empty roll whenever the instructor
///     cancelled (UI_REVIEW.md findings D6, U1, U3).
/// </summary>
public class ClassSchedulesViewModelTests
{
    private static (ClassSchedulesViewModel ViewModel, InMemoryDataStore Store, FakeRollWindowService Rolls)
        Create(params ClassSchedule[] schedules)
    {
        var store = new InMemoryDataStore();
        var scheduleRepository = new InMemoryClassScheduleRepository(store);

        // Added through the repository rather than straight into the store's dictionary, so ids come
        // from the store's own counter and don't collide with anything it hands out later.
        foreach (var schedule in schedules)
            scheduleRepository.AddAsync(schedule).GetAwaiter().GetResult();

        var rollRepository = new InMemoryClassRollRepository(store);
        var rollWindows = new FakeRollWindowService();

        var viewModel = new ClassSchedulesViewModel(new AppScreen(), scheduleRepository, rollRepository,
            new DialogService(), new FakeToastService(), rollWindows, new FakeNavigator());

        return (viewModel, store, rollWindows);
    }

    private static ClassSchedule Schedule(DayOfWeek day, int hour, bool archived = false) =>
        new(0, day, new TimeOnly(hour, 0), archived);

    [Fact]
    public void AddClassCommand_IsWiredUp()
    {
        // The command existed and worked, but no button was bound to it - so a class schedule could not
        // be created at all in the running app. The XAML binding is what actually fixes that; this just
        // pins the command's presence and that it doesn't require a selection to be invokable.
        var (viewModel, _, _) = Create();

        Assert.True(viewModel.AddClassCommand.CanExecute.FirstAsync().Wait());
    }

    [Fact]
    public void ActiveFilterCount_CountsArchivedAndDayFilters()
    {
        var (viewModel, _, _) = Create();

        Assert.Equal(0, viewModel.ActiveFilterCount);

        viewModel.IncludeArchived = true;
        Assert.Equal(1, viewModel.ActiveFilterCount);

        viewModel.Day = DayOfWeek.Monday;
        Assert.Equal(2, viewModel.ActiveFilterCount);
    }

    [Fact]
    public void ClearFilters_ResetsBothFilterFields()
    {
        var (viewModel, _, _) = Create();
        viewModel.IncludeArchived = true;
        viewModel.Day = DayOfWeek.Monday;

        viewModel.ClearFiltersCommand.Execute().Subscribe();

        Assert.False(viewModel.IncludeArchived);
        Assert.Null(viewModel.Day);
        Assert.Equal(0, viewModel.ActiveFilterCount);
    }

    [Fact]
    public void SortByOptions_ExcludeTheInternalIdFallbackKey()
    {
        var (viewModel, _, _) = Create();

        Assert.DoesNotContain(ClassScheduleSortOptions.Id, viewModel.SortByOptions);
        Assert.Contains(ClassScheduleSortOptions.Day, viewModel.SortByOptions);
        Assert.Contains(ClassScheduleSortOptions.Time, viewModel.SortByOptions);
    }

    [Fact]
    public async Task DayFilter_NarrowsTheList()
    {
        var (viewModel, _, _) = Create(
            Schedule(DayOfWeek.Monday, 9),
            Schedule(DayOfWeek.Tuesday, 10),
            Schedule(DayOfWeek.Monday, 18));

        await viewModel.RefreshCommand.Execute();
        Assert.Equal(3, viewModel.Items.Count);

        viewModel.Day = DayOfWeek.Monday;
        await viewModel.RefreshCommand.Execute();

        Assert.Equal(2, viewModel.Items.Count);
        Assert.All(viewModel.Items, s => Assert.Equal(DayOfWeek.Monday, s.Day));
    }

    [Fact]
    public async Task SortOrder_ReversesTheList()
    {
        var (viewModel, _, _) = Create(
            Schedule(DayOfWeek.Monday, 9),
            Schedule(DayOfWeek.Friday, 10));

        viewModel.SortBy = ClassScheduleSortOptions.Day;
        viewModel.SortOrder = Order.Ascending;
        await viewModel.RefreshCommand.Execute();
        var ascending = viewModel.Items.Select(s => s.Day).ToList();

        viewModel.SortOrder = Order.Descending;
        await viewModel.RefreshCommand.Execute();
        var descending = viewModel.Items.Select(s => s.Day).ToList();

        Assert.Equal(ascending, descending.AsEnumerable().Reverse());
    }

    [Fact]
    public async Task MarkNewRoll_DoesNotPersistTheRollBeforeTheWindowOpens()
    {
        var (viewModel, store, rollWindows) = Create(Schedule(DayOfWeek.Monday, 9));
        await viewModel.RefreshCommand.Execute();

        await viewModel.MarkNewRollCommand.Execute();

        var opened = Assert.Single(rollWindows.Opened);
        Assert.Equal(0, opened.Id);
        Assert.Empty(store.ClassRolls);
    }

    [Fact]
    public async Task MarkNewRoll_ThenCancel_LeavesNoOrphanRoll()
    {
        var (viewModel, store, rollWindows) = Create(Schedule(DayOfWeek.Monday, 9));
        await viewModel.RefreshCommand.Execute();

        await viewModel.MarkNewRollCommand.Execute();
        // Cancelling is just the window closing without a save.
        rollWindows.OnClosedCallbacks.Single()();

        Assert.Empty(store.ClassRolls);
    }

    [Fact]
    public async Task MarkNewRoll_ReusesAnExistingRollForToday_RatherThanCreatingASecond()
    {
        var (viewModel, store, rollWindows) = Create(Schedule(DayOfWeek.Monday, 9));
        await viewModel.RefreshCommand.Execute();

        var today = DateOnly.FromDateTime(DateTime.Now);
        var existing = new ClassRoll(0, today, viewModel.Items[0], []);
        var existingId = await new InMemoryClassRollRepository(store).AddAsync(existing);

        await viewModel.MarkNewRollCommand.Execute();

        var opened = Assert.Single(rollWindows.Opened);
        Assert.Equal(existingId, opened.Id);
        Assert.Single(store.ClassRolls);
    }
}
