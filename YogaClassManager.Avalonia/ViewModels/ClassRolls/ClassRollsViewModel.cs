using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;
using YogaClassManager.Avalonia.Services;
using YogaClassManager.Avalonia.ViewModels.Base;
using YogaClassManager.Avalonia.ViewModels.Shared;
using YogaClassManager.Core.Data;
using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models.Classes;
using YogaClassManager.Core.Models.People;
using YogaClassManager.Core.Repositories;

namespace YogaClassManager.Avalonia.ViewModels.ClassRolls;

public class ClassRollsViewModel : CollectionPageModelBase<ClassRoll>, IRoutableViewModel
{
    private readonly IClassRollRepository classRollRepository;
    private readonly IStudentRepository studentRepository;
    private readonly RollPassResolver passResolver;
    private readonly IDialogService dialogService;
    private readonly IToastService toastService;
    private readonly IRollWindowService rollWindowService;
    private readonly IPageNavigator navigator;

    // The list defaults to the last 6 months rather than every roll ever marked (same pattern as Class
    // Schedules' roll list and the Students page's attendance). Captured once so ActiveFilterCount and
    // Clear filters agree on what "default" means.
    private readonly DateOnly defaultDateFrom = DateOnly.FromDateTime(DateTime.Today).AddMonths(-6);
    private DateOnly? dateFrom;
    private DateOnly? dateTo;
    private DayOfWeek? dayOfWeek;
    private ClassRollSortOptions sortBy = ClassRollSortOptions.Date;
    private Order sortOrder = Order.Descending;

    public ClassRollsViewModel(IScreen hostScreen, IClassRollRepository classRollRepository,
        IStudentRepository studentRepository, IPassRepository passRepository, ITermRepository termRepository,
        IDialogService dialogService, IToastService toastService, IRollWindowService rollWindowService,
        IPageNavigator navigator)
        : base(toastService)
    {
        HostScreen = hostScreen;
        dateFrom = defaultDateFrom;
        this.navigator = navigator;
        this.classRollRepository = classRollRepository;
        this.studentRepository = studentRepository;
        this.dialogService = dialogService;
        this.toastService = toastService;
        this.rollWindowService = rollWindowService;
        passResolver = new RollPassResolver(studentRepository, passRepository, termRepository, toastService);

        EditRollCommand = ReactiveCommand.Create(EditRoll, HasSelection);
        DeleteRollCommand = ReactiveCommand.CreateFromTask(DeleteRollAsync, HasSelection);
        ClearFiltersCommand = ReactiveCommand.Create(ClearFilters);

        // Quick attendee edits without opening the whole roll in the Mark Roll window. Each saves at once.
        AddAttendeeCommand = ReactiveCommand.CreateFromTask(AddAttendeeAsync, HasSelection);
        ChangeAttendeePassCommand = ReactiveCommand.CreateFromTask<ClassRollEntry>(ChangeAttendeePassAsync);
        RemoveAttendeeCommand = ReactiveCommand.CreateFromTask<ClassRollEntry>(RemoveAttendeeAsync);
        foreach (var command in new IHandleObservableErrors[]
                     { AddAttendeeCommand, ChangeAttendeePassCommand, RemoveAttendeeCommand })
            command.ThrownExceptions.Subscribe(error =>
                toastService.ShowError($"Couldn't update the attendees: {error.Message}"));

        // The roll model's StudentEntries is a plain List, so the detail pane binds this observable copy.
        this.WhenAnyValue(x => x.Selection).Subscribe(roll => ShowAttendees(roll?.StudentEntries));

        RefreshWhenChanged(this.WhenAnyValue(x => x.DateFrom, x => x.DateTo, x => x.DayOfWeek, x => x.SortBy,
            x => x.SortOrder));

        this.WhenAnyValue(x => x.DateFrom, x => x.DateTo, x => x.DayOfWeek)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(ActiveFilterCount)));

        // Switch + onError for the same reasons as CollectionPageModelBase.ReloadOn: this runs once at
        // construction, but a throw here would otherwise be an unhandled Rx exception on the UI thread.
        RefreshCommand.Execute()
            .Select(_ => Observable.FromAsync(OpenPendingRollAsync))
            .Switch()
            .Subscribe(_ => { },
                error => toastService.ShowError($"Couldn't open that roll: {error.Message}"));
    }

    /// <summary>The selected roll's attendees, for the detail pane's Attendees list.</summary>
    public ObservableCollection<ClassRollEntry> Attendees { get; } = new();

    public ReactiveCommand<Unit, Unit> AddAttendeeCommand { get; }
    public ReactiveCommand<ClassRollEntry, Unit> ChangeAttendeePassCommand { get; }
    public ReactiveCommand<ClassRollEntry, Unit> RemoveAttendeeCommand { get; }

    public string UrlPathSegment => "class-rolls";
    public IScreen HostScreen { get; }

    public DateOnly? DateFrom
    {
        get => dateFrom;
        set => this.RaiseAndSetIfChanged(ref dateFrom, value);
    }

    public DateOnly? DateTo
    {
        get => dateTo;
        set => this.RaiseAndSetIfChanged(ref dateTo, value);
    }

    public DayOfWeek? DayOfWeek
    {
        get => dayOfWeek;
        set => this.RaiseAndSetIfChanged(ref dayOfWeek, value);
    }

    public IReadOnlyList<DayOfWeek?> DayOfWeekOptions { get; } =
        new DayOfWeek?[] { null }.Concat(Enum.GetValues<DayOfWeek>().Cast<DayOfWeek?>()).ToList();

    public ClassRollSortOptions SortBy
    {
        get => sortBy;
        set => this.RaiseAndSetIfChanged(ref sortBy, value);
    }

    public IReadOnlyList<ClassRollSortOptions> SortByOptions { get; } = Enum.GetValues<ClassRollSortOptions>();

    public Order SortOrder
    {
        get => sortOrder;
        set => this.RaiseAndSetIfChanged(ref sortOrder, value);
    }

    /// <summary>Named to match the other pages' own-list filter count (Students/Identities); it was the
    /// one page calling this bare "FilterCount", which is the name the style guide reserves for a
    /// child list's count on a page that has more than one filterable list.</summary>
    public int ActiveFilterCount =>
        (DateFrom != defaultDateFrom ? 1 : 0) + (DateTo is not null ? 1 : 0) + (DayOfWeek is not null ? 1 : 0);

    public ReactiveCommand<Unit, Unit> EditRollCommand { get; }
    public ReactiveCommand<Unit, Unit> DeleteRollCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearFiltersCommand { get; }

    protected override async Task<IReadOnlyList<ClassRoll>> LoadItemsAsync()
    {
        var filter = new ClassRollFilter
        {
            DateFrom = DateFrom,
            DateTo = DateTo,
            DayOfWeek = DayOfWeek,
            SortBy = new KeyValuePair<ClassRollSortOptions, Order>(SortBy, SortOrder)
        };

        return await classRollRepository.Query(filter).LoadMultiple();
    }

    private void ClearFilters()
    {
        DateFrom = defaultDateFrom;
        DateTo = null;
        DayOfWeek = null;
    }

    /// <summary>Honours a "view this class" link from elsewhere (a student's attendance history, a
    /// pass's usage history) by selecting that roll and opening its Mark Roll window.</summary>
    private async Task OpenPendingRollAsync()
    {
        if (navigator.TakePendingClassRollToView() is not { } pendingRollId)
            return;

        var roll = await classRollRepository.Query(new ClassRollFilter { Id = (uint)pendingRollId }).LoadSingle();
        if (roll is null)
            return;

        Selection = Items.FirstOrDefault(r => r.Id == roll.Id);
        rollWindowService.OpenOrActivate(roll, () => RefreshCommand.Execute().Subscribe());
    }

    private void EditRoll()
    {
        if (Selection is null)
            return;

        rollWindowService.OpenOrActivate(Selection, () => { _ = RefreshCommand.Execute(); });
    }

    private async Task DeleteRollAsync()
    {
        if (Selection is null)
            return;

        var name = Selection.ToString();

        if (!await dialogService.ConfirmAsync("Delete this roll?",
                $"{name} and its attendance record will be deleted permanently. This can't be undone.",
                confirmLabel: "Delete"))
            return;

        await classRollRepository.DeleteAsync(Selection.Id);
        Selection = null;
        await RefreshCommand.Execute();
        toastService.ShowInfo($"Deleted {name}.");
    }

    private void ShowAttendees(IEnumerable<ClassRollEntry>? entries)
    {
        Attendees.Clear();
        foreach (var entry in entries ?? [])
            Attendees.Add(entry);
    }

    /// <summary>Re-reads the selected roll after an attendee change and refreshes the Attendees list in
    /// place (rather than reloading the whole page list, which would reset the selection).</summary>
    private async Task ReloadSelectedRollAsync(int rollId)
    {
        var fresh = await classRollRepository.Query(new ClassRollFilter { Id = (uint)rollId }).LoadSingle();
        if (fresh is null || Selection?.Id != rollId)
            return;

        Selection.StudentEntries = fresh.StudentEntries;
        ShowAttendees(fresh.StudentEntries);
    }

    /// <summary>Add one student to the roll: pick them (anyone already on it is left out), choose their
    /// pass exactly as the Mark Roll window would (RollPassResolver), save.</summary>
    private async Task AddAttendeeAsync()
    {
        if (Selection is not { } roll)
            return;

        var onRoll = Attendees.Select(e => e.Student.Id).ToList();
        var student = await dialogService.ShowDialogAsync(
            new StudentPickerViewModel(studentRepository, $"Add a student to {roll}", onRoll));
        if (student is null)
            return;

        var choice = await passResolver.ChooseForNewAttendeeAsync(student, roll.ClassSchedule, roll.Date, dialogService);
        if (choice.Cancelled)
            return;

        await classRollRepository.AddStudentEntryAsync(roll.Id, new ClassRollEntry(student, choice.Pass));
        await ReloadSelectedRollAsync(roll.Id);
        toastService.ShowInfo($"Added {student.FullName} to the roll.");
    }

    private async Task ChangeAttendeePassAsync(ClassRollEntry entry)
    {
        if (Selection is not { } roll)
            return;

        var choice = await passResolver.ChooseReplacementAsync(entry.Student, roll.ClassSchedule, roll.Date, dialogService);
        if (choice.Cancelled)
            return;

        await classRollRepository.UpdateStudentEntryPassAsync(roll.Id, entry.Student.Id, choice.Pass?.Id);
        await ReloadSelectedRollAsync(roll.Id);
        toastService.ShowInfo($"Changed {entry.Student.FullName}'s pass.");
    }

    private async Task RemoveAttendeeAsync(ClassRollEntry entry)
    {
        if (Selection is not { } roll)
            return;

        // Saves immediately (unlike removing someone inside the Mark Roll window), so confirm first.
        if (!await dialogService.ConfirmAsync("Remove from roll?",
                $"{entry.Student.FullName} will be removed from {roll}. The class will no longer count "
                + "against their pass.",
                confirmLabel: "Remove"))
            return;

        await classRollRepository.RemoveStudentEntryAsync(roll.Id, entry.Student.Id);
        await ReloadSelectedRollAsync(roll.Id);
        toastService.ShowInfo($"Removed {entry.Student.FullName} from the roll.");
    }
}
