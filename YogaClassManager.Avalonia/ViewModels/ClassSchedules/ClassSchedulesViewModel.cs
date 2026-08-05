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
using YogaClassManager.Core.Repositories;

namespace YogaClassManager.Avalonia.ViewModels.ClassSchedules;

public class ClassSchedulesViewModel : CollectionPageModelBase<ClassSchedule>, IRoutableViewModel
{
    private readonly IClassScheduleRepository classScheduleRepository;
    private readonly IClassRollRepository classRollRepository;
    private readonly IDialogService dialogService;
    private readonly IToastService toastService;
    private readonly IRollWindowService rollWindowService;
    private readonly IPageNavigator navigator;

    private bool includeArchived;
    private DayOfWeek? day;
    private ClassScheduleSortOptions sortBy = ClassScheduleSortOptions.Day;
    private Order sortOrder = Order.Ascending;

    private DateOnly? rollDateFrom;
    private DateOnly? rollDateTo;
    private DayOfWeek? rollDayOfWeek;
    private ClassRollSortOptions rollSortBy = ClassRollSortOptions.Date;
    private Order rollSortOrder = Order.Descending;

    public ClassSchedulesViewModel(IScreen hostScreen, IClassScheduleRepository classScheduleRepository,
        IClassRollRepository classRollRepository, IDialogService dialogService, IToastService toastService,
        IRollWindowService rollWindowService, IPageNavigator navigator) : base(toastService)
    {
        HostScreen = hostScreen;
        this.navigator = navigator;
        this.classScheduleRepository = classScheduleRepository;
        this.classRollRepository = classRollRepository;
        this.dialogService = dialogService;
        this.toastService = toastService;
        this.rollWindowService = rollWindowService;

        AddClassCommand = ReactiveCommand.CreateFromTask(AddClassAsync);
        EditClassCommand = ReactiveCommand.CreateFromTask(EditClassAsync, HasSelection);
        ToggleArchiveCommand = ReactiveCommand.CreateFromTask(ToggleArchiveAsync, HasSelection);

        ClearFiltersCommand = ReactiveCommand.Create(ClearFilters);

        MarkNewRollCommand = ReactiveCommand.CreateFromTask(MarkNewRollAsync, HasSelection);
        EditRollCommand = ReactiveCommand.Create<ClassRoll>(EditRoll);
        RemoveRollCommand = ReactiveCommand.CreateFromTask<ClassRoll>(RemoveRollAsync);
        ClearRollFiltersCommand = ReactiveCommand.Create(ClearRollFilters);

        RefreshWhenChanged(this.WhenAnyValue(x => x.IncludeArchived, x => x.Day, x => x.SortBy, x => x.SortOrder));

        this.WhenAnyValue(x => x.IncludeArchived, x => x.Day)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(ActiveFilterCount)));

        ReloadOn(this.WhenAnyValue(x => x.Selection), LoadRollsAsync, "this class's rolls");

        ReloadOn(
            this.WhenAnyValue(x => x.RollDateFrom, x => x.RollDateTo, x => x.RollDayOfWeek, x => x.RollSortBy,
                x => x.RollSortOrder).Skip(1),
            LoadRollsAsync, "this class's rolls");

        this.WhenAnyValue(x => x.RollDateFrom, x => x.RollDateTo, x => x.RollDayOfWeek)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(RollFilterCount)));

        RefreshCommand.Execute()
            .Subscribe(_ =>
            {
                if (navigator.TakePendingClassScheduleSelection() is { } pendingScheduleId)
                    Selection = Items.FirstOrDefault(c => c.Id == pendingScheduleId);
            });
    }

    public string UrlPathSegment => "classes";
    public IScreen HostScreen { get; }

    public bool IncludeArchived
    {
        get => includeArchived;
        set => this.RaiseAndSetIfChanged(ref includeArchived, value);
    }

    public DayOfWeek? Day
    {
        get => day;
        set => this.RaiseAndSetIfChanged(ref day, value);
    }

    public IReadOnlyList<DayOfWeek?> DayOptions { get; } =
        new DayOfWeek?[] { null }.Concat(Enum.GetValues<DayOfWeek>().Cast<DayOfWeek?>()).ToList();

    public ClassScheduleSortOptions SortBy
    {
        get => sortBy;
        set => this.RaiseAndSetIfChanged(ref sortBy, value);
    }

    // Id is an internal fallback sort key (see InMemoryClassScheduleRepository's Sort), not a
    // meaningful choice for a user-facing dropdown - excluded here rather than removed from the enum.
    public IReadOnlyList<ClassScheduleSortOptions> SortByOptions { get; } =
        Enum.GetValues<ClassScheduleSortOptions>().Where(o => o != ClassScheduleSortOptions.Id).ToList();

    public Order SortOrder
    {
        get => sortOrder;
        set => this.RaiseAndSetIfChanged(ref sortOrder, value);
    }


    public int ActiveFilterCount => (IncludeArchived ? 1 : 0) + (Day is not null ? 1 : 0);

    public ObservableCollection<ClassRoll> Rolls { get; } = new();

    public DateOnly? RollDateFrom
    {
        get => rollDateFrom;
        set => this.RaiseAndSetIfChanged(ref rollDateFrom, value);
    }

    public DateOnly? RollDateTo
    {
        get => rollDateTo;
        set => this.RaiseAndSetIfChanged(ref rollDateTo, value);
    }

    public DayOfWeek? RollDayOfWeek
    {
        get => rollDayOfWeek;
        set => this.RaiseAndSetIfChanged(ref rollDayOfWeek, value);
    }

    public IReadOnlyList<DayOfWeek?> RollDayOfWeekOptions { get; } =
        new DayOfWeek?[] { null }.Concat(Enum.GetValues<DayOfWeek>().Cast<DayOfWeek?>()).ToList();

    public ClassRollSortOptions RollSortBy
    {
        get => rollSortBy;
        set => this.RaiseAndSetIfChanged(ref rollSortBy, value);
    }

    public IReadOnlyList<ClassRollSortOptions> RollSortByOptions { get; } = Enum.GetValues<ClassRollSortOptions>();

    public Order RollSortOrder
    {
        get => rollSortOrder;
        set => this.RaiseAndSetIfChanged(ref rollSortOrder, value);
    }

    public IReadOnlyList<Order> RollSortOrderOptions { get; } = Enum.GetValues<Order>();

    public int RollFilterCount =>
        (RollDateFrom is not null ? 1 : 0) + (RollDateTo is not null ? 1 : 0) + (RollDayOfWeek is not null ? 1 : 0);

    public ReactiveCommand<Unit, Unit> ClearFiltersCommand { get; }
    public ReactiveCommand<Unit, Unit> AddClassCommand { get; }
    public ReactiveCommand<Unit, Unit> EditClassCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleArchiveCommand { get; }
    public ReactiveCommand<Unit, Unit> MarkNewRollCommand { get; }
    public ReactiveCommand<ClassRoll, Unit> EditRollCommand { get; }
    public ReactiveCommand<ClassRoll, Unit> RemoveRollCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearRollFiltersCommand { get; }

    protected override async Task<IReadOnlyList<ClassSchedule>> LoadItemsAsync()
    {
        var filter = new ClassScheduleFilter
        {
            IncludeArchived = IncludeArchived,
            Day = Day,
            SortBy = new KeyValuePair<ClassScheduleSortOptions, Order>(SortBy, SortOrder)
        };

        return await classScheduleRepository.Query(filter).LoadMultiple();
    }

    private void ClearFilters()
    {
        IncludeArchived = false;
        Day = null;
    }

    /// <summary>The cancellationToken is what makes ReloadOn's Switch actually effective here - see its
    /// remarks. It is re-checked before the write because the write, not the return value, is this
    /// method's output.</summary>
    private async Task LoadRollsAsync(CancellationToken cancellationToken = default)
    {
        Rolls.Clear();

        if (Selection is null)
            return;

        var filter = new ClassRollFilter
        {
            ClassScheduleId = Selection.Id,
            DateFrom = RollDateFrom,
            DateTo = RollDateTo,
            DayOfWeek = RollDayOfWeek,
            SortBy = new KeyValuePair<ClassRollSortOptions, Order>(RollSortBy, RollSortOrder)
        };

        var rolls = await classRollRepository.Query(filter).LoadMultiple(cancellationToken: cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var roll in rolls)
            Rolls.Add(roll);
    }

    private void ClearRollFilters()
    {
        RollDateFrom = null;
        RollDateTo = null;
        RollDayOfWeek = null;
    }

    private async Task AddClassAsync()
    {
        var editViewModel = new ClassScheduleEditViewModel(classScheduleRepository);
        var schedule = await dialogService.ShowDialogAsync(editViewModel);
        if (schedule is null)
            return;

        try
        {
            await classScheduleRepository.AddAsync(schedule);
            await RefreshCommand.Execute();
            toastService.ShowInfo($"Added {schedule}.");
        }
        catch (InvalidOperationException ex)
        {
            toastService.ShowError(ex.Message);
        }
    }

    private async Task EditClassAsync()
    {
        if (Selection is null)
            return;

        var editViewModel = new ClassScheduleEditViewModel(classScheduleRepository, Selection);
        var schedule = await dialogService.ShowDialogAsync(editViewModel);
        if (schedule is null)
            return;

        try
        {
            await classScheduleRepository.UpdateAsync(schedule);
            await RefreshCommand.Execute();
            toastService.ShowInfo($"Saved {schedule}.");
        }
        catch (InvalidOperationException ex)
        {
            toastService.ShowError(ex.Message);
        }
    }

    private async Task ToggleArchiveAsync()
    {
        if (Selection is null)
            return;

        var name = Selection.ToString();
        var wasArchived = Selection.IsArchived;

        if (wasArchived)
            await classScheduleRepository.UnarchiveAsync(Selection.Id);
        else
            await classScheduleRepository.ArchiveAsync(Selection.Id);

        await RefreshCommand.Execute();
        toastService.ShowInfo(wasArchived ? $"{name} was unarchived." : $"{name} was archived.");
    }

    private async Task MarkNewRollAsync()
    {
        if (Selection is null)
            return;

        // "Mark new roll" clicked twice for the same class/day shouldn't create two distinct rolls -
        // reuse today's roll for this schedule if one already exists (dedup by window instance alone,
        // in RollWindowService, only catches re-opening the *same* ClassRoll id; this catches creating
        // a second one in the first place).
        var today = DateOnly.FromDateTime(DateTime.Now);
        var existingRoll = await classRollRepository
            .Query(new ClassRollFilter { ClassScheduleId = Selection.Id, DateFrom = today, DateTo = today })
            .LoadSingle();

        if (existingRoll is not null)
        {
            rollWindowService.OpenOrActivate(existingRoll, () => { _ = LoadRollsAsync(); });
            return;
        }

        // Deliberately NOT persisted here - MarkRollViewModel INSERTs it on save, so cancelling out of
        // a roll opened by mistake leaves nothing behind.
        var newRoll = new ClassRoll(0, today, Selection, []);
        rollWindowService.OpenOrActivate(newRoll, () => { _ = LoadRollsAsync(); });
    }

    private void EditRoll(ClassRoll roll)
    {
        rollWindowService.OpenOrActivate(roll, () => { _ = LoadRollsAsync(); });
    }

    private async Task RemoveRollAsync(ClassRoll roll)
    {
        if (!await dialogService.ConfirmAsync("Delete this roll?",
                $"{roll} and its attendance record will be deleted permanently. This can't be undone.",
                confirmLabel: "Delete"))
            return;

        await classRollRepository.DeleteAsync(roll.Id);
        await LoadRollsAsync();
        toastService.ShowInfo($"Deleted {roll}.");
    }
}
