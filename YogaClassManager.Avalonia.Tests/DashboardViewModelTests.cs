using System.Collections.ObjectModel;
using System.Reactive.Linq;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using YogaClassManager.Avalonia;
using YogaClassManager.Avalonia.Services;
using YogaClassManager.Avalonia.ViewModels.Dashboard;
using YogaClassManager.Avalonia.ViewModels.Shared;
using YogaClassManager.Core.Dummy;
using YogaClassManager.Core.Models.Classes;
using YogaClassManager.Core.Models.People;
using YogaClassManager.Core.Models.Passes;

namespace YogaClassManager.Avalonia.Tests;

public class DashboardViewModelTests
{
    private sealed record Fixture(
        DashboardViewModel ViewModel,
        InMemoryStudentRepository Students,
        InMemoryClassScheduleRepository Schedules,
        InMemoryClassRollRepository Rolls,
        ScriptedDialogService Dialogs,
        FakeNavigator Navigator,
        FakeSoundPlayer Sounds);

    private static async Task<Fixture> CreateAsync(Func<Fixture, Task>? seed = null)
    {
        var store = new InMemoryDataStore();
        var identities = new InMemoryIdentityRepository(store);
        var passes = new InMemoryPassRepository(store);
        var students = new InMemoryStudentRepository(store, identities, passes,
            new InMemoryEmergencyContactRepository(store));
        var schedules = new InMemoryClassScheduleRepository(store);
        var rolls = new InMemoryClassRollRepository(store);
        var dialogs = new ScriptedDialogService();
        var navigator = new FakeNavigator();
        var sounds = new FakeSoundPlayer();
        var soundsFolder = Path.Combine(Path.GetTempPath(), "dashboard-noot-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(soundsFolder);
        File.WriteAllBytes(Path.Combine(soundsFolder, "noot-noot.mp3"), []);

        // Seed before constructing: the dashboard loads in its constructor.
        var seedOnly = new Fixture(null!, students, schedules, rolls, dialogs, navigator, sounds);
        if (seed is not null)
            await seed(seedOnly);

        var viewModel = new DashboardViewModel(new AppScreen(), students, schedules, rolls, identities, passes,
            new InMemoryTermRepository(store), new FakeRollWindowService(), dialogs, new FakeToastService(),
            navigator, new NootNootEasterEgg(sounds, soundsFolder));
        return seedOnly with { ViewModel = viewModel };
    }

    private static Task<int> AddStudentAsync(InMemoryStudentRepository students, string firstName) =>
        students.AddAsync(new Student(0, firstName, "Example", "0400000000", null, true));

    [Fact]
    public async Task RecentClasses_AreEveryClassFromTheLastSevenDays_NewestFirst()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var fixture = await CreateAsync(async f =>
        {
            var scheduleId = await f.Schedules.AddAsync(new ClassSchedule(0, DayOfWeek.Monday, new TimeOnly(9, 0), false));
            var schedule = (await f.Schedules.Query(new() { Id = (uint)scheduleId }).LoadSingle())!;

            await f.Rolls.AddAsync(new ClassRoll(0, today, schedule, [])); // today: excluded
            // Day 8 is just outside the window.
            for (var daysAgo = 1; daysAgo <= DashboardViewModel.RecentClassDays + 1; daysAgo++)
                await f.Rolls.AddAsync(new ClassRoll(0, today.AddDays(-daysAgo), schedule, []));
        });

        Assert.Equal(
            Enumerable.Range(1, DashboardViewModel.RecentClassDays).Select(daysAgo => today.AddDays(-daysAgo)),
            fixture.ViewModel.RecentClasses.Select(r => r.Date));
        Assert.Single(fixture.ViewModel.TodaysClasses);
    }

    /// <summary>A roll only exists once it's saved, so Today's classes must come from the schedule:
    /// a class on today's weekday shows (as a new, unsaved roll) before anyone has marked it.</summary>
    [Fact]
    public async Task TodaysClasses_IncludeScheduledClassesNotMarkedYet_ByTime()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var otherDay = today.AddDays(1).DayOfWeek;
        int markedId = 0, unmarkedId = 0;
        var fixture = await CreateAsync(async f =>
        {
            async Task<ClassSchedule> Add(DayOfWeek day, int hour, bool archived = false)
            {
                var id = await f.Schedules.AddAsync(new ClassSchedule(0, day, new TimeOnly(hour, 0), archived));
                return (await f.Schedules.Query(new() { Id = (uint)id, IncludeArchived = true }).LoadSingle())!;
            }

            var evening = await Add(today.DayOfWeek, 18);
            markedId = evening.Id;
            unmarkedId = (await Add(today.DayOfWeek, 8)).Id;
            await Add(today.DayOfWeek, 12, archived: true); // archived: not today's class any more
            await Add(otherDay, 9);                        // another weekday

            await f.Rolls.AddAsync(new ClassRoll(0, today, evening, []));
        });

        var todays = fixture.ViewModel.TodaysClasses;
        Assert.Equal([unmarkedId, markedId], todays.Select(r => r.ClassSchedule.Id));
        Assert.Equal(0, todays[0].Id);      // 8am: not marked yet - an unsaved roll
        Assert.NotEqual(0, todays[1].Id);   // 6pm: its existing roll, not a duplicate
        Assert.All(todays, r => Assert.Equal(today, r.Date));
    }

    [Fact]
    public async Task ClickingTheBanner_PlaysANootNoot()
    {
        var fixture = await CreateAsync();

        await fixture.ViewModel.PlayNootCommand.Execute();

        Assert.Equal("noot-noot.mp3", Path.GetFileName(Assert.Single(fixture.Sounds.Played)));
    }

    [Fact]
    public async Task AddStudent_OpensTheNewStudentsPage()
    {
        var fixture = await CreateAsync();
        fixture.Dialogs.Answer<StudentEditViewModel>(new Student(0, "Judi", "Dench", "0400000000", null, true));

        await fixture.ViewModel.AddStudentCommand.Execute();

        var (page, id) = Assert.Single(fixture.Navigator.Navigations);
        Assert.Equal("student", page);
        Assert.Equal("Judi", (await fixture.Students.Query(new() { Id = (uint)id }).LoadSingle())!.FirstName);
    }

    [Fact]
    public async Task AddPass_PicksAStudentThenSavesThePassAgainstThem()
    {
        var studentId = 0;
        var fixture = await CreateAsync(async f => studentId = await AddStudentAsync(f.Students, "Annie"));
        var student = (await fixture.Students.Query(new() { Id = (uint)studentId }).LoadSingle())!;
        var newPass = new CasualPass(0, studentId, 10, new ObservableCollection<PassAlteration>(), 0);
        fixture.Dialogs.Answer<StudentPickerViewModel>(student).Answer<PassEditViewModel>(newPass);

        await fixture.ViewModel.AddPassCommand.Execute();

        // The same student search as the Mark Roll window, not the generic identity picker.
        Assert.IsType<StudentPickerViewModel>(fixture.Dialogs.Shown[0]);
        var passes = await fixture.Students.GetPassesAsync(studentId, includeExpired: true);
        Assert.Contains(passes, p => p is CasualPass { NumberOfClasses: 10 });
    }

    [Fact]
    public async Task SearchStudent_OpensThePickedStudentsPage()
    {
        var studentId = 0;
        var fixture = await CreateAsync(async f => studentId = await AddStudentAsync(f.Students, "Annie"));
        var student = (await fixture.Students.Query(new() { Id = (uint)studentId }).LoadSingle())!;
        fixture.Dialogs.Answer<StudentPickerViewModel>(student);

        await fixture.ViewModel.SearchStudentCommand.Execute();

        Assert.Equal([("student", studentId)], fixture.Navigator.Navigations);
    }

    [Fact]
    public async Task SearchStudent_DoesNothing_WhenThePickerIsCancelled()
    {
        var fixture = await CreateAsync(f => AddStudentAsync(f.Students, "Annie"));

        await fixture.ViewModel.SearchStudentCommand.Execute();

        Assert.Empty(fixture.Navigator.Navigations);
    }

    /// <summary>The class-row template is shared by both sections via Resources and reaches
    /// MarkRollCommand through $parent[ItemsControl] - a broken binding there fails silently at
    /// runtime, so check the rendered buttons are actually wired.</summary>
    [AvaloniaFact]
    public async Task DashboardView_WiresMarkRollInBothClassSections()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var fixture = await CreateAsync(async f =>
        {
            var scheduleId = await f.Schedules.AddAsync(new ClassSchedule(0, DayOfWeek.Monday, new TimeOnly(9, 0), false));
            var schedule = (await f.Schedules.Query(new() { Id = (uint)scheduleId }).LoadSingle())!;
            await f.Rolls.AddAsync(new ClassRoll(0, today, schedule, []));
            await f.Rolls.AddAsync(new ClassRoll(0, today.AddDays(-7), schedule, []));
        });

        var view = new Views.Dashboard.DashboardView { DataContext = fixture.ViewModel };
        new Window { Content = view, Width = 1200, Height = 800 }.Show();
        Dispatcher.UIThread.RunJobs();

        // Today's tile is "Mark roll", the previous one "Edit roll" - both open the roll window.
        var rollTiles = view.GetVisualDescendants().OfType<Button>()
            .Where(b => AutomationProperties.GetName(b) is { } name &&
                        (name.StartsWith("Mark roll") || name.StartsWith("Edit roll")))
            .ToList();
        Assert.Equal(2, rollTiles.Count);
        Assert.All(rollTiles, b => Assert.Same(fixture.ViewModel.MarkRollCommand, b.Command));
        Assert.Contains(rollTiles, b => AutomationProperties.GetName(b)!.StartsWith("Mark roll"));
        Assert.Contains(rollTiles, b => AutomationProperties.GetName(b)!.StartsWith("Edit roll"));
    }
}
