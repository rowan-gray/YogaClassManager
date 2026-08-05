using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;
using YogaClassManager.Avalonia.Services;
using YogaClassManager.Avalonia.ViewModels.Base;
using YogaClassManager.Avalonia.ViewModels.Shared;
using YogaClassManager.Core.Data;
using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models;
using YogaClassManager.Core.Models.People;
using YogaClassManager.Core.Repositories;

namespace YogaClassManager.Avalonia.ViewModels.Identities;

public class IdentitiesViewModel : SearchableCollectionPageModelBase<Identity>, IRoutableViewModel
{
    private readonly IIdentityRepository identityRepository;
    private readonly IStudentRepository studentRepository;
    private readonly IEmergencyContactRepository emergencyContactRepository;
    private readonly IDialogService dialogService;
    private readonly IToastService toastService;
    private readonly IPageNavigator navigator;
    private Student? linkedStudent;
    private bool includeArchived;
    private IdentitySortOptions sortBy = IdentitySortOptions.FirstName;
    private Order sortOrder = Order.Ascending;

    public IdentitiesViewModel(IScreen hostScreen, IIdentityRepository identityRepository,
        IStudentRepository studentRepository, IEmergencyContactRepository emergencyContactRepository,
        IDialogService dialogService, IToastService toastService, IPageNavigator navigator)
        : base(toastService)
    {
        HostScreen = hostScreen;
        this.navigator = navigator;
        this.identityRepository = identityRepository;
        this.studentRepository = studentRepository;
        this.emergencyContactRepository = emergencyContactRepository;
        this.dialogService = dialogService;
        this.toastService = toastService;

        AddIdentityCommand = ReactiveCommand.CreateFromTask(AddIdentityAsync);
        EditIdentityCommand = ReactiveCommand.CreateFromTask(EditIdentityAsync, HasSelection);
        ToggleArchiveCommand = ReactiveCommand.CreateFromTask(ToggleArchiveAsync, HasSelection);
        MergeDuplicatesCommand = ReactiveCommand.CreateFromTask(MergeDuplicatesAsync, HasSelection);
        NavigateToStudentCommand = ReactiveCommand.CreateFromObservable<Student, IRoutableViewModel>(NavigateToStudent);
        ClearFiltersCommand = ReactiveCommand.Create(ClearFilters);

        ReloadOn(this.WhenAnyValue(x => x.Selection), LoadContextAsync, "this identity's links");

        RefreshWhenChanged(this.WhenAnyValue(x => x.IncludeArchived, x => x.SortBy, x => x.SortOrder));

        this.WhenAnyValue(x => x.IncludeArchived)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(ActiveFilterCount)));

        RefreshCommand.Execute().Subscribe(_ =>
        {
            if (navigator.TakePendingIdentitySelection() is not { } pendingId)
                return;

            Selection = Items.FirstOrDefault(p => p.Id == pendingId);
        });
    }

    public string UrlPathSegment => "identities";
    public IScreen HostScreen { get; }

    public bool IncludeArchived
    {
        get => includeArchived;
        set => this.RaiseAndSetIfChanged(ref includeArchived, value);
    }

    public int ActiveFilterCount => IncludeArchived ? 1 : 0;

    public IdentitySortOptions SortBy
    {
        get => sortBy;
        set => this.RaiseAndSetIfChanged(ref sortBy, value);
    }

    // Id is an internal fallback sort key (see InMemoryIdentityRepository's Sort), not a meaningful
    // choice for a user-facing dropdown - excluded here rather than removed from the enum.
    public IReadOnlyList<IdentitySortOptions> SortByOptions { get; } =
        Enum.GetValues<IdentitySortOptions>().Where(o => o != IdentitySortOptions.Id).ToList();

    public Order SortOrder
    {
        get => sortOrder;
        set => this.RaiseAndSetIfChanged(ref sortOrder, value);
    }

    /// <summary>Non-null iff the selected identity is also a Student - drives the "View student" link.</summary>
    public Student? LinkedStudent
    {
        get => linkedStudent;
        private set => this.RaiseAndSetIfChanged(ref linkedStudent, value);
    }

    /// <summary>Every Student the selected identity is an emergency contact for (can be more than one).</summary>
    public ObservableCollection<Student> EmergencyContactForStudents { get; } = new();

    public bool HasEmergencyContactLinks => EmergencyContactForStudents.Count > 0;

    public ReactiveCommand<Unit, Unit> AddIdentityCommand { get; }
    public ReactiveCommand<Unit, Unit> EditIdentityCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleArchiveCommand { get; }
    public ReactiveCommand<Unit, Unit> MergeDuplicatesCommand { get; }
    public ReactiveCommand<Student, IRoutableViewModel> NavigateToStudentCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearFiltersCommand { get; }

    protected override async Task<IReadOnlyList<Identity>> LoadItemsAsync()
    {
        var query = SearchQuery?.Trim();

        var filter = new IdentityFilter
        {
            NameFilter = string.IsNullOrEmpty(query) ? null : query,
            IsActive = IncludeArchived ? null : true,
            SortBy = new KeyValuePair<IdentitySortOptions, Order>(SortBy, SortOrder)
        };

        return await identityRepository.Query(filter).LoadMultiple();
    }

    private void ClearFilters()
    {
        IncludeArchived = false;
    }

    /// <summary>The cancellationToken is what makes ReloadOn's Switch actually effective here - see its
    /// remarks. It is re-checked before each write because the writes, not the return value, are this
    /// method's output.</summary>
    private async Task LoadContextAsync(CancellationToken cancellationToken = default)
    {
        LinkedStudent = null;
        EmergencyContactForStudents.Clear();

        if (Selection is null)
        {
            this.RaisePropertyChanged(nameof(HasEmergencyContactLinks));
            return;
        }

        var student = await studentRepository.Query(new StudentFilter { Id = (uint)Selection.Id })
            .LoadSingle(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        LinkedStudent = student;

        var contacts = await emergencyContactRepository.Query(new EmergencyContactFilter { Id = (uint)Selection.Id })
            .LoadMultiple(cancellationToken: cancellationToken);

        foreach (var studentId in contacts.Select(c => c.StudentId).Distinct())
        {
            var linkedTo = await studentRepository.Query(new StudentFilter { Id = (uint)studentId })
                .LoadSingle(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (linkedTo is not null)
                EmergencyContactForStudents.Add(linkedTo);
        }

        this.RaisePropertyChanged(nameof(HasEmergencyContactLinks));
    }

    private IObservable<IRoutableViewModel> NavigateToStudent(Student student) =>
        navigator.ToStudent(student.Id);

    private async Task AddIdentityAsync()
    {
        var editViewModel = new IdentityEditViewModel(new Identity(), isNew: true);
        var result = await dialogService.ShowDialogAsync(editViewModel);

        if (result is null)
            return;

        var newId = await identityRepository.AddAsync(result);
        await RefreshCommand.Execute();
        Selection = Items.FirstOrDefault(p => p.Id == newId);
        toastService.ShowInfo($"Added {result.FullName}.");
    }

    private async Task EditIdentityAsync()
    {
        if (Selection is null)
            return;

        var editViewModel = new IdentityEditViewModel(Identity.Copy(Selection), isNew: false);
        var result = await dialogService.ShowDialogAsync(editViewModel);

        if (result is null)
            return;

        await identityRepository.UpdateAsync(result);
        await RefreshCommand.Execute();
        toastService.ShowInfo($"Saved {result.FullName}.");
    }

    private async Task ToggleArchiveAsync()
    {
        if (Selection is null)
            return;

        var name = Selection.FullName;

        if (Selection.IsActive)
        {
            // ArchiveOrDeleteAsync hard-deletes an identity with no links at all and soft-archives one
            // with links, and the user previously only found out which from the toast afterwards. The
            // same linkage summary the merge preview uses tells us up front, so the prompt can be
            // honest about whether this is reversible.
            IdentityLinkageSummary linkage;
            try
            {
                linkage = await identityRepository.GetLinkageSummaryAsync(Selection.Id);
            }
            catch (Exception ex)
            {
                toastService.ShowError($"Could not archive {name}: {ex.Message}");
                return;
            }

            var confirmed = linkage.HasAnyLinks
                ? await dialogService.ConfirmAsync($"Archive {name}?",
                    $"{name} will be hidden from the identities list. Their linked records are kept, "
                    + "and you can unarchive them later.", confirmLabel: "Archive")
                : await dialogService.ConfirmAsync($"Delete {name}?",
                    $"{name} isn't linked to any student, pass, or attendance record, so this deletes "
                    + "the record permanently. This can't be undone.", confirmLabel: "Delete");

            if (!confirmed)
                return;

            var result = await identityRepository.ArchiveOrDeleteAsync(Selection.Id);
            Selection = null;
            await RefreshCommand.Execute();

            toastService.ShowInfo(result == ArchiveResult.Deleted
                ? $"{name} had no other links, so the record was deleted."
                : $"{name} was archived.");
        }
        else
        {
            await identityRepository.UnarchiveAsync(Selection.Id);
            await RefreshCommand.Execute();
            toastService.ShowInfo($"{name} was unarchived.");
        }
    }

    private async Task MergeDuplicatesAsync()
    {
        if (Selection is null)
            return;

        var survivor = Selection;

        // excludeStudents: MergeAsync rejects Student+Student outright, so offering students here let
        // the user pick an invalid target, read a full merge preview, confirm, and only then be told no.
        var pickerViewModel = new IdentityPickerViewModel(identityRepository,
            $"Select the duplicate of {survivor.FullName} to merge in", excludeIds: [survivor.Id],
            excludeStudents: survivor is Student);
        var duplicate = await dialogService.ShowDialogAsync(pickerViewModel);
        if (duplicate is null)
            return;

        IdentityLinkageSummary duplicateLinkage;
        try
        {
            duplicateLinkage = await identityRepository.GetLinkageSummaryAsync(duplicate.Id);
        }
        catch (Exception ex)
        {
            toastService.ShowError($"Could not merge: {ex.Message}");
            return;
        }

        var confirmViewModel = new MergeConfirmViewModel(survivor, duplicate, duplicateLinkage);
        var confirmed = await dialogService.ShowDialogAsync(confirmViewModel);
        if (!confirmed)
            return;

        try
        {
            var result = await identityRepository.MergeAsync(survivor.Id, duplicate.Id);
            toastService.ShowInfo($"Merged {duplicate.FullName} into {survivor.FullName}: " +
                             $"{result.RepointedEmergencyContactLinks} contact link(s), " +
                             $"{result.RepointedPasses} pass(es), " +
                             $"{result.RepointedAttendanceRecords} attendance record(s) repointed.");
        }
        catch (InvalidOperationException ex)
        {
            toastService.ShowError($"Could not merge: {ex.Message}");
        }

        Selection = null;
        await RefreshCommand.Execute();
    }
}
