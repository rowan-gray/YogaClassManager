using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;
using YogaClassManager.Avalonia.Services;
using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models.Classes;
using YogaClassManager.Core.Models.Passes;
using YogaClassManager.Core.Models.People;
using YogaClassManager.Core.Repositories;

namespace YogaClassManager.Avalonia.ViewModels.Shared;

/// <summary>
///     Marks attendance for one ClassRoll occurrence - shown via IRollWindowService as a real,
///     non-modal Window (not the shared dialog overlay) so the instructor can keep using the rest of
///     the app while a roll stays open, so this owns its own private IDialogService instance (Dialogs)
///     for its Add-pass/pass-picker popups, rather than using the app-wide one, which only renders
///     inside MainWindow.
///
///     Uses a save/cancel paradigm, not save-immediately: date/attendee/pass-assignment edits only
///     mutate local state (Date/Entries) and set IsDirty, and are reconciled against the repository -
///     via a diff against the last-saved snapshot, since IClassRollRepository's Add/Remove/UpdatePass
///     methods are incremental, not a single "replace everything" call - only when SaveCommand runs.
///     Creating a brand-new Pass via the "student has no available pass" flow is the one exception:
///     that's a distinct entity with its own explicit Save button in PassEditView, so it persists
///     immediately there regardless of whether the roll itself is later saved or cancelled.
///
///     That paradigm extends to the roll record itself: a roll opened for a class that has none yet
///     arrives here with Id 0 and is only INSERTed on save, so cancelling out of a roll the instructor
///     opened by mistake leaves nothing behind (it previously persisted an empty roll up front, which
///     cancelling then orphaned with no way to undo).
/// </summary>
public class MarkRollViewModel : ViewModelBase
{
    private readonly ClassSchedule classSchedule;
    private readonly IClassRollRepository classRollRepository;
    private readonly IStudentRepository studentRepository;
    private readonly IPassRepository passRepository;
    private readonly ITermRepository termRepository;
    private readonly IToastService toastService;

    /// <summary>0 until this roll has been persisted - see the class remarks. Not readonly because
    /// the first successful save is what assigns it a real id.</summary>
    private int classRollId;

    private DateOnly date;
    private List<(int StudentId, int? PassId)> savedEntryState;
    private string? searchQuery;
    private Student? selectedSearchResult;
    private bool hasSearchResults;
    private bool isDirty;

    public MarkRollViewModel(ClassRoll roll, IClassRollRepository classRollRepository,
        IStudentRepository studentRepository, IPassRepository passRepository, ITermRepository termRepository,
        IToastService toastService)
    {
        classRollId = roll.Id;
        classSchedule = roll.ClassSchedule;
        date = roll.Date;
        this.classRollRepository = classRollRepository;
        this.studentRepository = studentRepository;
        this.passRepository = passRepository;
        this.termRepository = termRepository;
        this.toastService = toastService;

        foreach (var entry in roll.StudentEntries)
            Entries.Add(entry);
        savedEntryState = SnapshotEntries();

        SaveCommand = ReactiveCommand.CreateFromTask(SaveAsync, this.WhenAnyValue(x => x.IsDirty));
        AddStudentCommand = ReactiveCommand.CreateFromTask<Student>(AddStudentAsync);
        RemoveStudentCommand = ReactiveCommand.Create<ClassRollEntry>(RemoveEntry);
        ChangePassCommand = ReactiveCommand.CreateFromTask<ClassRollEntry>(ChangePassAsync);

        // The keyboard path for the app's most-repeated action: Enter in the search box adds the top
        // match, Enter on a row in the results list adds that row. One command serves both because
        // "whatever is selected, or the first result if nothing is" is the same intent either way.
        var canAddSelected = this
            .WhenAnyValue(x => x.SelectedSearchResult, x => x.HasSearchResults,
                (selected, hasResults) => selected is not null || hasResults);
        AddSelectedStudentCommand = ReactiveCommand.CreateFromTask(AddSelectedStudentAsync, canAddSelected);

        this.WhenAnyValue(x => x.SearchQuery)
            // Skip(1): WhenAnyValue replays the current (empty) query immediately, which would run a
            // pointless search on open - the same footgun SearchableCollectionPageModelBase documents.
            .Skip(1)
            .Throttle(TimeSpan.FromMilliseconds(250), RxApp.MainThreadScheduler)
            .DistinctUntilChanged()
            // Switch, not SelectMany: two searches in flight would otherwise both Clear()-and-refill
            // SearchResults, and the slower one could land last and show results for a stale query.
            .Select(query => Observable.FromAsync(() => SearchStudentsAsync(query)))
            .Switch()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(ApplySearchResults,
                error => toastService.ShowError($"Couldn't search students - {error.Message}"));
    }

    /// <summary>Its own private instance, not the shared app-wide IDialogService - popups opened from
    /// here render in this window's own local overlay (see MarkRollWindow.axaml), not MainWindow's.</summary>
    public IDialogService Dialogs { get; } = new DialogService();

    public string ClassScheduleDescription => classSchedule.ToString();

    public bool IsDirty
    {
        get => isDirty;
        private set => this.RaiseAndSetIfChanged(ref isDirty, value);
    }

    public DateOnly Date
    {
        get => date;
        set
        {
            this.RaiseAndSetIfChanged(ref date, value);
            IsDirty = true;
        }
    }

    public string? SearchQuery
    {
        get => searchQuery;
        set => this.RaiseAndSetIfChanged(ref searchQuery, value);
    }

    /// <summary>The row the keyboard is on in the results list. Null while focus is still in the search
    /// box, which is why <see cref="AddSelectedStudentAsync" /> falls back to the first result.</summary>
    public Student? SelectedSearchResult
    {
        get => selectedSearchResult;
        set => this.RaiseAndSetIfChanged(ref selectedSearchResult, value);
    }

    public bool HasSearchResults
    {
        get => hasSearchResults;
        private set => this.RaiseAndSetIfChanged(ref hasSearchResults, value);
    }

    /// <summary>What to show in place of an empty results list. Distinguishes "you haven't typed
    /// anything yet" from "nothing matched what you typed" - both leave the list empty, but only one of
    /// them is a dead end.</summary>
    public string? SearchEmptyText => HasSearchResults
        ? null
        : string.IsNullOrWhiteSpace(SearchQuery)
            ? "Type a name to find a student."
            : "No students match this search.";

    public ObservableCollection<Student> SearchResults { get; } = new();
    public ObservableCollection<ClassRollEntry> Entries { get; } = new();

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Student, Unit> AddStudentCommand { get; }
    public ReactiveCommand<Unit, Unit> AddSelectedStudentCommand { get; }
    public ReactiveCommand<ClassRollEntry, Unit> RemoveStudentCommand { get; }
    public ReactiveCommand<ClassRollEntry, Unit> ChangePassCommand { get; }

    /// <summary>Shows the "discard unsaved changes?" prompt (via this window's own local Dialogs) if
    /// there's anything unsaved; returns true if it's fine to proceed closing (nothing to lose, or the
    /// instructor confirmed discarding it), false if the close should be aborted.</summary>
    public async Task<bool> ConfirmDiscardChangesAsync()
    {
        if (!IsDirty)
            return true;

        var confirmViewModel = new ConfirmViewModel("Discard changes?",
            "This roll has unsaved changes. Discard them?", confirmLabel: "Discard", cancelLabel: "Keep editing");

        return await Dialogs.ShowDialogAsync(confirmViewModel) == true;
    }

    private void ApplySearchResults(IEnumerable<Student> results)
    {
        SearchResults.Clear();
        foreach (var student in results)
            SearchResults.Add(student);

        SelectedSearchResult = null;
        HasSearchResults = SearchResults.Count > 0;
        this.RaisePropertyChanged(nameof(SearchEmptyText));
    }

    private Task AddSelectedStudentAsync()
    {
        var student = SelectedSearchResult ?? SearchResults.FirstOrDefault();
        return student is null ? Task.CompletedTask : AddStudentAsync(student);
    }

    private async Task<IEnumerable<Student>> SearchStudentsAsync(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        var excludeIds = Entries.Select(e => e.Student.Id).ToList();
        return await studentRepository
            .Query(new StudentFilter { NameFilter = query, IsActive = true, ExcludeIds = excludeIds })
            .LoadMultiple();
    }

    private async Task SaveAsync()
    {
        if (classRollId == 0)
        {
            // Never persisted (see class remarks): INSERT it with its entries in one call. There's no
            // previously-saved state to diff against, so the incremental Add/Remove/UpdatePass pass
            // below has nothing to do.
            classRollId = await classRollRepository.AddAsync(new ClassRoll(0, Date, classSchedule,
                Entries.ToList()));
            savedEntryState = SnapshotEntries();
            IsDirty = false;
            return;
        }

        await classRollRepository.UpdateDetailsAsync(new ClassRoll(classRollId, Date, classSchedule,
            Entries.ToList()));

        var previousStudentIds = savedEntryState.Select(e => e.StudentId).ToHashSet();
        var currentStudentIds = Entries.Select(e => e.Student.Id).ToHashSet();

        foreach (var studentId in previousStudentIds.Except(currentStudentIds))
            await classRollRepository.RemoveStudentEntryAsync(classRollId, studentId);

        foreach (var entry in Entries)
        {
            if (!previousStudentIds.Contains(entry.Student.Id))
            {
                await classRollRepository.AddStudentEntryAsync(classRollId, entry);
            }
            else
            {
                var previousPassId = savedEntryState.First(e => e.StudentId == entry.Student.Id).PassId;
                if (previousPassId != entry.Pass?.Id)
                    await classRollRepository.UpdateStudentEntryPassAsync(classRollId, entry.Student.Id,
                        entry.Pass?.Id);
            }
        }

        savedEntryState = SnapshotEntries();
        IsDirty = false;
    }

    private List<(int StudentId, int? PassId)> SnapshotEntries()
    {
        return Entries.Select(e => (e.Student.Id, e.Pass?.Id)).ToList();
    }

    private async Task AddStudentAsync(Student student)
    {
        try
        {
            StartBusy();

            var candidates = await BuildCandidatesAsync(student);

            Pass? chosenPass;
            if (candidates.Count == 0)
            {
                toastService.ShowWarning($"{student.FullName} has no available pass.");

                var editViewModel = new PassEditViewModel(student.Id, Dialogs, termRepository);
                var newPass = await Dialogs.ShowDialogAsync(editViewModel);
                if (newPass is not null)
                    await passRepository.AddAsync(newPass);

                chosenPass = newPass;
            }
            else if (candidates.Count == 1)
            {
                chosenPass = candidates[0];
            }
            else
            {
                var pickerViewModel = new PassPickerViewModel(candidates, classSchedule, Date, student.FullName);
                chosenPass = await Dialogs.ShowDialogAsync(pickerViewModel);
            }

            Entries.Add(new ClassRollEntry(student, chosenPass));
            IsDirty = true;

            SearchQuery = null;
        }
        finally
        {
            EndBusy();
        }
    }

    private void RemoveEntry(ClassRollEntry entry)
    {
        Entries.Remove(entry);
        IsDirty = true;
    }

    /// <summary>Explicit "change the pass being used" action from an attendee row's context menu -
    /// unlike AddStudentAsync, a single candidate still shows the picker (so the instructor sees and
    /// confirms the choice, since they deliberately asked to change it), and cancelling at any point
    /// leaves the entry's current pass untouched rather than falling back to "no pass".</summary>
    private async Task ChangePassAsync(ClassRollEntry entry)
    {
        try
        {
            StartBusy();

            var candidates = await BuildCandidatesAsync(entry.Student);

            Pass chosenPass;
            if (candidates.Count == 0)
            {
                toastService.ShowWarning($"{entry.Student.FullName} has no available pass.");

                var editViewModel = new PassEditViewModel(entry.Student.Id, Dialogs, termRepository);
                var newPass = await Dialogs.ShowDialogAsync(editViewModel);
                if (newPass is null)
                    return;

                await passRepository.AddAsync(newPass);
                chosenPass = newPass;
            }
            else
            {
                var pickerViewModel = new PassPickerViewModel(candidates, classSchedule, Date, entry.Student.FullName);
                var picked = await Dialogs.ShowDialogAsync(pickerViewModel);
                if (picked is null)
                    return;

                chosenPass = picked;
            }

            var index = Entries.IndexOf(entry);
            Entries[index] = new ClassRollEntry(entry.Student, chosenPass);
            IsDirty = true;
        }
        finally
        {
            EndBusy();
        }
    }

    private async Task<List<Pass>> BuildCandidatesAsync(Student student)
    {
        var passes = await studentRepository.GetPassesAsync(student.Id, includeExpired: true,
            includeDepleted: false);

        return passes
            .Select(p => (Pass: p, Priority: p.GetPassUsagePriority(classSchedule, Date)))
            .Where(x => x.Priority is not null || IsRecentlyExpired(x.Pass, classSchedule, Date))
            .OrderBy(x => TypeRank(x.Pass))
            .ThenBy(x => x.Priority is null) // usable-now before recently-expired, within the same type
            .ThenBy(x => x.Priority ?? 0)
            .Select(x => x.Pass)
            .ToList();
    }

    /// <summary>A pass that no longer auto-selects (GetPassUsagePriority returned null) but still
    /// expired recently enough to be worth offering the instructor explicitly, clearly marked. Computed
    /// relative to the roll's own Date (not real wall-clock "today") so marking a backdated roll doesn't
    /// disagree with itself about what "recently expired" means. Term passes must still match the
    /// class's own schedule, mirroring GetPassUsagePriority's own guard; Dated has no such constraint,
    /// also matching GetPassUsagePriority.</summary>
    public static bool IsRecentlyExpired(Pass pass, ClassSchedule schedule, DateOnly date)
    {
        return pass.ExpiryDate is { } expiry
               && expiry < date
               && expiry >= date.AddMonths(-1)
               && (pass is not TermPass termPass || termPass.TermClassSchedule.ClassSchedule.Equals(schedule));
    }

    private static int TypeRank(Pass pass) => pass switch
    {
        TermPass => 1,
        DatedPass => 2,
        _ => 3
    };
}
