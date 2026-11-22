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
    private bool includeDepletedPasses;
    // Attendance history defaults to the last 6 months rather than everything, so a long-standing
    // student's list opens on recent classes. Captured once per ViewModel so the filter count and
    // Clear filters agree on what "default" means.
    private readonly DateOnly defaultAttendanceDateFrom = DateOnly.FromDateTime(DateTime.Today).AddMonths(-6);
    private DateOnly? attendanceDateFrom;
    private DateOnly? attendanceDateTo;
    private DayOfWeek? attendanceDayOfWeek;
    private ClassRollSortOptions attendanceSortBy = ClassRollSortOptions.Date;
    private Order attendanceSortOrder = Order.Descending;

    public StudentsViewModel(IScreen hostScreen, IStudentRepository studentRepository,
        IIdentityRepository identityRepository, IPassRepository passRepository, ITermRepository termRepository,
        IClassRollRepository classRollRepository, IDialogService dialogService, IToastService toastService,
        IPageNavigator navigator)
        : base(toastService)
    {
        HostScreen = hostScreen;
        this.navigator = navigator;
        attendanceDateFrom = defaultAttendanceDateFrom;
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
        ViewEmergencyContactCommand =
            ReactiveCommand.CreateFromObservable<EmergencyContact, IRoutableViewModel>(ViewEmergencyContact);

        AddHealthConcernCommand = ReactiveCommand.CreateFromTask(AddHealthConcernAsync, HasSelection);
        RemoveHealthConcernCommand = ReactiveCommand.CreateFromTask<string>(RemoveHealthConcernAsync);
        EditHealthConcernCommand = ReactiveCommand.CreateFromTask<string>(EditHealthConcernAsync);

        AddPassCommand = ReactiveCommand.CreateFromTask(AddPassAsync, HasSelection);
        EditPassCommand = ReactiveCommand.CreateFromTask<Pass>(EditPassAsync);
        RemovePassCommand = ReactiveCommand.CreateFromTask<Pass>(RemovePassAsync);
        ViewPassCommand = ReactiveCommand.CreateFromObservable<Pass, IRoutableViewModel>(ViewPass);
        ViewIdentityCommand = ReactiveCommand.CreateFromObservable(ViewIdentity, HasSelection);
        ViewClassCommand =
            ReactiveCommand.CreateFromObservable<ClassAttendanceRecord, IRoutableViewModel>(ViewClass);

        // Debounced, with the lists emptied at once so their skeletons show during the wait.
        ReloadOn(this.WhenAnyValue(x => x.Selection), LoadLinkedDataAsync, "this student's details",
            debounce: true, clearNow: () =>
            {
                EmergencyContacts.Clear();
                HealthConcerns.Clear();
                Passes.Clear();
                AttendanceHistory.Clear();
            });

        RefreshWhenChanged(this.WhenAnyValue(x => x.IncludeArchived, x => x.SortBy, x => x.SortOrder,
            x => x.LastAttendedFrom, x => x.LastAttendedTo));

        ReloadOn(
            this.WhenAnyValue(x => x.PassKindFilter, x => x.IncludeExpiredPasses, x => x.IncludeDepletedPasses)
                .Skip(1),
            LoadPassesAsync, "this student's passes");

        ReloadOn(
            this.WhenAnyValue(x => x.AttendanceDateFrom, x => x.AttendanceDateTo, x => x.AttendanceDayOfWeek,
                x => x.AttendanceSortBy, x => x.AttendanceSortOrder).Skip(1),
            LoadAttendanceHistoryAsync, "this student's attendance history");

        this.WhenAnyValue(x => x.IncludeArchived, x => x.LastAttendedFrom, x => x.LastAttendedTo)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(ActiveFilterCount)));

        this.WhenAnyValue(x => x.PassKindFilter, x => x.IncludeExpiredPasses, x => x.IncludeDepletedPasses)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(PassFilterCount)));

        this.WhenAnyValue(x => x.AttendanceDateFrom, x => x.AttendanceDateTo, x => x.AttendanceDayOfWeek)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(AttendanceFilterCount)));

        ClearFiltersCommand = ReactiveCommand.Create(ClearFilters);
        ClearPassFiltersCommand = ReactiveCommand.Create(ClearPassFilters);
        ClearAttendanceFiltersCommand = ReactiveCommand.Create(ClearAttendanceFilters);

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

    // A filter counts as "active" when it differs from its default. Include-expired defaults to true
    // (so it's active when unchecked); include-depleted defaults to false - depleted passes are hidden
    // by default - so it's active when checked.
    public int PassFilterCount =>
        (PassKindFilter != PassKind.Any ? 1 : 0) +
        (!IncludeExpiredPasses ? 1 : 0) +
        (IncludeDepletedPasses ? 1 : 0);

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

    // Same filter/sort shape as ClassSchedulesViewModel's Roll* members (Class Schedules' "Class
    // rolls" list) - both go through ClassRollFilter/ClassRollSortOptions, so this list's filtering
    // and ordering can't drift from that one's.
    public DateOnly? AttendanceDateFrom
    {
        get => attendanceDateFrom;
        set => this.RaiseAndSetIfChanged(ref attendanceDateFrom, value);
    }

    public DateOnly? AttendanceDateTo
    {
        get => attendanceDateTo;
        set => this.RaiseAndSetIfChanged(ref attendanceDateTo, value);
    }

    public DayOfWeek? AttendanceDayOfWeek
    {
        get => attendanceDayOfWeek;
        set => this.RaiseAndSetIfChanged(ref attendanceDayOfWeek, value);
    }

    public IReadOnlyList<DayOfWeek?> AttendanceDayOfWeekOptions { get; } =
        new DayOfWeek?[] { null }.Concat(Enum.GetValues<DayOfWeek>().Cast<DayOfWeek?>()).ToList();

    public ClassRollSortOptions AttendanceSortBy
    {
        get => attendanceSortBy;
        set => this.RaiseAndSetIfChanged(ref attendanceSortBy, value);
    }

    public IReadOnlyList<ClassRollSortOptions> AttendanceSortByOptions { get; } = Enum.GetValues<ClassRollSortOptions>();

    public Order AttendanceSortOrder
    {
        get => attendanceSortOrder;
        set => this.RaiseAndSetIfChanged(ref attendanceSortOrder, value);
    }

    public IReadOnlyList<Order> AttendanceSortOrderOptions { get; } = Enum.GetValues<Order>();

    public int AttendanceFilterCount =>
        (AttendanceDateFrom != defaultAttendanceDateFrom ? 1 : 0) + (AttendanceDateTo is not null ? 1 : 0) +
        (AttendanceDayOfWeek is not null ? 1 : 0);

    public ReactiveCommand<Unit, Unit> AddStudentCommand { get; }
    public ReactiveCommand<Unit, Unit> EditStudentCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleArchiveCommand { get; }
    public ReactiveCommand<Unit, Unit> AddEmergencyContactCommand { get; }
    public ReactiveCommand<EmergencyContact, Unit> RemoveEmergencyContactCommand { get; }
    public ReactiveCommand<EmergencyContact, IRoutableViewModel> ViewEmergencyContactCommand { get; }
    public ReactiveCommand<Unit, Unit> AddHealthConcernCommand { get; }
    public ReactiveCommand<string, Unit> RemoveHealthConcernCommand { get; }
    public ReactiveCommand<string, Unit> EditHealthConcernCommand { get; }
    public ReactiveCommand<Unit, Unit> AddPassCommand { get; }
    public ReactiveCommand<Pass, Unit> EditPassCommand { get; }
    public ReactiveCommand<Pass, Unit> RemovePassCommand { get; }
    public ReactiveCommand<Pass, IRoutableViewModel> ViewPassCommand { get; }
    public ReactiveCommand<Unit, IRoutableViewModel> ViewIdentityCommand { get; }
    public ReactiveCommand<ClassAttendanceRecord, IRoutableViewModel> ViewClassCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearFiltersCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearPassFiltersCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearAttendanceFiltersCommand { get; }

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
        IncludeDepletedPasses = false;
    }

    private void ClearAttendanceFilters()
    {
        AttendanceDateFrom = defaultAttendanceDateFrom;
        AttendanceDateTo = null;
        AttendanceDayOfWeek = null;
    }

    /// <summary>The cancellationToken is what makes ReloadOn's Switch actually effective here - see its
    /// remarks. It is re-checked before each write because the writes, not the return value, are this
    /// method's output.</summary>
    private async Task LoadLinkedDataAsync(CancellationToken cancellationToken = default)
    {
        EmergencyContacts.Clear();
        HealthConcerns.Clear();

        await LoadPassesAsync(cancellationToken);
        await LoadAttendanceHistoryAsync(cancellationToken);

        if (Selection is null)
            return;

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

        // Defaults to including expired passes but hiding depleted ones (used up, so rarely relevant);
        // both are flagged via Tag when shown, and the filter flyout lets the instructor bring
        // depleted passes back or narrow the list further.
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

    private async Task LoadAttendanceHistoryAsync(CancellationToken cancellationToken = default)
    {
        AttendanceHistory.Clear();

        if (Selection is null)
            return;

        // Same ClassRollFilter/ClassRollSortOptions shape as the Class Schedules page's "Class rolls"
        // list (see LoadRollsAsync there) - GetAttendanceHistoryAsync routes this through the exact
        // same Query filtering/ordering, so the two can't drift apart.
        var filter = new ClassRollFilter
        {
            DateFrom = AttendanceDateFrom,
            DateTo = AttendanceDateTo,
            DayOfWeek = AttendanceDayOfWeek,
            SortBy = new KeyValuePair<ClassRollSortOptions, Order>(AttendanceSortBy, AttendanceSortOrder)
        };

        var attendance = await classRollRepository.GetAttendanceHistoryAsync(Selection.Id, filter, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var record in attendance)
            AttendanceHistory.Add(record);
    }

    private IObservable<IRoutableViewModel> ViewPass(Pass pass)
    {
        return HostScreen.Router.Navigate.Execute(
            new PassDetailViewModel(HostScreen, pass, passRepository, navigator, toastService));
    }

    private IObservable<IRoutableViewModel> ViewIdentity() => navigator.ToIdentity(Selection!.Id);

    /// <summary>An emergency contact is an Identity in their own right - open it on the People page
    /// (contact details, merge, which other students they're a contact for).</summary>
    private IObservable<IRoutableViewModel> ViewEmergencyContact(EmergencyContact contact) =>
        navigator.ToIdentity(contact.Id);

    private IObservable<IRoutableViewModel> ViewClass(ClassAttendanceRecord record) =>
        navigator.ToClassRoll(record.ClassRollId);

    private async Task AddStudentAsync()
    {
        // Shared with the Dashboard's "Add student" quick action - see StudentCreationFlow.
        var studentId = await new StudentCreationFlow(studentRepository, identityRepository, dialogService,
            toastService).RunAsync();
        if (studentId is null)
            return;

        await RefreshCommand.Execute();
        Selection = Items.FirstOrDefault(s => s.Id == studentId);
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
        // The same search as the student picker and Mark Roll (ContactPickerViewModel), over any person.
        var pickerViewModel = new ContactPickerViewModel(identityRepository,
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

    /// <summary>
    ///     Health concerns are plain strings with no id, and the repository has no update - so an edit
    ///     adds the new text, then removes the old. Adding first means a failure part-way never loses
    ///     the concern. Cancelling, a blank result, or no change leaves it as it was.
    /// </summary>
    private async Task EditHealthConcernAsync(string concern)
    {
        if (Selection is null)
            return;

        var edited = (await dialogService.ShowDialogAsync(
            new TextInputViewModel("Edit health concern", "Description", concern)))?.Trim();
        if (string.IsNullOrEmpty(edited) || edited == concern)
            return;

        await studentRepository.AddHealthConcernAsync(Selection.Id, edited);
        await studentRepository.RemoveHealthConcernAsync(Selection.Id, concern);
        await LoadLinkedDataAsync();
        toastService.ShowInfo("Health concern updated.");
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

        var editViewModel = new PassEditViewModel(Selection.Id, Selection.FullName, dialogService, termRepository);
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

        var editViewModel = new PassEditViewModel(Selection.Id, Selection.FullName, dialogService, termRepository, pass);
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
