using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;
using YogaClassManager.Avalonia.Services;
using YogaClassManager.Avalonia.ViewModels.Base;
using YogaClassManager.Avalonia.ViewModels.Passes;
using YogaClassManager.Avalonia.ViewModels.Shared;
using YogaClassManager.Core.Data;
using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models;
using YogaClassManager.Core.Models.Classes;
using YogaClassManager.Core.Models.Passes;
using YogaClassManager.Core.Models.People;
using YogaClassManager.Core.Repositories;

namespace YogaClassManager.Avalonia.ViewModels.Students;

public class StudentsViewModel : SearchableCollectionPageModelBase<Student>, IRoutableViewModel
{
    private readonly IStudentRepository studentRepository;
    private readonly IIdentityRepository identityRepository;
    private readonly IPassRepository passRepository;
    private readonly ITermRepository termRepository;
    private readonly IClassRollRepository classRollRepository;
    private readonly IDialogService dialogService;
    private readonly IToastService toastService;
    private readonly IPageNavigator navigator;
    private bool includeArchived;
    private StudentSortOptions sortBy = StudentSortOptions.FirstName;
    private Order sortOrder = Order.Ascending;
    private DateOnly? lastAttendedFrom;
    private DateOnly? lastAttendedTo;
    private PassKind passKindFilter = PassKind.Any;
    private bool includeExpiredPasses = true;
    private bool includeDepletedPasses = true;

    public StudentsViewModel(IScreen hostScreen, IStudentRepository studentRepository,
        IIdentityRepository identityRepository, IPassRepository passRepository, ITermRepository termRepository,
        IClassRollRepository classRollRepository, IDialogService dialogService, IToastService toastService,
        IPageNavigator navigator)
        : base(toastService)
    {
        HostScreen = hostScreen;
        this.navigator = navigator;
        this.studentRepository = studentRepository;
        this.identityRepository = identityRepository;
        this.passRepository = passRepository;
        this.termRepository = termRepository;
        this.classRollRepository = classRollRepository;
        this.dialogService = dialogService;
        this.toastService = toastService;

        AddStudentCommand = ReactiveCommand.CreateFromTask(AddStudentAsync);
        EditStudentCommand = ReactiveCommand.CreateFromTask(EditStudentAsync, HasSelection);
        ToggleArchiveCommand = ReactiveCommand.CreateFromTask(ToggleArchiveAsync, HasSelection);

        AddEmergencyContactCommand = ReactiveCommand.CreateFromTask(AddEmergencyContactAsync, HasSelection);
        RemoveEmergencyContactCommand = ReactiveCommand.CreateFromTask<EmergencyContact>(RemoveEmergencyContactAsync);

        AddHealthConcernCommand = ReactiveCommand.CreateFromTask(AddHealthConcernAsync, HasSelection);
        RemoveHealthConcernCommand = ReactiveCommand.CreateFromTask<string>(RemoveHealthConcernAsync);

        AddPassCommand = ReactiveCommand.CreateFromTask(AddPassAsync, HasSelection);
        EditPassCommand = ReactiveCommand.CreateFromTask<Pass>(EditPassAsync);
        RemovePassCommand = ReactiveCommand.CreateFromTask<Pass>(RemovePassAsync);
        ViewPassCommand = ReactiveCommand.CreateFromObservable<Pass, IRoutableViewModel>(ViewPass);
        ViewIdentityCommand = ReactiveCommand.CreateFromObservable(ViewIdentity, HasSelection);
        ViewClassCommand =
            ReactiveCommand.CreateFromObservable<ClassAttendanceRecord, IRoutableViewModel>(ViewClass);

        ReloadOn(this.WhenAnyValue(x => x.Selection), LoadLinkedDataAsync, "this student's details");

        RefreshWhenChanged(this.WhenAnyValue(x => x.IncludeArchived, x => x.SortBy, x => x.SortOrder,
            x => x.LastAttendedFrom, x => x.LastAttendedTo));

        ReloadOn(
            this.WhenAnyValue(x => x.PassKindFilter, x => x.IncludeExpiredPasses, x => x.IncludeDepletedPasses)
                .Skip(1),
            LoadPassesAsync, "this student's passes");

        this.WhenAnyValue(x => x.IncludeArchived, x => x.LastAttendedFrom, x => x.LastAttendedTo)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(ActiveFilterCount)));

        this.WhenAnyValue(x => x.PassKindFilter, x => x.IncludeExpiredPasses, x => x.IncludeDepletedPasses)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(PassFilterCount)));

        ClearFiltersCommand = ReactiveCommand.Create(ClearFilters);
        ClearPassFiltersCommand = ReactiveCommand.Create(ClearPassFilters);

        RefreshCommand.Execute().Subscribe(_ =>
        {
            if (navigator.TakePendingStudentSelection() is not { } pendingId)
                return;

            Selection = Items.FirstOrDefault(s => s.Id == pendingId);
        });
    }

    public string UrlPathSegment => "students";
    public IScreen HostScreen { get; }

    public ObservableCollection<EmergencyContact> EmergencyContacts { get; } = new();
    public ObservableCollection<string> HealthConcerns { get; } = new();
    public ObservableCollection<Pass> Passes { get; } = new();
    public ObservableCollection<ClassAttendanceRecord> AttendanceHistory { get; } = new();

    public bool IncludeArchived
    {
        get => includeArchived;
        set => this.RaiseAndSetIfChanged(ref includeArchived, value);
    }

    public int ActiveFilterCount =>
        (IncludeArchived ? 1 : 0) + (LastAttendedFrom is not null ? 1 : 0) + (LastAttendedTo is not null ? 1 : 0);

    public StudentSortOptions SortBy
    {
        get => sortBy;
        set => this.RaiseAndSetIfChanged(ref sortBy, value);
    }

    // Id is an internal fallback sort key (see InMemoryStudentRepository's Sort), not a meaningful
    // choice for a user-facing dropdown - excluded here rather than removed from the enum.
    public IReadOnlyList<StudentSortOptions> SortByOptions { get; } =
        Enum.GetValues<StudentSortOptions>().Where(o => o != StudentSortOptions.Id).ToList();

    public Order SortOrder
    {
        get => sortOrder;
        set => this.RaiseAndSetIfChanged(ref sortOrder, value);
    }

    public PassKind PassKindFilter
    {
        get => passKindFilter;
        set => this.RaiseAndSetIfChanged(ref passKindFilter, value);
    }

    public IReadOnlyList<PassKind> PassKindFilterOptions { get; } = Enum.GetValues<PassKind>();

    public bool IncludeExpiredPasses
    {
        get => includeExpiredPasses;
        set => this.RaiseAndSetIfChanged(ref includeExpiredPasses, value);
    }

    public bool IncludeDepletedPasses
    {
        get => includeDepletedPasses;
        set => this.RaiseAndSetIfChanged(ref includeDepletedPasses, value);
    }

    // Include-expired/-depleted default to true (show everything), so a filter counts as "active"
    // when one is unchecked (narrowing the default view), not when checked.
    public int PassFilterCount =>
        (PassKindFilter != PassKind.Any ? 1 : 0) +
        (!IncludeExpiredPasses ? 1 : 0) +
        (!IncludeDepletedPasses ? 1 : 0);

    public DateOnly? LastAttendedFrom
    {
        get => lastAttendedFrom;
        set => this.RaiseAndSetIfChanged(ref lastAttendedFrom, value);
    }

    public DateOnly? LastAttendedTo
    {
        get => lastAttendedTo;
        set => this.RaiseAndSetIfChanged(ref lastAttendedTo, value);
    }

    public ReactiveCommand<Unit, Unit> AddStudentCommand { get; }
    public ReactiveCommand<Unit, Unit> EditStudentCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleArchiveCommand { get; }
    public ReactiveCommand<Unit, Unit> AddEmergencyContactCommand { get; }
    public ReactiveCommand<EmergencyContact, Unit> RemoveEmergencyContactCommand { get; }
    public ReactiveCommand<Unit, Unit> AddHealthConcernCommand { get; }
    public ReactiveCommand<string, Unit> RemoveHealthConcernCommand { get; }
    public ReactiveCommand<Unit, Unit> AddPassCommand { get; }
    public ReactiveCommand<Pass, Unit> EditPassCommand { get; }
    public ReactiveCommand<Pass, Unit> RemovePassCommand { get; }
    public ReactiveCommand<Pass, IRoutableViewModel> ViewPassCommand { get; }
    public ReactiveCommand<Unit, IRoutableViewModel> ViewIdentityCommand { get; }
    public ReactiveCommand<ClassAttendanceRecord, IRoutableViewModel> ViewClassCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearFiltersCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearPassFiltersCommand { get; }

    protected override async Task<IReadOnlyList<Student>> LoadItemsAsync()
    {
        var query = SearchQuery?.Trim();

        var filter = new StudentFilter
        {
            SortBy = new KeyValuePair<StudentSortOptions, Order>(SortBy, SortOrder),
            NameFilter = string.IsNullOrEmpty(query) ? null : query,
            IsActive = IncludeArchived ? null : true,
            LastAttendedFrom = LastAttendedFrom,
            LastAttendedTo = LastAttendedTo
        };

        return await studentRepository.Query(filter).LoadMultiple();
    }

    private void ClearFilters()
    {
        IncludeArchived = false;
        LastAttendedFrom = null;
        LastAttendedTo = null;
    }

    private void ClearPassFilters()
    {
        PassKindFilter = PassKind.Any;
        IncludeExpiredPasses = true;
        IncludeDepletedPasses = true;
    }

    /// <summary>The cancellationToken is what makes ReloadOn's Switch actually effective here - see its
    /// remarks. It is re-checked before each write because the writes, not the return value, are this
    /// method's output.</summary>
    private async Task LoadLinkedDataAsync(CancellationToken cancellationToken = default)
    {
        EmergencyContacts.Clear();
        HealthConcerns.Clear();
        AttendanceHistory.Clear();

        await LoadPassesAsync(cancellationToken);

        if (Selection is null)
            return;

        var attendance = await classRollRepository.GetAttendanceHistoryAsync(Selection.Id, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var record in attendance)
            AttendanceHistory.Add(record);

        var contacts = await studentRepository.GetEmergencyContactsAsync(Selection.Id,
            cancellationToken: cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var contact in contacts)
            EmergencyContacts.Add(contact);

        var concerns = await studentRepository.GetHealthConcernsAsync(Selection.Id, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var concern in concerns)
            HealthConcerns.Add(concern);
    }

    private async Task LoadPassesAsync(CancellationToken cancellationToken = default)
    {
        Passes.Clear();

        if (Selection is null)
            return;

        // Defaults to including expired/depleted passes (not just active ones) so they're visible per
        // the "see expired or depleted passes" requirement - the pass list flags them via Tag rather
        // than hiding them, and the filter row lets the instructor narrow the list themselves.
        var filter = new PassFilter
        {
            StudentId = Selection.Id,
            Kind = PassKindFilter,
            IncludeExpired = IncludeExpiredPasses,
            IncludeDepleted = IncludeDepletedPasses
        };

        var passes = await passRepository.Query(filter).LoadMultiple(cancellationToken: cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var pass in passes)
            Passes.Add(pass);
    }

    private IObservable<IRoutableViewModel> ViewPass(Pass pass)
    {
        return HostScreen.Router.Navigate.Execute(
            new PassDetailViewModel(HostScreen, pass, passRepository, navigator, toastService));
    }

    private IObservable<IRoutableViewModel> ViewIdentity() => navigator.ToIdentity(Selection!.Id);

    private IObservable<IRoutableViewModel> ViewClass(ClassAttendanceRecord record) =>
        navigator.ToClassRoll(record.ClassRollId);

    private async Task AddStudentAsync()
    {
        var editViewModel = new StudentEditViewModel(new Student(), isNew: true, identityRepository, dialogService);
        var result = await dialogService.ShowDialogAsync(editViewModel);

        if (result is null)
            return;

        // The dialog sets a positive Id itself when the instructor explicitly picked "use an existing
        // identity" - in that case the ambiguity is already resolved, so promote straight away rather
        // than running the name-match check below (which is only for the plain "brand new student"
        // path, to catch identities the instructor didn't think to look up themselves).
        if (result.Id > 0)
        {
            await studentRepository.PromoteToStudentAsync(result);
            await RefreshCommand.Execute();
            Selection = Items.FirstOrDefault(s => s.Id == result.Id);
            toastService.ShowInfo($"Converted {result.FullName} to a student.");
            return;
        }

        // Before creating a brand-new record, check whether this looks like someone who already
        // exists as an Identity (e.g. someone else's emergency contact) so we don't silently create a
        // duplicate - see PromoteConfirmViewModel for the "use existing instead?" prompt.
        var candidates = await identityRepository.Query(new IdentityFilter { NameFilter = result.FullName })
            .LoadMultiple();
        var match = candidates.FirstOrDefault(i => i.FullName.Equals(result.FullName, StringComparison.OrdinalIgnoreCase));

        if (match is Student existingStudent)
        {
            await RefreshCommand.Execute();
            Selection = Items.FirstOrDefault(s => s.Id == existingStudent.Id);
            toastService.ShowInfo($"{existingStudent.FullName} already exists as a student — selected instead.");
            return;
        }

        if (match is not null)
        {
            var confirmViewModel = new PromoteConfirmViewModel(match, result);
            var confirmed = await dialogService.ShowDialogAsync(confirmViewModel);

            if (confirmed)
            {
                result.Id = match.Id;
                await studentRepository.PromoteToStudentAsync(result);
                await RefreshCommand.Execute();
                Selection = Items.FirstOrDefault(s => s.Id == result.Id);
                toastService.ShowInfo($"Linked to the existing record for {result.FullName}.");
                return;
            }
        }

        var newId = await studentRepository.AddAsync(result);
        await RefreshCommand.Execute();
        Selection = Items.FirstOrDefault(s => s.Id == newId);
        toastService.ShowInfo($"Added {result.FullName}.");
    }

    private async Task EditStudentAsync()
    {
        if (Selection is null)
            return;

        var editViewModel = new StudentEditViewModel(Student.Copy(Selection), isNew: false, identityRepository,
            dialogService);
        var result = await dialogService.ShowDialogAsync(editViewModel);

        if (result is null)
            return;

        await studentRepository.UpdateAsync(result);
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
            // A Student always has links by definition (IdentityLinkageSummary.HasAnyLinks counts
            // IsStudent), so this archives rather than deletes - the wording can be definite here,
            // unlike the Identities page's equivalent prompt.
            if (!await dialogService.ConfirmAsync($"Archive {name}?",
                    $"{name} will be hidden from the students list. Their passes and attendance history "
                    + "are kept, and you can unarchive them later.",
                    confirmLabel: "Archive"))
                return;

            var result = await studentRepository.ArchiveOrDeleteAsync(Selection.Id);
            Selection = null;
            await RefreshCommand.Execute();

            toastService.ShowInfo(result == ArchiveResult.Deleted
                ? $"{name} had no other links, so the record was deleted."
                : $"{name} was archived.");
        }
        else
        {
            await studentRepository.UnarchiveAsync(Selection.Id);
            await RefreshCommand.Execute();
            toastService.ShowInfo($"{name} was unarchived.");
        }
    }

    private async Task AddEmergencyContactAsync()
    {
        if (Selection is null)
            return;

        var excludeIds = EmergencyContacts.Select(c => c.Id).Append(Selection.Id).ToList();
        var pickerViewModel = new IdentityPickerViewModel(identityRepository,
            $"Select an emergency contact for {Selection.FullName}", excludeIds);

        var identity = await dialogService.ShowDialogAsync(pickerViewModel);
        if (identity is null)
            return;

        var relationshipViewModel = new RelationshipPickerViewModel(identity.FullName);
        var relationship = await dialogService.ShowDialogAsync(relationshipViewModel);
        if (relationship is null)
            return;

        await studentRepository.LinkEmergencyContactAsync(Selection.Id, identity.Id, relationship.Value);
        await LoadLinkedDataAsync();
        toastService.ShowInfo($"Linked {identity.FullName} as an emergency contact.");
    }

    private async Task RemoveEmergencyContactAsync(EmergencyContact contact)
    {
        if (Selection is null)
            return;

        if (!await dialogService.ConfirmAsync("Remove emergency contact?",
                $"{contact.FullName} will no longer be listed as an emergency contact for "
                + $"{Selection.FullName}. Their own identity record is kept.",
                confirmLabel: "Remove"))
            return;

        await studentRepository.UnlinkEmergencyContactAsync(Selection.Id, contact.Id);
        await LoadLinkedDataAsync();
        toastService.ShowInfo($"Removed {contact.FullName} as an emergency contact.");
    }

    private async Task AddHealthConcernAsync()
    {
        if (Selection is null)
            return;

        var inputViewModel = new TextInputViewModel("Add health concern", "Description");
        var concern = await dialogService.ShowDialogAsync(inputViewModel);
        if (string.IsNullOrWhiteSpace(concern))
            return;

        await studentRepository.AddHealthConcernAsync(Selection.Id, concern);
        await LoadLinkedDataAsync();
    }

    private async Task RemoveHealthConcernAsync(string concern)
    {
        if (Selection is null)
            return;

        if (!await dialogService.ConfirmAsync("Remove health concern?",
                $"\"{concern}\" will be removed from {Selection.FullName}'s record.",
                confirmLabel: "Remove"))
            return;

        await studentRepository.RemoveHealthConcernAsync(Selection.Id, concern);
        await LoadLinkedDataAsync();
        toastService.ShowInfo("Health concern removed.");
    }

    private async Task AddPassAsync()
    {
        if (Selection is null)
            return;

        var editViewModel = new PassEditViewModel(Selection.Id, dialogService, termRepository);
        var pass = await dialogService.ShowDialogAsync(editViewModel);
        if (pass is null)
            return;

        await passRepository.AddAsync(pass);
        await LoadLinkedDataAsync();
        toastService.ShowInfo($"Added a {pass.PassName} for {Selection.FullName}.");
    }

    private async Task EditPassAsync(Pass pass)
    {
        if (Selection is null)
            return;

        var editViewModel = new PassEditViewModel(Selection.Id, dialogService, termRepository, pass);
        var updated = await dialogService.ShowDialogAsync(editViewModel);
        if (updated is null)
            return;

        await passRepository.UpdateAsync(updated);
        await LoadLinkedDataAsync();
        toastService.ShowInfo("Pass updated.");
    }

    private async Task RemovePassAsync(Pass pass)
    {
        // Deleting a pass isn't purely additive: the repository also clears it from every class roll
        // entry that used it, so past attendance stops recording which pass was spent. Say so.
        if (!await dialogService.ConfirmAsync("Delete this pass?",
                $"The {pass.PassName} will be deleted permanently. Any class already marked against it "
                + "will keep the attendance record but lose the link to this pass.",
                confirmLabel: "Delete"))
            return;

        await passRepository.DeleteAsync(pass.Id);
        await LoadLinkedDataAsync();
        toastService.ShowInfo($"Deleted the {pass.PassName}.");
    }
}
