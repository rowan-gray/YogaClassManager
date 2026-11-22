using System.Collections.ObjectModel;
using System.Reactive;
using ReactiveUI;
using YogaClassManager.Avalonia.Services;
using YogaClassManager.Avalonia.ViewModels.Shared;
using YogaClassManager.Core.Data;
using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models.Classes;
using YogaClassManager.Core.Models.People;
using YogaClassManager.Core.Repositories;

namespace YogaClassManager.Avalonia.ViewModels.Dashboard;

/// <summary>
///     Landing page: headline counts, quick actions (add a student, add a pass to a student, search
///     for a student and open their page), today's classes and the last 7 days' classes.
/// </summary>
public class DashboardViewModel : ViewModelBase, IRoutableViewModel
{
    /// <summary>How many days back (not counting today) the "Recent classes" section reaches.</summary>
    public const int RecentClassDays = 7;

    private readonly IClassRollRepository classRollRepository;
    private readonly IStudentRepository studentRepository;
    private readonly IClassScheduleRepository classScheduleRepository;
    private readonly IIdentityRepository identityRepository;
    private readonly IPassRepository passRepository;
    private readonly ITermRepository termRepository;
    private readonly IRollWindowService rollWindowService;
    private readonly IDialogService dialogService;
    private readonly IToastService toastService;
    private readonly IPageNavigator navigator;
    private readonly NootNootEasterEgg easterEgg;

    private int studentCount;
    private int classScheduleCount;

    public DashboardViewModel(IScreen hostScreen, IStudentRepository studentRepository,
        IClassScheduleRepository classScheduleRepository, IClassRollRepository classRollRepository,
        IIdentityRepository identityRepository, IPassRepository passRepository, ITermRepository termRepository,
        IRollWindowService rollWindowService, IDialogService dialogService, IToastService toastService,
        IPageNavigator navigator, NootNootEasterEgg easterEgg)
    {
        HostScreen = hostScreen;
        this.studentRepository = studentRepository;
        this.classScheduleRepository = classScheduleRepository;
        this.classRollRepository = classRollRepository;
        this.identityRepository = identityRepository;
        this.passRepository = passRepository;
        this.termRepository = termRepository;
        this.rollWindowService = rollWindowService;
        this.dialogService = dialogService;
        this.toastService = toastService;
        this.navigator = navigator;
        this.easterEgg = easterEgg;

        MarkRollCommand = ReactiveCommand.Create<ClassRoll>(MarkRoll);
        PlayNootCommand = ReactiveCommand.Create(easterEgg.PlayNext);
        AddStudentCommand = ReactiveCommand.CreateFromTask(AddStudentAsync);
        AddPassCommand = ReactiveCommand.CreateFromTask(AddPassAsync);
        SearchStudentCommand = ReactiveCommand.CreateFromTask(SearchStudentAsync);

        // A quick action that fails (e.g. the database went away mid-save) says so, rather than
        // leaving the instructor wondering whether it happened.
        foreach (var command in new[] { AddStudentCommand, AddPassCommand, SearchStudentCommand })
            command.ThrownExceptions.Subscribe(exception => toastService.ShowError(exception.Message));

        LoadOnCreate(LoadAsync,
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

    /// <summary>Every class roll from the last <see cref="RecentClassDays" /> days before today,
    /// newest first (today's own rolls are already under "Today's classes").</summary>
    public ObservableCollection<ClassRoll> RecentClasses { get; } = new();

    public ReactiveCommand<ClassRoll, Unit> MarkRollCommand { get; }

    /// <summary>Easter egg (from the MAUI app): clicking the Dashboard banner plays a noot noot.</summary>
    public ReactiveCommand<Unit, Unit> PlayNootCommand { get; }
    public ReactiveCommand<Unit, Unit> AddStudentCommand { get; }
    public ReactiveCommand<Unit, Unit> AddPassCommand { get; }
    public ReactiveCommand<Unit, Unit> SearchStudentCommand { get; }

    private async Task LoadAsync()
    {
        StartBusy();
        try
        {
            await ReloadCountsAsync();
            await ReloadClassesAsync();
        }
        finally
        {
            EndBusy();
        }
    }

    private async Task ReloadCountsAsync()
    {
        var students = await studentRepository.Query(new StudentFilter { IsActive = true }).LoadMultiple();
        StudentCount = students.Count;

        var schedules = await classScheduleRepository.Query(new ClassScheduleFilter()).LoadMultiple();
        ClassScheduleCount = schedules.Count;
    }

    private async Task ReloadClassesAsync()
    {
        await ReloadTodaysClassesAsync();
        await ReloadRecentClassesAsync();
    }

    /// <summary>
    ///     Every class scheduled for today, not just the ones whose roll has been started: a roll only
    ///     exists once it's been saved, so listing rolls alone hid today's classes until someone had
    ///     already marked them - exactly when the instructor needs them. Each active schedule for
    ///     today's weekday shows its existing roll if there is one, otherwise a new, unsaved roll
    ///     (MarkRollViewModel INSERTs it on save, so opening one by mistake leaves nothing behind - the
    ///     same as Class Schedules' "Mark new roll"). A roll already marked today for a class that's
    ///     since been archived or moved still shows.
    /// </summary>
    private async Task ReloadTodaysClassesAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var todaysRolls = await classRollRepository
            .Query(new ClassRollFilter { DateFrom = today, DateTo = today })
            .LoadMultiple();
        var todaysSchedules = await classScheduleRepository
            .Query(new ClassScheduleFilter { Day = today.DayOfWeek })
            .LoadMultiple();

        var scheduleIdsWithARoll = todaysRolls.Select(r => r.ClassSchedule.Id).ToHashSet();
        var notYetMarked = todaysSchedules
            .Where(schedule => !scheduleIdsWithARoll.Contains(schedule.Id))
            .Select(schedule => new ClassRoll(0, today, schedule, []));

        TodaysClasses.Clear();
        foreach (var roll in todaysRolls.Concat(notYetMarked).OrderBy(r => r.ClassSchedule.Time))
            TodaysClasses.Add(roll);
    }

    private async Task ReloadRecentClassesAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var previousRolls = await classRollRepository
            .Query(new ClassRollFilter
            {
                DateFrom = today.AddDays(-RecentClassDays),
                DateTo = today.AddDays(-1),
                SortBy = new KeyValuePair<ClassRollSortOptions, Order>(ClassRollSortOptions.Date, Order.Descending)
            })
            .LoadMultiple();

        RecentClasses.Clear();
        foreach (var roll in previousRolls)
            RecentClasses.Add(roll);
    }

    private void MarkRoll(ClassRoll roll)
    {
        // Either section's attendee counts can change in the roll window.
        rollWindowService.OpenOrActivate(roll, () => _ = ReloadClassesAsync());
    }

    private async Task AddStudentAsync()
    {
        var studentId = await new StudentCreationFlow(studentRepository, identityRepository, dialogService,
            toastService).RunAsync();
        if (studentId is null)
            return;

        // Straight to the new student's page - the next thing after adding someone is usually their
        // pass or emergency contact. (The dashboard's counts reload on the way back: page view models
        // are rebuilt on every navigation.)
        navigator.ToStudent(studentId.Value).Subscribe();
    }

    private async Task AddPassAsync()
    {
        if (await PickStudentAsync("Add a pass for...") is not { } student)
            return;

        var pass = await dialogService.ShowDialogAsync(new PassEditViewModel(student.Id, student.FullName, dialogService, termRepository));
        if (pass is null)
            return;

        await passRepository.AddAsync(pass);
        toastService.ShowInfo($"Added a {pass.PassName} for {student.FullName}.");
    }

    /// <summary>Find a student and open their page - the quick way to check their remaining passes
    /// (its Passes list shows each pass's classes remaining, hiding used-up ones by default), contact
    /// details or attendance, with Add pass right there if they need another.</summary>
    private async Task SearchStudentAsync()
    {
        if (await PickStudentAsync("Search for a student") is not { } student)
            return;

        navigator.ToStudent(student.Id).Subscribe();
    }

    /// <summary>The same student search as the Mark Roll window (StudentPickerViewModel wraps it).</summary>
    private Task<Student?> PickStudentAsync(string title) =>
        dialogService.ShowDialogAsync(new StudentPickerViewModel(studentRepository, title));
}
