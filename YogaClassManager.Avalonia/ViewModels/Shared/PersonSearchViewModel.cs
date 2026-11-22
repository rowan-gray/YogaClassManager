using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Windows.Input;
using ReactiveUI;
using YogaClassManager.Core.Models.People;

namespace YogaClassManager.Avalonia.ViewModels.Shared;

/// <summary>What Controls/PersonSearchBox needs from its DataContext beyond plain bindings (the
/// double-click shortcut) - non-generic, since the control can't name the generic type.</summary>
public interface IPersonSearch
{
    ICommand ChooseSelectedCommand { get; }
}

/// <summary>
///     The "find a person by name" search behind <c>Controls/PersonSearchBox</c>: debounced search, the
///     results list, its empty-state text, and the keyboard contract (Enter in the box chooses the match
///     only when the search found exactly one person; Enter or a double-click on a row chooses that row). Generic over the kind of person so
///     the same search finds students (<see cref="StudentSearchViewModel" /> - Mark Roll, the student
///     picker) or any identity (<see cref="ContactPickerViewModel" /> - emergency contacts), with
///     identical behaviour.
/// </summary>
public class PersonSearchViewModel<TPerson> : ReactiveObject, IPersonSearch where TPerson : Identity
{
    private readonly string promptText;
    private readonly string noMatchText;
    private string? searchQuery;
    private bool hasSingleResult;
    private TPerson? selectedSearchResult;
    private bool hasSearchResults;

    /// <param name="search">Finds the people matching a (non-blank) query.</param>
    /// <param name="onChosen">What choosing a person does.</param>
    /// <param name="onSearchError">Where a failed search is reported.</param>
    /// <param name="promptText">Shown before anything is typed ("Type a name to find a student.").</param>
    /// <param name="noMatchText">Shown when nothing matches ("No students match the current search.").</param>
    public PersonSearchViewModel(Func<string, Task<IEnumerable<TPerson>>> search, Func<TPerson, Task> onChosen,
        Action<Exception> onSearchError, string promptText, string noMatchText)
    {
        this.promptText = promptText;
        this.noMatchText = noMatchText;

        ChooseCommand = ReactiveCommand.CreateFromTask(onChosen);

        // One command for every "choose this one" gesture: the highlighted row, or - with nothing
        // highlighted (focus still in the search box) - the result only when it's the *only* one. Picking
        // the top of several matches on Enter was too easy to get wrong (two "Sarah"s), so with several
        // the instructor narrows the search or arrows to the one they mean.
        var canChooseSelected = this
            .WhenAnyValue(x => x.SelectedSearchResult, x => x.HasSingleResult,
                (selected, single) => selected is not null || single);
        ChooseSelectedCommand = ReactiveCommand.CreateFromTask(() =>
        {
            var person = SelectedSearchResult ?? (SearchResults.Count == 1 ? SearchResults[0] : null);
            return person is null ? Task.CompletedTask : onChosen(person);
        }, canChooseSelected);

        this.WhenAnyValue(x => x.SearchQuery)
            // Skip(1): WhenAnyValue replays the current (empty) query immediately, which would run a
            // pointless search on open - the same footgun SearchableCollectionPageModelBase documents.
            .Skip(1)
            .Throttle(TimeSpan.FromMilliseconds(250), RxApp.MainThreadScheduler)
            .DistinctUntilChanged()
            // Switch, not SelectMany: two searches in flight would otherwise both Clear()-and-refill
            // SearchResults, and the slower one could land last and show results for a stale query.
            .Select(query => Observable.FromAsync(() => string.IsNullOrWhiteSpace(query)
                ? Task.FromResult(Enumerable.Empty<TPerson>())
                : search(query.Trim())))
            .Switch()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(ApplySearchResults, onSearchError);
    }

    public string? SearchQuery
    {
        get => searchQuery;
        set => this.RaiseAndSetIfChanged(ref searchQuery, value);
    }

    /// <summary>The row the keyboard is on in the results list. Null while focus is still in the
    /// search box, which is why <see cref="ChooseSelectedCommand" /> falls back to the first result.</summary>
    public TPerson? SelectedSearchResult
    {
        get => selectedSearchResult;
        set => this.RaiseAndSetIfChanged(ref selectedSearchResult, value);
    }

    /// <summary>The search found exactly one person - the only case Enter in the box chooses
    /// without a row highlighted.</summary>
    public bool HasSingleResult
    {
        get => hasSingleResult;
        private set => this.RaiseAndSetIfChanged(ref hasSingleResult, value);
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
        : string.IsNullOrWhiteSpace(SearchQuery) ? promptText : noMatchText;

    public ObservableCollection<TPerson> SearchResults { get; } = new();

    /// <summary>Choose a specific person (a row's action button).</summary>
    public ReactiveCommand<TPerson, Unit> ChooseCommand { get; }

    /// <summary>Enter, or a double-click on a row: the highlighted row, else the only result (never
    /// the first of several).</summary>
    public ReactiveCommand<Unit, Unit> ChooseSelectedCommand { get; }

    ICommand IPersonSearch.ChooseSelectedCommand => ChooseSelectedCommand;

    private void ApplySearchResults(IEnumerable<TPerson> results)
    {
        SearchResults.Clear();
        foreach (var person in results)
            SearchResults.Add(person);

        SelectedSearchResult = null;
        HasSearchResults = SearchResults.Count > 0;
        HasSingleResult = SearchResults.Count == 1;
        this.RaisePropertyChanged(nameof(SearchEmptyText));
    }
}
