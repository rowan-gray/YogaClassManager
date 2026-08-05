using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;
using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models.People;
using YogaClassManager.Core.Repositories;

namespace YogaClassManager.Avalonia.ViewModels.Shared;

/// <summary>
///     A generic "search and pick an Identity" modal, generalizing the MAUI app's SearchPeoplePageModel.
///     Reused for emergency-contact linking and for the merge-duplicates flow (pick identity A, then B).
///     A future StudentPickerViewModel/ClassSchedulePickerViewModel etc. would follow this same shape.
/// </summary>
public class IdentityPickerViewModel : DialogViewModelBase<Identity>
{
    private readonly IIdentityRepository identityRepository;
    private string? searchQuery;
    private Identity? selectedItem;
    private bool showArchived;
    private IReadOnlyCollection<int>? excludeIds;
    private bool excludeStudents;
    private string? validationError;

    public IdentityPickerViewModel(IIdentityRepository identityRepository, string title = "Select an identity",
        IReadOnlyCollection<int>? excludeIds = null, bool excludeStudents = false)
    {
        this.identityRepository = identityRepository;
        Title = title;
        this.excludeIds = excludeIds;
        this.excludeStudents = excludeStudents;

        var canSelect = this.WhenAnyValue(x => x.SelectedItem).Select(item => item is not null);
        SelectCommand = ReactiveCommand.Create(() => Close(SelectedItem), canSelect);
        DefaultCommand = SelectCommand;

        // Skip(1): WhenAnyValue replays the current values on subscription, so without it the initial
        // load below runs a second time ~200ms later - the footgun SearchableCollectionPageModelBase
        // documents, which this hand-rolled debounce was missing.
        var searchCommand = ReactiveCommand.CreateFromTask(SearchAsync);
        searchCommand.ThrownExceptions.Subscribe(exception =>
            ValidationError = $"Couldn't search identities: {exception.Message}");

        this.WhenAnyValue(x => x.SearchQuery, x => x.ShowArchived)
            .Skip(1)
            .Throttle(TimeSpan.FromMilliseconds(200), RxApp.MainThreadScheduler)
            .DistinctUntilChanged()
            .Select(_ => Unit.Default)
            .InvokeCommand(searchCommand);

        LoadOnCreate(SearchAsync,
            exception => ValidationError = $"Couldn't load identities: {exception.Message}");
    }

    /// <summary>Where a failed search or initial load is reported - without it the list just stays
    /// empty, indistinguishable from "no matches".</summary>
    public string? ValidationError
    {
        get => validationError;
        private set => this.RaiseAndSetIfChanged(ref validationError, value);
    }

    public string Title { get; }

    public string? SearchQuery
    {
        get => searchQuery;
        set => this.RaiseAndSetIfChanged(ref searchQuery, value);
    }

    public bool ShowArchived
    {
        get => showArchived;
        set => this.RaiseAndSetIfChanged(ref showArchived, value);
    }

    public Identity? SelectedItem
    {
        get => selectedItem;
        set => this.RaiseAndSetIfChanged(ref selectedItem, value);
    }

    public ObservableCollection<Identity> Results { get; } = new();

    /// <summary>Drives the "no matches" line, so a search that finds nothing reads as an answer rather
    /// than as a list that hasn't loaded.</summary>
    public bool HasNoResults => Results.Count == 0;

    public ReactiveCommand<Unit, Unit> SelectCommand { get; }

    private async Task SearchAsync()
    {
        var identities = await identityRepository.Query(new IdentityFilter { IsActive = ShowArchived ? null : true })
            .LoadMultiple();

        var query = SearchQuery?.Trim();

        var filtered = identities
            .Where(p => excludeIds is null || !excludeIds.Contains(p.Id))
            .Where(p => !excludeStudents || p is not Student)
            .Where(p => string.IsNullOrEmpty(query) ||
                        p.FullName.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.FirstName)
            .ThenBy(p => p.LastName);

        Results.Clear();
        foreach (var identity in filtered)
            Results.Add(identity);

        this.RaisePropertyChanged(nameof(HasNoResults));
    }
}
