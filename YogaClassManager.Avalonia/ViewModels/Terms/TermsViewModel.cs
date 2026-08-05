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

namespace YogaClassManager.Avalonia.ViewModels.Terms;

public class TermsViewModel : SearchableCollectionPageModelBase<Term>, IRoutableViewModel
{
    private readonly ITermRepository termRepository;
    private readonly IClassScheduleRepository classScheduleRepository;
    private readonly IDialogService dialogService;
    private readonly IToastService toastService;
    private bool includeCompleted;
    private TermSortOptions sortBy = TermSortOptions.StartDate;
    private Order sortOrder = Order.Descending;

    public TermsViewModel(IScreen hostScreen, ITermRepository termRepository,
        IClassScheduleRepository classScheduleRepository, IDialogService dialogService,
        IToastService toastService) : base(toastService)
    {
        HostScreen = hostScreen;
        this.termRepository = termRepository;
        this.classScheduleRepository = classScheduleRepository;
        this.dialogService = dialogService;
        this.toastService = toastService;

        AddTermCommand = ReactiveCommand.CreateFromTask(AddTermAsync);
        EditTermCommand = ReactiveCommand.CreateFromTask(EditTermAsync, HasSelection);
        DeleteTermCommand = ReactiveCommand.CreateFromTask(DeleteTermAsync, HasSelection);

        AddClassLinkCommand = ReactiveCommand.CreateFromTask(AddClassLinkAsync, HasSelection);
        RemoveClassLinkCommand = ReactiveCommand.CreateFromTask<TermClassSchedule>(RemoveClassLinkAsync);
        ClearFiltersCommand = ReactiveCommand.Create(ClearFilters);

        RefreshWhenChanged(this.WhenAnyValue(x => x.IncludeCompleted, x => x.SortBy, x => x.SortOrder));

        this.WhenAnyValue(x => x.IncludeCompleted)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(ActiveFilterCount)));

        RefreshCommand.Execute().Subscribe();
    }

    public string UrlPathSegment => "terms";
    public IScreen HostScreen { get; }

    public bool IncludeCompleted
    {
        get => includeCompleted;
        set => this.RaiseAndSetIfChanged(ref includeCompleted, value);
    }

    public int ActiveFilterCount => IncludeCompleted ? 1 : 0;

    public TermSortOptions SortBy
    {
        get => sortBy;
        set => this.RaiseAndSetIfChanged(ref sortBy, value);
    }

    // Id is an internal fallback sort key (see InMemoryTermRepository's Sort), not a meaningful
    // choice for a user-facing dropdown - excluded here rather than removed from the enum.
    public IReadOnlyList<TermSortOptions> SortByOptions { get; } =
        Enum.GetValues<TermSortOptions>().Where(o => o != TermSortOptions.Id).ToList();

    public Order SortOrder
    {
        get => sortOrder;
        set => this.RaiseAndSetIfChanged(ref sortOrder, value);
    }

    public ReactiveCommand<Unit, Unit> ClearFiltersCommand { get; }
    public ReactiveCommand<Unit, Unit> AddTermCommand { get; }
    public ReactiveCommand<Unit, Unit> EditTermCommand { get; }
    public ReactiveCommand<Unit, Unit> DeleteTermCommand { get; }
    public ReactiveCommand<Unit, Unit> AddClassLinkCommand { get; }
    public ReactiveCommand<TermClassSchedule, Unit> RemoveClassLinkCommand { get; }

    protected override async Task<IReadOnlyList<Term>> LoadItemsAsync()
    {
        var query = SearchQuery?.Trim();

        var filter = new TermFilter
        {
            IncludeCompleted = IncludeCompleted,
            NameFilter = string.IsNullOrEmpty(query) ? null : query,
            SortBy = new KeyValuePair<TermSortOptions, Order>(SortBy, SortOrder)
        };

        return await termRepository.Query(filter).LoadMultiple();
    }

    private void ClearFilters()
    {
        IncludeCompleted = false;
    }

    private async Task AddTermAsync()
    {
        var editViewModel = new TermEditViewModel();
        var term = await dialogService.ShowDialogAsync(editViewModel);
        if (term is null)
            return;

        var newId = await termRepository.AddAsync(term);
        await RefreshCommand.Execute();
        Selection = Items.FirstOrDefault(t => t.Id == newId);
        toastService.ShowInfo($"Added {term.Name}.");
    }

    private async Task EditTermAsync()
    {
        if (Selection is null)
            return;

        var editViewModel = new TermEditViewModel(Selection);
        var term = await dialogService.ShowDialogAsync(editViewModel);
        if (term is null)
            return;

        await termRepository.UpdateAsync(term);
        await RefreshCommand.Execute();
        toastService.ShowInfo($"Saved {term.Name}.");
    }

    private async Task DeleteTermAsync()
    {
        if (Selection is null)
            return;

        var name = Selection.Name;

        if (!await dialogService.ConfirmAsync($"Delete {name}?",
                $"{name} will be deleted permanently. This can't be undone.", confirmLabel: "Delete"))
            return;

        var deleted = await termRepository.TryDeleteAsync(Selection.Id);

        if (!deleted)
        {
            toastService.ShowWarning($"Can't delete {name} - it still has linked classes. Unlink them first.");
            return;
        }

        Selection = null;
        await RefreshCommand.Execute();
        toastService.ShowInfo($"Deleted {name}.");
    }

    private async Task AddClassLinkAsync()
    {
        if (Selection is null)
            return;

        var excludeIds = Selection.Classes.Select(c => c.ClassSchedule.Id).ToList();
        var linkViewModel = new ClassLinkViewModel(classScheduleRepository, Selection.Name, excludeIds);
        var selection = await dialogService.ShowDialogAsync(linkViewModel);
        if (selection is null)
            return;

        await termRepository.LinkClassAsync(Selection.Id, selection.Schedule.Id, selection.ClassCount);
        toastService.ShowInfo($"Linked {selection.Schedule} to {Selection.Name}.");
    }

    private async Task RemoveClassLinkAsync(TermClassSchedule link)
    {
        if (Selection is null)
            return;

        if (!await dialogService.ConfirmAsync("Unlink this class?",
                $"{link.ClassSchedule} will no longer be part of {Selection.Name}.",
                confirmLabel: "Unlink"))
            return;

        var scheduleId = link.ClassSchedule.Id;
        var unlinked = await termRepository.UnlinkClassAsync(Selection.Id, scheduleId);

        if (unlinked)
            toastService.ShowInfo("Class unlinked.");
        else
            toastService.ShowWarning("Can't unlink that class - it has already been used this term.");
    }
}
