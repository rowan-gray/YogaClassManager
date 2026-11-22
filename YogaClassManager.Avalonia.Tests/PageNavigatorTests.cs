using System.Reactive.Linq;
using ReactiveUI;
using YogaClassManager.Avalonia.Services;
using YogaClassManager.Avalonia.ViewModels.ClassRolls;
using YogaClassManager.Avalonia.ViewModels.Identities;
using YogaClassManager.Avalonia.ViewModels.Students;
using YogaClassManager.Core.Dummy;
using YogaClassManager.Core.Models.Classes;
using YogaClassManager.Core.Models.People;

namespace YogaClassManager.Avalonia.Tests;

/// <summary>
///     The real PageNavigator, end to end: "view this student/person/roll" lands on a freshly built page
///     that selects (or opens) that record, and the hand-off is used up - so a later visit to the same
///     page doesn't jump back to it. FakeNavigator stands in for this everywhere else; this is what it
///     fakes.
/// </summary>
public class PageNavigatorTests
{
    private sealed record Fixture(PageNavigator Navigator, AppScreen Screen, FakeRollWindowService RollWindows,
        Func<Type, object?> Resolve, int StudentId, int IdentityId, int RollId);

    private static async Task<Fixture> CreateAsync()
    {
        var store = new InMemoryDataStore();
        var identities = new InMemoryIdentityRepository(store);
        var passes = new InMemoryPassRepository(store);
        var contacts = new InMemoryEmergencyContactRepository(store);
        var students = new InMemoryStudentRepository(store, identities, passes, contacts);
        var schedules = new InMemoryClassScheduleRepository(store);
        var rolls = new InMemoryClassRollRepository(store);
        var terms = new InMemoryTermRepository(store);

        await students.AddAsync(new Student(0, "Ada", "Lovelace", "0400000000", null, true));
        var studentId = await students.AddAsync(new Student(0, "Grace", "Hopper", "0400000001", null, true));
        var identityId = await identities.AddAsync(new Identity(0, "Kim", "Contact", "0400000002", null, true));
        var scheduleId = await schedules.AddAsync(new ClassSchedule(0, DayOfWeek.Monday, new TimeOnly(9, 0), false));
        var schedule = Assert.Single(await schedules.Query(new() { Id = (uint)scheduleId }).LoadMultiple());
        var rollId = await rolls.AddAsync(new ClassRoll(0, DateOnly.FromDateTime(DateTime.Today), schedule, []));

        var screen = new AppScreen();
        var rollWindows = new FakeRollWindowService();
        PageNavigator navigator = null!;
        object? Resolve(Type type)
        {
            if (type == typeof(StudentsViewModel))
                return new StudentsViewModel(screen, students, identities, passes, terms, rolls,
                    new ScriptedDialogService(), new FakeToastService(), navigator);
            if (type == typeof(IdentitiesViewModel))
                return new IdentitiesViewModel(screen, identities, students, contacts,
                    new ScriptedDialogService(), new FakeToastService(), navigator);
            if (type == typeof(ClassRollsViewModel))
                return new ClassRollsViewModel(screen, rolls, students, passes, terms,
                    new ScriptedDialogService(), new FakeToastService(), rollWindows, navigator);
            return null;
        }

        navigator = new PageNavigator(screen, Resolve);
        return new Fixture(navigator, screen, rollWindows, Resolve, studentId, identityId, rollId);
    }

    private static T CurrentPage<T>(AppScreen screen) => Assert.IsType<T>(screen.Router.NavigationStack.LastOrDefault());

    [Fact]
    public async Task ToStudent_OpensTheStudentsPage_WithThatStudentSelected_Once()
    {
        var fixture = await CreateAsync();

        await fixture.Navigator.ToStudent(fixture.StudentId);

        Assert.Equal(fixture.StudentId, CurrentPage<StudentsViewModel>(fixture.Screen).Selection?.Id);
        Assert.Null(fixture.Navigator.TakePendingStudentSelection());
    }

    [Fact]
    public async Task ToIdentity_OpensThePeoplePage_WithThatPersonSelected_Once()
    {
        var fixture = await CreateAsync();

        await fixture.Navigator.ToIdentity(fixture.IdentityId);

        Assert.Equal(fixture.IdentityId, CurrentPage<IdentitiesViewModel>(fixture.Screen).Selection?.Id);
        Assert.Null(fixture.Navigator.TakePendingIdentitySelection());
    }

    [Fact]
    public async Task ToClassRoll_OpensTheClassRollsPage_AndOpensThatRoll_Once()
    {
        var fixture = await CreateAsync();

        await fixture.Navigator.ToClassRoll(fixture.RollId);

        Assert.IsType<ClassRollsViewModel>(fixture.Screen.Router.NavigationStack.LastOrDefault());
        Assert.Equal(fixture.RollId, Assert.Single(fixture.RollWindows.Opened).Id);
        Assert.Null(fixture.Navigator.TakePendingClassRollToView());
    }

    [Fact]
    public async Task ASecondVisit_DoesNotReapplyTheEarlierSelection()
    {
        var fixture = await CreateAsync();
        await fixture.Navigator.ToStudent(fixture.StudentId);

        // What the nav rail does: a fresh page, with no hand-off of its own.
        var secondVisit = (StudentsViewModel)fixture.Resolve(typeof(StudentsViewModel))!;
        await fixture.Screen.Router.Navigate.Execute(secondVisit);

        // The page's usual default (its first row, Ada), not Grace from the earlier hand-off.
        var page = CurrentPage<StudentsViewModel>(fixture.Screen);
        Assert.NotEqual(fixture.StudentId, page.Selection?.Id);
        Assert.Same(page.Items[0], page.Selection);
    }

    [Fact]
    public async Task EachHandOff_IsTakenIndependently_AndOnlyOnce()
    {
        var fixture = await CreateAsync();
        fixture.Screen.PendingStudentSelectionId = 1;
        fixture.Screen.PendingIdentitySelectionId = 2;
        fixture.Screen.PendingClassScheduleSelectionId = 3;
        fixture.Screen.PendingClassRollIdToView = 4;

        Assert.Equal(3, fixture.Navigator.TakePendingClassScheduleSelection());
        Assert.Null(fixture.Navigator.TakePendingClassScheduleSelection());

        Assert.Equal(1, fixture.Navigator.TakePendingStudentSelection());
        Assert.Equal(2, fixture.Navigator.TakePendingIdentitySelection());
        Assert.Equal(4, fixture.Navigator.TakePendingClassRollToView());
        Assert.Null(fixture.Navigator.TakePendingStudentSelection());
        Assert.Null(fixture.Navigator.TakePendingIdentitySelection());
        Assert.Null(fixture.Navigator.TakePendingClassRollToView());
    }

    [Fact]
    public void NavigatingToAnUnregisteredPage_SaysWhereToRegisterIt()
    {
        var navigator = new PageNavigator(new AppScreen(), _ => null);

        var exception = Assert.Throws<InvalidOperationException>(() => navigator.ToStudent(1));

        Assert.Contains("StudentsViewModel is not registered", exception.Message);
        Assert.Contains("ConfigureServices", exception.Message);
    }
}
