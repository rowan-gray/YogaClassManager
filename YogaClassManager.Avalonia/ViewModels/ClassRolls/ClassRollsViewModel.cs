using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;
using YogaClassManager.Avalonia.Services;
using YogaClassManager.Avalonia.ViewModels.Base;
using YogaClassManager.Core.Data;
using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models.Classes;
using YogaClassManager.Core.Repositories;

namespace YogaClassManager.Avalonia.ViewModels.ClassRolls;

public class ClassRollsViewModel : CollectionPageModelBase<ClassRoll>, IRoutableViewModel
{
    private readonly IClassRollRepository classRollRepository;
    private readonly IDialogService dialogService;
    private readonly IToastService toastService;
    private readonly IRollWindowService rollWindowService;
    private readonly IPageNavigator navigator;

    private DateOnly? dateFrom;
    private DateOnly? dateTo;
    private DayOfWeek? dayOfWeek;
    private ClassRollSortOptions sortBy = ClassRollSortOptions.Date;
    private Order sortOrder = Order.Descending;

    public ClassRollsViewModel(IScreen hostScreen, IClassRollRepository classRollRepository,
        IDialogService dialogService, IToastService toastService, IRollWindowService rollWindowService,
        IPageNavigator navigator)
        : base(toastService)
    {
        HostScreen = hostScreen;
        this.navigator = navigator;
        this.classRollRepository = classRollRepository;
        this.dialogService = dialogService;
        this.toastService = toastService;
        this.rollWindowService = rollWindowService;

        EditRollCommand = ReactiveCommand.Create(EditRoll, HasSelection);
        DeleteRollCommand = ReactiveCommand.CreateFromTask(DeleteRollAsync, HasSelection);
        ClearFiltersCommand = ReactiveCommand.Create(ClearFilters);

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
        (DateFrom is not null ? 1 : 0) + (DateTo is not null ? 1 : 0) + (DayOfWeek is not null ? 1 : 0);

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
        DateFrom = null;
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
}
