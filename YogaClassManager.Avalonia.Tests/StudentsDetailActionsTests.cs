using System.Collections.ObjectModel;
using System.Reactive.Linq;
using YogaClassManager.Avalonia.ViewModels.Passes;
using YogaClassManager.Avalonia.ViewModels.Shared;
using YogaClassManager.Avalonia.ViewModels.Students;
using YogaClassManager.Core.Dummy;
using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models.Classes;
using YogaClassManager.Core.Models.Passes;
using YogaClassManager.Core.Models.People;

namespace YogaClassManager.Avalonia.Tests;

/// <summary>Actions on the Students page's detail pane: editing a health concern; opening the student's
/// person record, an emergency contact, a pass or a class they attended; and each filter button's Clear.</summary>
public class StudentsDetailActionsTests
{
    private sealed record Fixture(StudentsViewModel ViewModel, InMemoryStudentRepository Students,
        InMemoryIdentityRepository Identities, ScriptedDialogService Dialogs, FakeNavigator Navigator, int StudentId);

    private static async Task<Fixture> CreateAsync(params string[] healthConcerns)
    {
        var store = new InMemoryDataStore();
        var identities = new InMemoryIdentityRepository(store);
        var passes = new InMemoryPassRepository(store);
        var students = new InMemoryStudentRepository(store, identities, passes,
            new InMemoryEmergencyContactRepository(store));
        var studentId = await students.AddAsync(new Student(0, "Judi", "Dench", "0400000000", null, true));
        foreach (var concern in healthConcerns)
            await students.AddHealthConcernAsync(studentId, concern);

        var dialogs = new ScriptedDialogService();
        var navigator = new FakeNavigator();
        var viewModel = new StudentsViewModel(new AppScreen(), students, identities, passes,
            new InMemoryTermRepository(store), new InMemoryClassRollRepository(store), dialogs,
            new FakeToastService(), navigator);

        await viewModel.RefreshCommand.Execute();
        viewModel.Selection = viewModel.Items.Single(s => s.Id == studentId);
        return new Fixture(viewModel, students, identities, dialogs, navigator, studentId);
    }

    [Fact]
    public async Task EditingAHealthConcern_ReplacesIt()
    {
        var fixture = await CreateAsync("Asthma", "Bad knee");
        fixture.Dialogs.Answer<TextInputViewModel>("  Asthma (carries an inhaler) ");

        await fixture.ViewModel.EditHealthConcernCommand.Execute("Asthma");

        var prompt = Assert.IsType<TextInputViewModel>(Assert.Single(fixture.Dialogs.Shown));
        Assert.Equal("Edit health concern", prompt.Title);
        Assert.Equal(["Asthma (carries an inhaler)", "Bad knee"],
            (await fixture.Students.GetHealthConcernsAsync(fixture.StudentId)).Order());
    }

    [Theory]
    [InlineData(null)]       // cancelled
    [InlineData("   ")]      // blanked out
    [InlineData("Asthma")]   // unchanged
    public async Task EditingAHealthConcern_ThatIsCancelledBlankOrUnchanged_LeavesItAlone(string? answer)
    {
        var fixture = await CreateAsync("Asthma");
        fixture.Dialogs.Answer<TextInputViewModel>(answer);

        await fixture.ViewModel.EditHealthConcernCommand.Execute("Asthma");

        Assert.Equal(["Asthma"], await fixture.Students.GetHealthConcernsAsync(fixture.StudentId));
    }

    [Fact]
    public async Task ViewIdentity_OnAnEmergencyContact_OpensThatContactsIdentity()
    {
        var fixture = await CreateAsync();
        var contactId = await fixture.Identities.AddAsync(new Identity(0, "Kenneth", "Branagh", "0411111111", null, true));
        await fixture.Students.LinkEmergencyContactAsync(fixture.StudentId, contactId, Relationship.Friend);
        var contact = new EmergencyContact(
            (await fixture.Identities.Query(new() { Id = (uint)contactId }).LoadSingle())!, fixture.StudentId,
            Relationship.Friend);

        await fixture.ViewModel.ViewEmergencyContactCommand.Execute(contact);

        Assert.Equal([("identity", contactId)], fixture.Navigator.Navigations);
    }

    [Fact]
    public async Task ViewIdentity_OpensTheSelectedStudentsPersonRecord()
    {
        var fixture = await CreateAsync();

        await fixture.ViewModel.ViewIdentityCommand.Execute();

        Assert.Equal(("identity", fixture.StudentId), Assert.Single(fixture.Navigator.Navigations));
    }

    [Fact]
    public async Task ViewClass_OpensTheRollThatAttendanceWasFor()
    {
        var fixture = await CreateAsync();
        var record = new ClassAttendanceRecord(17, DateOnly.FromDateTime(DateTime.Today),
            new ClassSchedule(1, DayOfWeek.Monday, new TimeOnly(9, 0), false), fixture.StudentId, null);

        await fixture.ViewModel.ViewClassCommand.Execute(record);

        Assert.Equal(("class-roll", 17), Assert.Single(fixture.Navigator.Navigations));
    }

    [Fact]
    public async Task ViewPass_OpensThatPassesDetailPage()
    {
        var fixture = await CreateAsync();
        var pass = new CasualPass(5, fixture.StudentId, 10, new ObservableCollection<PassAlteration>(), 0);

        await fixture.ViewModel.ViewPassCommand.Execute(pass);

        var page = Assert.IsType<PassDetailViewModel>(fixture.ViewModel.HostScreen.Router.NavigationStack.Last());
        Assert.Same(pass, page.Pass);
    }

    // ---- Each filter button's "Clear" ----

    [Fact]
    public async Task ClearFilters_ResetsTheStudentListFilters()
    {
        var viewModel = (await CreateAsync()).ViewModel;
        viewModel.IncludeArchived = true;
        viewModel.LastAttendedFrom = new DateOnly(2026, 1, 1);
        viewModel.LastAttendedTo = new DateOnly(2026, 6, 30);
        Assert.Equal(3, viewModel.ActiveFilterCount);

        await viewModel.ClearFiltersCommand.Execute();

        Assert.Equal(0, viewModel.ActiveFilterCount);
        Assert.False(viewModel.IncludeArchived);
        Assert.Null(viewModel.LastAttendedFrom);
        Assert.Null(viewModel.LastAttendedTo);
    }

    [Fact]
    public async Task ClearPassFilters_ResetsThePassListFilters()
    {
        var viewModel = (await CreateAsync()).ViewModel;
        viewModel.PassKindFilter = PassKind.Term;
        viewModel.IncludeExpiredPasses = false;
        viewModel.IncludeDepletedPasses = true;
        Assert.Equal(3, viewModel.PassFilterCount);

        await viewModel.ClearPassFiltersCommand.Execute();

        Assert.Equal(0, viewModel.PassFilterCount);
        Assert.Equal(PassKind.Any, viewModel.PassKindFilter);
        Assert.True(viewModel.IncludeExpiredPasses);
        Assert.False(viewModel.IncludeDepletedPasses);
    }

    [Fact]
    public async Task ClearAttendanceFilters_ResetsToTheLastSixMonths()
    {
        var viewModel = (await CreateAsync()).ViewModel;
        var defaultFrom = viewModel.AttendanceDateFrom;
        viewModel.AttendanceDateFrom = new DateOnly(2020, 1, 1);
        viewModel.AttendanceDateTo = new DateOnly(2020, 12, 31);
        viewModel.AttendanceDayOfWeek = DayOfWeek.Friday;
        Assert.Equal(3, viewModel.AttendanceFilterCount);

        await viewModel.ClearAttendanceFiltersCommand.Execute();

        Assert.Equal(0, viewModel.AttendanceFilterCount);
        Assert.Equal(DateOnly.FromDateTime(DateTime.Today).AddMonths(-6), defaultFrom);
        Assert.Equal(defaultFrom, viewModel.AttendanceDateFrom);
        Assert.Null(viewModel.AttendanceDateTo);
        Assert.Null(viewModel.AttendanceDayOfWeek);
    }
}
