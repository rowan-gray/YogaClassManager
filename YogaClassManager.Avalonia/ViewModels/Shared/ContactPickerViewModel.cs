using ReactiveUI;
using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models.People;
using YogaClassManager.Core.Repositories;

namespace YogaClassManager.Avalonia.ViewModels.Shared;

/// <summary>
///     "Pick a person" modal for choosing an emergency contact - the same search as the student picker
///     and the Mark Roll window (<see cref="PersonSearchViewModel{TPerson}" /> + Controls/PersonSearchBox),
///     but over any active identity, since a contact needn't be a student. Type a name and press Enter
///     when the search finds just one person, arrow to a row and press Enter, double-click a row, or select one and press
///     Select.
/// </summary>
public class ContactPickerViewModel : DialogViewModelBase<Identity>
{
    private string? searchError;

    /// <param name="excludeIds">People who can't be chosen - the student themselves and anyone who is
    /// already one of their contacts.</param>
    public ContactPickerViewModel(IIdentityRepository identityRepository, string title,
        IReadOnlyCollection<int>? excludeIds = null)
    {
        Title = title;
        var excluded = excludeIds ?? [];

        Search = new PersonSearchViewModel<Identity>(
            async query => (await identityRepository
                    .Query(new IdentityFilter { NameFilter = query, IsActive = true })
                    .LoadMultiple())
                .Where(person => !excluded.Contains(person.Id))
                .OrderBy(person => person.FirstName)
                .ThenBy(person => person.LastName),
            person =>
            {
                Close(person);
                return Task.CompletedTask;
            },
            exception => SearchError = $"Couldn't search people: {exception.Message}",
            promptText: "Type a name to find a person.",
            noMatchText: "No people match the current search.");

        // The dialog's Select button and DialogHost's Enter both choose exactly what Enter in the
        // search box would - the highlighted row, else the only result.
        DefaultCommand = Search.ChooseSelectedCommand;
    }

    public string Title { get; }

    public PersonSearchViewModel<Identity> Search { get; }

    /// <summary>A failed search, shown under the list - otherwise it would look like "no matches".</summary>
    public string? SearchError
    {
        get => searchError;
        private set => this.RaiseAndSetIfChanged(ref searchError, value);
    }
}
