using System.Reactive.Linq;
using YogaClassManager.Avalonia.ViewModels.ClassRolls;
using YogaClassManager.Core.Dummy;
using YogaClassManager.Core.Models.Classes;

namespace YogaClassManager.Avalonia.Tests;

/// <summary>The Class Rolls page lists the last 6 months by default, without that counting as a filter.</summary>
public class ClassRollsDefaultFilterTests
{
    private static async Task<(ClassRollsViewModel ViewModel, int RecentId)> CreateAsync()
    {
        var store = new InMemoryDataStore();
        var schedules = new InMemoryClassScheduleRepository(store);
        var rolls = new InMemoryClassRollRepository(store);
        var scheduleId = await schedules.AddAsync(new ClassSchedule(0, DayOfWeek.Monday, new TimeOnly(9, 0), false));
        var schedule = (await schedules.Query(new() { Id = (uint)scheduleId }).LoadSingle())!;
        var today = DateOnly.FromDateTime(DateTime.Today);
        var recentId = await rolls.AddAsync(new ClassRoll(0, today.AddMonths(-2), schedule, []));
        await rolls.AddAsync(new ClassRoll(0, today.AddMonths(-9), schedule, []));

        var identities = new InMemoryIdentityRepository(store);
        var passes = new InMemoryPassRepository(store);
        var students = new InMemoryStudentRepository(store, identities, passes, new InMemoryEmergencyContactRepository(store));
        var viewModel = new ClassRollsViewModel(new AppScreen(), rolls, students, passes, new InMemoryTermRepository(store),
            new ScriptedDialogService(), new FakeToastService(), new FakeRollWindowService(), new FakeNavigator());
        await viewModel.RefreshCommand.Execute();
        return (viewModel, recentId);
    }

    [Fact]
    public async Task ByDefault_OnlyTheLastSixMonthsAreListed_WithoutCountingAsAFilter()
    {
        var (viewModel, recentId) = await CreateAsync();

        Assert.Equal(DateOnly.FromDateTime(DateTime.Today).AddMonths(-6), viewModel.DateFrom);
        Assert.Equal(0, viewModel.ActiveFilterCount);
        Assert.Equal([recentId], viewModel.Items.Select(r => r.Id));
    }

    [Fact]
    public async Task ClearingTheFromDate_CountsAsAFilter_AndClearFiltersRestoresTheDefault()
    {
        var (viewModel, _) = await CreateAsync();

        viewModel.DateFrom = null;
        Assert.Equal(1, viewModel.ActiveFilterCount);

        viewModel.ClearFiltersCommand.Execute().Subscribe();
        Assert.Equal(DateOnly.FromDateTime(DateTime.Today).AddMonths(-6), viewModel.DateFrom);
        Assert.Equal(0, viewModel.ActiveFilterCount);
    }
}
