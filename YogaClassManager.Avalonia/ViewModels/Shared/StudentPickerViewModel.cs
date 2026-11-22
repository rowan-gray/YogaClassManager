using ReactiveUI;
using YogaClassManager.Core.Models.People;
using YogaClassManager.Core.Repositories;

namespace YogaClassManager.Avalonia.ViewModels.Shared;

/// <summary>
///     "Pick a student" modal built on the same search as the Mark Roll window's "Add a student"
///     (<see cref="StudentSearchViewModel" /> + Controls/PersonSearchBox): type a name and press Enter
///     when the search finds just one student, arrow to a row and press Enter, double-click a row, or select one and press
///     Select. Used by the Dashboard's Add pass and Search student quick actions.
/// </summary>
public class StudentPickerViewModel : DialogViewModelBase<Student>
{
    private string? searchError;

    /// <param name="excludeIds">Students who can't be picked (e.g. already on the roll being added to).</param>
    public StudentPickerViewModel(IStudentRepository studentRepository, string title = "Select a student",
        IReadOnlyCollection<int>? excludeIds = null)
    {
        Title = title;
        Search = new StudentSearchViewModel(studentRepository,
            student =>
            {
                Close(student);
                return Task.CompletedTask;
            },
            exception => SearchError = $"Couldn't search students: {exception.Message}",
            () => excludeIds ?? []);

        // The dialog's Select button and DialogHost's Enter both choose exactly what Enter in the
        // search box would - the highlighted row, else the only result.
        DefaultCommand = Search.ChooseSelectedCommand;
    }

    public string Title { get; }

    public StudentSearchViewModel Search { get; }

    /// <summary>A failed search, shown under the list - otherwise it would look like "no matches".</summary>
    public string? SearchError
    {
        get => searchError;
        private set => this.RaiseAndSetIfChanged(ref searchError, value);
    }
}
