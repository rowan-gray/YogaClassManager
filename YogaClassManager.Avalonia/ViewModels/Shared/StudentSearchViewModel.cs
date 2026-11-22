using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models.People;
using YogaClassManager.Core.Repositories;

namespace YogaClassManager.Avalonia.ViewModels.Shared;

/// <summary>
///     The shared person search (<see cref="PersonSearchViewModel{TPerson}" />, Controls/PersonSearchBox)
///     over <b>active students</b>. Used by the Mark Roll window (choosing adds the student to the roll)
///     and <see cref="StudentPickerViewModel" /> (choosing closes the picker with that student).
/// </summary>
public class StudentSearchViewModel : PersonSearchViewModel<Student>
{
    /// <param name="excludeIds">Students to leave out of the results (e.g. already on the roll),
    /// re-read on every search.</param>
    public StudentSearchViewModel(IStudentRepository studentRepository, Func<Student, Task> onChosen,
        Action<Exception> onSearchError, Func<IReadOnlyCollection<int>>? excludeIds = null)
        : base(async query => await studentRepository
                .Query(new StudentFilter
                {
                    NameFilter = query,
                    IsActive = true,
                    ExcludeIds = (excludeIds?.Invoke() ?? []).ToList()
                })
                .LoadMultiple(),
            onChosen, onSearchError,
            promptText: "Type a name to find a student.",
            noMatchText: "No students match the current search.")
    {
    }
}
