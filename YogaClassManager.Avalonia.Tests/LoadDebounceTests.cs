using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ReactiveUI;
using YogaClassManager.Avalonia.ViewModels;
using YogaClassManager.Avalonia.ViewModels.Base;
using YogaClassManager.Avalonia.ViewModels.Identities;
using YogaClassManager.Avalonia.Views.Identities;
using YogaClassManager.Core.Dummy;
using YogaClassManager.Core.Models.People;

namespace YogaClassManager.Avalonia.Tests;

/// <summary>LoadDebounce is a static every other test relies on being zero, so these run alone.</summary>
[CollectionDefinition(nameof(LoadDebounceTests), DisableParallelization = true)]
public class LoadDebounceCollection;

/// <summary>
///     Fetches wait out a short debounce - a page's first load, and a list-and-details page's details on
///     a selection change - with the skeleton showing meanwhile, so clicking quickly through pages or
///     records only fetches for the one you stop on.
/// </summary>
[Collection(nameof(LoadDebounceTests))]
public class LoadDebounceTests : IDisposable
{
    public LoadDebounceTests() => ViewModelBase.LoadDebounce = TimeSpan.FromMilliseconds(150);

    public void Dispose() => ViewModelBase.LoadDebounce = TimeSpan.Zero;

    private static void PumpFor(int milliseconds)
    {
        var until = DateTime.UtcNow.AddMilliseconds(milliseconds);
        while (DateTime.UtcNow < until)
        {
            if (Dispatcher.UIThread.CheckAccess())
                Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }
        if (Dispatcher.UIThread.CheckAccess())
            Dispatcher.UIThread.RunJobs();
    }

    private sealed class TestPage : CollectionPageModelBase<string>, IRoutableViewModel
    {
        private readonly Subject<int> selectionTrigger = new();

        public TestPage(IScreen screen) : base(new FakeToastService())
        {
            HostScreen = screen;
            ReloadOn(selectionTrigger, LoadDetailsAsync, "the details", debounce: true,
                clearNow: () => Details.Clear());
        }

        public string UrlPathSegment => "test";
        public IScreen HostScreen { get; }
        public int ListLoads { get; private set; }
        public List<int> DetailLoads { get; } = [];
        public List<string> Details { get; } = ["stale details"];
        private int lastTrigger;

        public void Select(int value)
        {
            lastTrigger = value;
            selectionTrigger.OnNext(value);
        }

        protected override Task<IReadOnlyList<string>> LoadItemsAsync()
        {
            ListLoads++;
            return Task.FromResult<IReadOnlyList<string>>(["Ada"]);
        }

        private Task LoadDetailsAsync(CancellationToken cancellationToken)
        {
            DetailLoads.Add(lastTrigger);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public void RapidSelectionChanges_ClearAtOnce_ButOnlyLoadTheLastOne()
    {
        var page = new TestPage(new AppScreen());

        page.Select(1);
        Assert.Empty(page.Details);             // the previous record's details vanish straight away
        Assert.True(page.IsLoadingDetails);     // so the skeletons show during the wait

        page.Select(2);
        page.Select(3);
        Assert.Empty(page.DetailLoads);         // nothing fetched yet

        PumpFor(400);
        Assert.Equal([3], page.DetailLoads);    // only the one we stopped on
        Assert.False(page.IsLoadingDetails);
    }

    // UI test: IsBusy follows the command's IsExecuting, which is delivered on the UI scheduler.
    [AvaloniaFact]
    public void APagesFirstLoad_WaitsOutTheDebounce_ShowingBusy()
    {
        var page = new TestPage(new AppScreen());

        page.RefreshCommand.Execute().Subscribe();
        Dispatcher.UIThread.RunJobs();
        Assert.True(page.IsBusy);               // the list skeleton is up
        Assert.Equal(0, page.ListLoads);        // ...but nothing fetched yet

        PumpFor(400);
        Assert.Equal(1, page.ListLoads);
        Assert.Equal(["Ada"], page.Items);
        Assert.False(page.IsBusy);
    }

    [Fact]
    public async Task APageLeftDuringTheDebounce_NeverFetches()
    {
        var screen = new AppScreen();
        var first = new TestPage(screen);
        await screen.Router.Navigate.Execute(first);

        var refresh = first.RefreshCommand.Execute().ToTask();
        await screen.Router.Navigate.Execute(new TestPage(screen)); // moved on before the wait ended
        await refresh;

        Assert.Equal(0, first.ListLoads);
    }

    [AvaloniaFact]
    public async Task IdentitiesPage_ShowsASkeletonForTheLinkCards_WhileTheyLoad()
    {
        var store = new InMemoryDataStore();
        var identities = new InMemoryIdentityRepository(store);
        var contacts = new InMemoryEmergencyContactRepository(store);
        var students = new InMemoryStudentRepository(store, identities, new InMemoryPassRepository(store), contacts);
        var judiId = await students.AddAsync(new Student(0, "Judi", "Dench", "0400000000", null, true));

        var viewModel = new IdentitiesViewModel(new AppScreen(), identities, students, contacts,
            new ScriptedDialogService(), new FakeToastService(), new FakeNavigator());
        var window = new Window { Content = new IdentitiesView { DataContext = viewModel }, Width = 1200, Height = 800 };
        window.Show();
        PumpFor(400); // the page's own first load
        viewModel.Selection = null;
        Dispatcher.UIThread.RunJobs();

        viewModel.Selection = viewModel.Items.Single(i => i.Id == judiId);
        Dispatcher.UIThread.RunJobs();

        // The link-card placeholder - not the main list's card, which holds a SkeletonList of its own.
        Border SkeletonCard() => window.GetVisualDescendants().OfType<Border>()
            .First(b => b.Classes.Contains("Card") &&
                        !b.GetVisualDescendants().OfType<Avalonia.Controls.SkeletonList>().Any() &&
                        b.GetVisualDescendants().OfType<Border>().Any(inner => inner.Classes.Contains("Skeleton")));

        Assert.True(viewModel.IsLoadingDetails);
        Assert.True(SkeletonCard().IsVisible);

        PumpFor(400);
        Assert.False(SkeletonCard().IsVisible);
        Assert.NotNull(viewModel.LinkedStudent); // the real Student card's data has arrived
        window.Close();
    }

    /// <summary>Every list-and-details page shows its main list's skeleton while its first load waits
    /// out the debounce, then the real rows.</summary>
    [AvaloniaTheory]
    [InlineData("Students")]
    [InlineData("Terms")]
    [InlineData("ClassRolls")]
    public void TheMainList_ShowsItsSkeleton_DuringThePagesDebounce_ThenTheRows(string pageName)
    {
        var store = new InMemoryDataStore();
        DummyDataSeeder.Seed(store);
        var identities = new InMemoryIdentityRepository(store);
        var passes = new InMemoryPassRepository(store);
        var students = new InMemoryStudentRepository(store, identities, passes, new InMemoryEmergencyContactRepository(store));
        var rolls = new InMemoryClassRollRepository(store);
        var terms = new InMemoryTermRepository(store);
        var screen = new AppScreen();

        Control view = pageName switch
        {
            "Students" => new Views.Students.StudentsView
            {
                DataContext = new ViewModels.Students.StudentsViewModel(screen, students, identities, passes, terms, rolls,
                    new ScriptedDialogService(), new FakeToastService(), new FakeNavigator())
            },
            "Terms" => new Views.Terms.TermsView
            {
                DataContext = new ViewModels.Terms.TermsViewModel(screen, terms, new InMemoryClassScheduleRepository(store),
                    new ScriptedDialogService(), new FakeToastService())
            },
            _ => new Views.ClassRolls.ClassRollsView
            {
                DataContext = new ViewModels.ClassRolls.ClassRollsViewModel(screen, rolls, students, passes, terms,
                    new ScriptedDialogService(), new FakeToastService(), new FakeRollWindowService(), new FakeNavigator())
            }
        };
        var window = new Window { Content = view, Width = 1200, Height = 800 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var skeleton = window.GetVisualDescendants().OfType<Avalonia.Controls.SkeletonList>()
            .First(s => s.FindAncestorOfType<Avalonia.Controls.MasterDetailView>() is not null &&
                        s.FindAncestorOfType<Avalonia.Controls.LinkedRecordsPanel>() is null);
        Assert.True(skeleton.IsVisible); // waiting out the debounce

        PumpFor(400);
        Assert.False(skeleton.IsVisible);
        Assert.NotEmpty(window.GetVisualDescendants().OfType<ListBoxItem>());
        window.Close();
    }
}
