using YogaClassManager.Avalonia.ViewModels.Shared;
using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models.People;
using YogaClassManager.Core.Repositories;

namespace YogaClassManager.Avalonia.Services;

/// <summary>
///     The whole "add a student" flow: the edit dialog, then either promoting an existing identity
///     (picked explicitly, or matched by name and confirmed) or creating a brand-new student. Shared
///     by the Students page's Add button and the Dashboard's "Add student" quick action so the
///     duplicate checks can't drift apart. Each caller decides what to do with the resulting student
///     (the Students page selects it; the Dashboard opens it on the Students page).
/// </summary>
public sealed class StudentCreationFlow
{
    private readonly IStudentRepository studentRepository;
    private readonly IIdentityRepository identityRepository;
    private readonly IDialogService dialogService;
    private readonly IToastService toastService;

    public StudentCreationFlow(IStudentRepository studentRepository, IIdentityRepository identityRepository,
        IDialogService dialogService, IToastService toastService)
    {
        this.studentRepository = studentRepository;
        this.identityRepository = identityRepository;
        this.dialogService = dialogService;
        this.toastService = toastService;
    }

    /// <summary>Runs the flow and returns the resulting student's id (newly created, or an existing
    /// identity converted to a student), or null if the instructor cancelled - including declining to
    /// create a second student with the same name as an existing one.</summary>
    public async Task<int?> RunAsync()
    {
        var editViewModel = new StudentEditViewModel(new Student(), isNew: true, identityRepository, dialogService);
        var result = await dialogService.ShowDialogAsync(editViewModel);

        if (result is null)
            return null;

        // The dialog sets a positive Id itself when the instructor explicitly picked "use an existing
        // identity" - in that case the ambiguity is already resolved, so promote straight away rather
        // than running the name-match check below (which is only for the plain "brand new student"
        // path, to catch identities the instructor didn't think to look up themselves).
        if (result.Id > 0)
        {
            await studentRepository.PromoteToStudentAsync(result);
            toastService.ShowInfo($"Converted {result.FullName} to a student.");
            return result.Id;
        }

        // Before creating a brand-new record, check whether this looks like someone who already
        // exists, so we don't silently create a duplicate.
        var candidates = await identityRepository.Query(new IdentityFilter { NameFilter = result.FullName })
            .LoadMultiple();
        var sameName = candidates
            .Where(i => i.FullName.Equals(result.FullName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Someone with this name is already a student: there's no identity to convert, so don't offer
        // "use existing" - just warn, and let the instructor decide whether this really is a different
        // person (two students can share a name).
        if (sameName.OfType<Student>().FirstOrDefault() is { } existingStudent)
        {
            var createAnyway = await dialogService.ConfirmAsync("A student with this name already exists",
                $"{existingStudent.FullName} is already a student. Create a new, separate student with the "
                + "same name anyway?",
                confirmLabel: "Create new student");
            if (!createAnyway)
                return null;

            var duplicateId = await studentRepository.AddAsync(result);
            toastService.ShowInfo($"Added {result.FullName}.");
            return duplicateId;
        }

        // Someone with this name exists but isn't a student yet (e.g. someone else's emergency
        // contact) - offer to convert them rather than create a duplicate; see PromoteConfirmViewModel.
        if (sameName.FirstOrDefault() is { } match)
        {
            var confirmed = await dialogService.ShowDialogAsync(new PromoteConfirmViewModel(match, result));

            if (confirmed)
            {
                result.Id = match.Id;
                await studentRepository.PromoteToStudentAsync(result);
                toastService.ShowInfo($"Linked to the existing record for {result.FullName}.");
                return result.Id;
            }
        }

        var newId = await studentRepository.AddAsync(result);
        toastService.ShowInfo($"Added {result.FullName}.");
        return newId;
    }
}
