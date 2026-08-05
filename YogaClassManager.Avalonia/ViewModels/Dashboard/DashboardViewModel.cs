using System.Collections.ObjectModel;
using System.Reactive;
using ReactiveUI;
using YogaClassManager.Avalonia.Services;
using YogaClassManager.Avalonia.ViewModels.Shared;
using YogaClassManager.Core.Data;
using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models.Classes;
using YogaClassManager.Core.Repositories;

namespace YogaClassManager.Avalonia.ViewModels.Dashboard;

/// <summary>
///     Landing page showing today's classes at a glance, mirroring the MAUI app's MainPageModel.
/// </summary>
public class DashboardViewModel : ViewModelBase, IRoutableViewModel
{
    private readonly IClassRollRepository classRollRepository;
    private readonly IStudentRepository studentRepository;
    private readonly IRollWindowService rollWindowService;

    private int studentCount;
    private int classScheduleCount;

    public DashboardViewModel(IScreen hostScreen, IStudentRepository studentRepository,
        IClassScheduleRepository classScheduleRepository, IClassRollRepository classRollRepository,
        IRollWindowService rollWindowService, IToastService toastService)
    {
        HostScreen = hostScreen;
        this.studentRepository = studentRepository;
        this.classRollRepository = classRollRepository;
        this.rollWindowService = rollWindowService;

        MarkRollCommand = ReactiveCommand.Create<ClassRoll>(MarkRoll);

        LoadOnCreate(() => LoadAsync(studentRepository, classScheduleRepository),
            exception => toastService.ShowError($"Couldn't load the dashboard: {exception.Message}"));
    }

    public string UrlPathSegment => "dashboard";
    public IScreen HostScreen { get; }

    public int StudentCount
    {
        get => studentCount;
        private set => this.RaiseAndSetIfChanged(ref studentCount, value);
    }

    public int ClassScheduleCount
    {
        get => classScheduleCount;
        private set => this.RaiseAndSetIfChanged(ref classScheduleCount, value);
    }

    public ObservableCollection<ClassRoll> TodaysClasses { get; } = new();

    public ReactiveCommand<ClassRoll, Unit> MarkRollCommand { get; }

    private async Task LoadAsync(IStudentRepository studentRepository, IClassScheduleRepository classScheduleRepository)
    {
        StartBusy();
        try
        {
            var students = await studentRepository.Query(new StudentFilter { IsActive = true }).LoadMultiple();
            StudentCount = students.Count;

            var schedules = await classScheduleRepository.Query(new ClassScheduleFilter()).LoadMultiple();
            ClassScheduleCount = schedules.Count;

            await ReloadTodaysClassesAsync();
        }
        finally
        {
            EndBusy();
        }
    }

    private async Task ReloadTodaysClassesAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var todaysRolls = await classRollRepository
            .Query(new ClassRollFilter
            {
                DateFrom = today,
                DateTo = today,
                SortBy = new KeyValuePair<ClassRollSortOptions, Order>(ClassRollSortOptions.Time, Order.Ascending)
            })
            .LoadMultiple();

        TodaysClasses.Clear();
        foreach (var roll in todaysRolls)
            TodaysClasses.Add(roll);
    }

    private void MarkRoll(ClassRoll roll)
    {
        rollWindowService.OpenOrActivate(roll, () => _ = ReloadTodaysClassesAsync());
    }
}
