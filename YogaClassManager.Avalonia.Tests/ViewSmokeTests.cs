using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using YogaClassManager.Avalonia;
using YogaClassManager.Avalonia.Controls;
using YogaClassManager.Avalonia.Services;
using YogaClassManager.Avalonia.ViewModels.ClassSchedules;
using YogaClassManager.Avalonia.ViewModels.Terms;
using YogaClassManager.Core.Dummy;
using YogaClassManager.Core.Models.Classes;

namespace YogaClassManager.Avalonia.Tests;

/// <summary>
///     Renders real views against real ViewModels on the headless platform. These catch the class of
///     bug the build cannot: a XAML file that compiles but throws on load, and a binding path that
///     silently resolves to nothing at runtime.
///
///     The sort/filter assertions lean on a property of the shared controls: SortByButton hides itself
///     when SortOptions is null and FilterButton hides itself when Filters is null. So "the button is
///     visible" is a genuine assertion that the host's binding resolved, not just that markup parsed.
/// </summary>
public class ViewSmokeTests
{
    private static T Render<T>(T view, object viewModel) where T : Control
    {
        view.DataContext = viewModel;
        var window = new Window { Content = view, Width = 1200, Height = 800 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return view;
    }

    private static TControl FindOne<TControl>(Control root) where TControl : Control =>
        Assert.Single(root.GetVisualDescendants().OfType<TControl>());

    private static ClassSchedulesViewModel CreateClassSchedulesViewModel()
    {
        var store = new InMemoryDataStore();
        var scheduleRepository = new InMemoryClassScheduleRepository(store);
        scheduleRepository.AddAsync(new ClassSchedule(0, DayOfWeek.Monday, new TimeOnly(9, 0), false))
            .GetAwaiter().GetResult();

        return new ClassSchedulesViewModel(new AppScreen(), scheduleRepository,
            new InMemoryClassRollRepository(store), new DialogService(), new FakeToastService(),
            new FakeRollWindowService(), new FakeNavigator());
    }

    private static TermsViewModel CreateTermsViewModel()
    {
        var store = new InMemoryDataStore();
        return new TermsViewModel(new AppScreen(), new InMemoryTermRepository(store),
            new InMemoryClassScheduleRepository(store), new DialogService(), new FakeToastService());
    }

    [AvaloniaFact]
    public void ClassSchedulesView_Renders()
    {
        var view = Render(new Views.ClassSchedules.ClassSchedulesView(), CreateClassSchedulesViewModel());

        Assert.True(view.IsVisible);
    }

    [AvaloniaFact]
    public void ClassSchedulesView_ExposesSortAndFilterControls()
    {
        var view = Render(new Views.ClassSchedules.ClassSchedulesView(), CreateClassSchedulesViewModel());

        // The page-level pair. LinkedRecordsPanel adds its own pair for the nested Class rolls list, so
        // filter to the ones that are not inside it.
        var pageSortButtons = view.GetVisualDescendants().OfType<SortByButton>()
            .Where(b => b.FindAncestorOfType<LinkedRecordsPanel>() is null).ToList();
        var pageFilterButtons = view.GetVisualDescendants().OfType<FilterButton>()
            .Where(b => b.FindAncestorOfType<LinkedRecordsPanel>() is null).ToList();

        Assert.Single(pageSortButtons);
        Assert.Single(pageFilterButtons);
        Assert.NotNull(pageSortButtons[0].SortOptions);
        Assert.NotNull(pageFilterButtons[0].Filters);
    }

    [AvaloniaFact]
    public void ClassSchedulesView_HasAnAddClassButton_BoundToAddClassCommand()
    {
        var viewModel = CreateClassSchedulesViewModel();
        var view = Render(new Views.ClassSchedules.ClassSchedulesView(), viewModel);

        var addButton = view.GetVisualDescendants().OfType<Button>()
            .SingleOrDefault(b => b.Content as string == "Add class");

        Assert.NotNull(addButton);
        Assert.Same(viewModel.AddClassCommand, addButton.Command);
    }

    [AvaloniaFact]
    public void TermsView_Renders()
    {
        var view = Render(new Views.Terms.TermsView(), CreateTermsViewModel());

        Assert.True(view.IsVisible);
    }

    [AvaloniaFact]
    public void TermsView_UsesFilterBar_RatherThanAHandRolledSearchRow()
    {
        var view = Render(new Views.Terms.TermsView(), CreateTermsViewModel());

        var filterBar = FindOne<FilterBar>(view);

        Assert.NotNull(filterBar.SortOptions);
        Assert.NotNull(filterBar.AdvancedFilters);
    }

    [AvaloniaFact]
    public async Task MasterDetailView_ShowsEmptyText_WhenTheListLoadsNothing()
    {
        var viewModel = CreateTermsViewModel();
        var view = Render(new Views.Terms.TermsView(), viewModel);

        await viewModel.RefreshCommand.Execute();
        Dispatcher.UIThread.RunJobs();

        var masterDetail = FindOne<MasterDetailView>(view);
        Assert.True(masterDetail.IsEmpty);

        var emptyLabel = view.GetVisualDescendants().OfType<TextBlock>()
            .SingleOrDefault(t => t.Text == "No terms match this search.");
        Assert.NotNull(emptyLabel);
        Assert.True(emptyLabel.IsVisible);
    }

    [AvaloniaFact]
    public async Task MasterDetailView_HidesEmptyText_OnceTheListHasItems()
    {
        var viewModel = CreateClassSchedulesViewModel();
        var view = Render(new Views.ClassSchedules.ClassSchedulesView(), viewModel);

        await viewModel.RefreshCommand.Execute();
        Dispatcher.UIThread.RunJobs();

        var masterDetail = FindOne<MasterDetailView>(view);
        Assert.False(masterDetail.IsEmpty);

        var emptyLabel = view.GetVisualDescendants().OfType<TextBlock>()
            .SingleOrDefault(t => t.Text == "No class schedules match these filters.");
        Assert.NotNull(emptyLabel);
        Assert.False(emptyLabel.IsVisible);
    }

    [AvaloniaFact]
    public async Task MasterList_RowsRenderAsRecordListItems_WithTheirTitleResolved()
    {
        var viewModel = CreateClassSchedulesViewModel();
        var view = Render(new Views.ClassSchedules.ClassSchedulesView(), viewModel);

        await viewModel.RefreshCommand.Execute();
        Dispatcher.UIThread.RunJobs();

        // Title binds the ClassSchedule itself, not a string property, so this also pins down that the
        // object-to-string conversion the extracted control now relies on actually happens.
        // Compared against ToString() rather than a literal, since the time part is culture-formatted.
        var row = Assert.Single(view.GetVisualDescendants().OfType<RecordListItem>());
        Assert.Equal(viewModel.Items[0].ToString(), row.Title);
        Assert.StartsWith("Monday", row.Title);
        Assert.False(row.IsFlagged);
    }

    [AvaloniaFact]
    public async Task AnArchivedRow_ShowsATag_RatherThanSmallColouredText()
    {
        var store = new InMemoryDataStore();
        var scheduleRepository = new InMemoryClassScheduleRepository(store);
        await scheduleRepository.AddAsync(new ClassSchedule(0, DayOfWeek.Tuesday, new TimeOnly(18, 0), true));

        var viewModel = new ClassSchedulesViewModel(new AppScreen(), scheduleRepository,
            new InMemoryClassRollRepository(store), new DialogService(), new FakeToastService(),
            new FakeRollWindowService(), new FakeNavigator())
        {
            IncludeArchived = true
        };

        var view = Render(new Views.ClassSchedules.ClassSchedulesView(), viewModel);
        await viewModel.RefreshCommand.Execute();
        Dispatcher.UIThread.RunJobs();

        var row = Assert.Single(view.GetVisualDescendants().OfType<RecordListItem>());
        Assert.True(row.IsFlagged);

        var tag = Assert.Single(row.GetVisualDescendants().OfType<Tag>());
        Assert.Equal("ARCHIVED", tag.Label);
        Assert.True(tag.IsVisible);
    }

    [AvaloniaFact]
    public void IdentitiesView_Renders()
    {
        var store = new InMemoryDataStore();
        var identityRepository = new InMemoryIdentityRepository(store);
        var passRepository = new InMemoryPassRepository(store);
        var emergencyContactRepository = new InMemoryEmergencyContactRepository(store);
        var studentRepository = new InMemoryStudentRepository(store, identityRepository, passRepository,
            emergencyContactRepository);

        var viewModel = new ViewModels.Identities.IdentitiesViewModel(new AppScreen(), identityRepository,
            studentRepository, emergencyContactRepository, new DialogService(), new FakeToastService(),
            new FakeNavigator());

        var view = Render(new Views.Identities.IdentitiesView(), viewModel);

        Assert.True(view.IsVisible);
    }
}
