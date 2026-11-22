using YogaClassManager.Avalonia.ViewModels.Shared;
using YogaClassManager.Core.Models.Classes;
using YogaClassManager.Core.Models.Passes;
using YogaClassManager.Core.Models.People;
using YogaClassManager.Core.Repositories;

namespace YogaClassManager.Avalonia.Services;

/// <summary>The outcome of choosing a pass for a roll entry. <see cref="Cancelled" /> means the
/// instructor backed out (don't add/change anything); otherwise <see cref="Pass" /> is the pass to use,
/// or null for "no pass".</summary>
public readonly record struct PassChoice(bool Cancelled, Pass? Pass)
{
    public static PassChoice Cancel => new(true, null);
    public static PassChoice Use(Pass? pass) => new(false, pass);
}

/// <summary>
///     Which pass a student's attendance at a class should use - shared by the Mark Roll window and the
///     Class Rolls page's attendee actions, so both choose passes identically:
///     <list type="bullet">
///         <item>Candidates are the student's non-depleted passes that cover this class on this date,
///               plus ones that expired within the last month (offered, clearly marked), term passes
///               first, then dated, then casual.</item>
///         <item>None: warn, and offer "Add pass" - cancelling it cancels the whole action.</item>
///     </list>
/// </summary>
public sealed class RollPassResolver
{
    private readonly IStudentRepository studentRepository;
    private readonly IPassRepository passRepository;
    private readonly ITermRepository termRepository;
    private readonly IToastService toastService;

    public RollPassResolver(IStudentRepository studentRepository, IPassRepository passRepository,
        ITermRepository termRepository, IToastService toastService)
    {
        this.studentRepository = studentRepository;
        this.passRepository = passRepository;
        this.termRepository = termRepository;
        this.toastService = toastService;
    }

    /// <summary>For adding a student to a roll: a single candidate is used automatically; several show
    /// the picker (cancelling it adds them without a pass); none offers "Add pass" (cancelling that
    /// cancels adding them at all).</summary>
    public async Task<PassChoice> ChooseForNewAttendeeAsync(Student student, ClassSchedule schedule, DateOnly date,
        IDialogService dialogs)
    {
        var candidates = await CandidatesAsync(student, schedule, date);
        return candidates.Count switch
        {
            0 => await AddPassAsync(student, dialogs),
            1 => PassChoice.Use(candidates[0]),
            _ => PassChoice.Use(await dialogs.ShowDialogAsync(
                new PassPickerViewModel(candidates, schedule, date, student.FullName)))
        };
    }

    /// <summary>For deliberately changing an attendee's pass: the picker shows even for a single
    /// candidate (so the choice is seen and confirmed), and cancelling at any point changes nothing.</summary>
    public async Task<PassChoice> ChooseReplacementAsync(Student student, ClassSchedule schedule, DateOnly date,
        IDialogService dialogs)
    {
        var candidates = await CandidatesAsync(student, schedule, date);
        if (candidates.Count == 0)
            return await AddPassAsync(student, dialogs);

        var picked = await dialogs.ShowDialogAsync(new PassPickerViewModel(candidates, schedule, date, student.FullName));
        return picked is null ? PassChoice.Cancel : PassChoice.Use(picked);
    }

    public async Task<List<Pass>> CandidatesAsync(Student student, ClassSchedule schedule, DateOnly date)
    {
        var passes = await studentRepository.GetPassesAsync(student.Id, includeExpired: true, includeDepleted: false);

        return passes
            .Select(p => (Pass: p, Priority: p.GetPassUsagePriority(schedule, date)))
            .Where(x => x.Priority is not null || IsRecentlyExpired(x.Pass, schedule, date))
            .OrderBy(x => TypeRank(x.Pass))
            .ThenBy(x => x.Priority is null) // usable-now before recently-expired, within the same type
            .ThenBy(x => x.Priority ?? 0)
            .Select(x => x.Pass)
            .ToList();
    }

    /// <summary>A pass that no longer auto-selects (GetPassUsagePriority returned null) but still
    /// expired recently enough to be worth offering explicitly, clearly marked. Computed relative to the
    /// roll's own date (not today) so marking a backdated roll doesn't disagree with itself about what
    /// "recently expired" means. Term passes must still match the class's own schedule, mirroring
    /// GetPassUsagePriority's own guard; Dated has no such constraint, also matching it.</summary>
    public static bool IsRecentlyExpired(Pass pass, ClassSchedule schedule, DateOnly date)
    {
        return pass.ExpiryDate is { } expiry
               && expiry < date
               && expiry >= date.AddMonths(-1)
               && (pass is not TermPass termPass || termPass.TermClassSchedule.ClassSchedule.Equals(schedule));
    }

    private async Task<PassChoice> AddPassAsync(Student student, IDialogService dialogs)
    {
        toastService.ShowWarning($"{student.FullName} has no available pass.");

        var newPass = await dialogs.ShowDialogAsync(
            new PassEditViewModel(student.Id, student.FullName, dialogs, termRepository));
        if (newPass is null)
            return PassChoice.Cancel;

        await passRepository.AddAsync(newPass);
        return PassChoice.Use(newPass);
    }

    private static int TypeRank(Pass pass) => pass switch
    {
        TermPass => 1,
        DatedPass => 2,
        _ => 3
    };
}
