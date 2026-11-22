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
///     as a diff against the last-saved snapshot, applied atomically via IClassRollRepository.ApplyChangesAsync
///     (a diff, not "replace everything", so concurrent edits to the roll elsewhere survive) - only when
///     SaveCommand runs.
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
    private readonly RollPassResolver passResolver;

    /// <summary>0 until this roll has been persisted - see the class remarks. Not readonly because
    /// the first successful save is what assigns it a real id.</summary>
    private int classRollId;

    private DateOnly date;
    private List<(int StudentId, int? PassId)> savedEntryState;
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
        RemoveStudentCommand = ReactiveCommand.Create<ClassRollEntry>(RemoveEntry);
        ChangePassCommand = ReactiveCommand.CreateFromTask<ClassRollEntry>(ChangePassAsync);

        // The keyboard path for the app's most-repeated action (Enter in the box adds the student when they're the only match,
        // Enter on a row adds that row) lives in the shared search - see StudentSearchViewModel.
        // Students already on the roll are left out of the results.
        passResolver = new RollPassResolver(studentRepository, passRepository, termRepository, toastService);

        Search = new StudentSearchViewModel(studentRepository, AddStudentAsync,
            error => toastService.ShowError($"Couldn't search students - {error.Message}"),
            () => Entries.Select(e => e.Student.Id).ToList());

        // Attendee rows show each student's health concerns under their name. Students loaded with a
        // roll don't carry them (the roll query only hydrates name/contact details), so fetch them.
        LoadOnCreate(LoadAttendeeHealthConcernsAsync,
            error => toastService.ShowError($"Couldn't load attendees' health concerns - {error.Message}"));
    }

    /// <summary>The "Add a student" search (Controls/PersonSearchBox); choosing a student adds them
    /// to the roll.</summary>
    public StudentSearchViewModel Search { get; }

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

    public ObservableCollection<ClassRollEntry> Entries { get; } = new();

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
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
            await RefreshAttendeePassesAsync();
            return;
        }

        // Diffed against the last save (not "replace the list") so an attendee added to this roll from the
        // Class Rolls page while this window was open isn't wiped out; applied in one atomic call so a
        // failure leaves the roll exactly as it was, with this window still dirty for a retry.
        var previousPassIds = savedEntryState.ToDictionary(e => e.StudentId, e => e.PassId);
        var currentStudentIds = Entries.Select(e => e.Student.Id).ToHashSet();

        var removedStudentIds = previousPassIds.Keys.Where(id => !currentStudentIds.Contains(id)).ToList();
        var addedOrChangedEntries = Entries
            .Where(e => !previousPassIds.TryGetValue(e.Student.Id, out var previousPassId) || previousPassId != e.Pass?.Id)
            .ToList();

        await classRollRepository.ApplyChangesAsync(new ClassRoll(classRollId, Date, classSchedule, []),
            removedStudentIds, addedOrChangedEntries);

        savedEntryState = SnapshotEntries();
        IsDirty = false;

        await RefreshAttendeePassesAsync();
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

            // Cancelled = the instructor backed out of "Add pass" (no usable pass): they're not put on the
            // roll without one, and the search (query and results) is left as it was so they can try
            // again or pick someone else. See RollPassResolver for how the pass is chosen.
            var choice = await passResolver.ChooseForNewAttendeeAsync(student, classSchedule, Date, Dialogs);
            if (choice.Cancelled)
                return;

            await LoadHealthConcernsAsync(student);
            Entries.Add(new ClassRollEntry(student, choice.Pass));
            IsDirty = true;

            Search.SearchQuery = null;
        }
        finally
        {
            EndBusy();
        }
    }

    private async Task LoadAttendeeHealthConcernsAsync()
    {
        foreach (var entry in Entries.ToList())
            await LoadHealthConcernsAsync(entry.Student);
    }

    /// <summary>Replaces (rather than adds to) Student.HealthConcerns, so the attendee row's binding
    /// to it re-evaluates.</summary>
    private async Task LoadHealthConcernsAsync(Student student)
    {
        var concerns = await studentRepository.GetHealthConcernsAsync(student.Id);
        student.HealthConcerns = new ObservableCollection<string>(concerns);
    }

    /// <summary>Attendee rows show each pass's classes remaining as saved; once this roll is saved its
    /// classes count against those passes, so re-read them in place (Pass.ClassesUsed notifies
    /// ClassesRemaining, which the rows bind to).</summary>
    private async Task RefreshAttendeePassesAsync()
    {
        foreach (var pass in Entries.Select(e => e.Pass).OfType<Pass>().Distinct().ToList())
            await passRepository
                .Query(new PassFilter { Id = (uint)pass.Id, IncludeExpired = true, IncludeDepleted = true })
                .Refresh(pass);
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

            var choice = await passResolver.ChooseReplacementAsync(entry.Student, classSchedule, Date, Dialogs);
            if (choice.Cancelled)
                return;

            var index = Entries.IndexOf(entry);
            Entries[index] = new ClassRollEntry(entry.Student, choice.Pass);
            IsDirty = true;
        }
        finally
        {
            EndBusy();
        }
    }
}
