using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Reactive.Testing;
using ReactiveUI;
using YogaClassManager.Avalonia.Controls;
using YogaClassManager.Avalonia.ViewModels.Shared;
using YogaClassManager.Avalonia.Views.Shared;
using YogaClassManager.Core.Dummy;
using YogaClassManager.Core.Models.People;

namespace YogaClassManager.Avalonia.Tests;

/// <summary>The shared person search in its three hosts: Mark Roll (rows carry "Add to roll"), and the
/// student and emergency-contact pickers (no row button - Enter, double-click or Select chooses).</summary>
public class PersonSearchTests
{
    private static void WithTestScheduler(Action<TestScheduler> body)
    {
        var testScheduler = new TestScheduler();
        var originalScheduler = RxApp.MainThreadScheduler;
        RxApp.MainThreadScheduler = testScheduler;
        try
        {
            body(testScheduler);
        }
        finally
        {
            RxApp.MainThreadScheduler = originalScheduler;
        }
    }

    private sealed record Store(InMemoryIdentityRepository Identities, InMemoryStudentRepository Students);

    private static Store CreateStore()
    {
        var store = new InMemoryDataStore();
        var identities = new InMemoryIdentityRepository(store);
        var students = new InMemoryStudentRepository(store, identities, new InMemoryPassRepository(store),
            new InMemoryEmergencyContactRepository(store));
        return new Store(identities, students);
    }

    [Fact]
    public async Task ContactPicker_FindsAnyPerson_LeavesOutTheExcluded_AndEnterPicksTheOnlyMatch()
    {
        var store = CreateStore();
        var studentId = await store.Students.AddAsync(new Student(0, "Kenneth", "Student", "0400000000", null, true));
        var parentId = await store.Identities.AddAsync(new Identity(0, "Kenneth", "Parent", "0411111111", null, true));

        WithTestScheduler(scheduler =>
        {

            var picker = new ContactPickerViewModel(store.Identities, "Select an emergency contact",
                excludeIds: [studentId]);
            Identity? closedWith = null;
            picker.RequestClose += person => closedWith = person;

            picker.Search.SearchQuery = "Kenneth";
            scheduler.AdvanceByMs(300);

            // A plain identity (not a student) is found; the excluded student isn't.
            Assert.Equal([parentId], picker.Search.SearchResults.Select(p => p.Id));

            picker.Search.ChooseSelectedCommand.Execute().Subscribe();
            Assert.Equal(parentId, closedWith?.Id);
            Assert.Same(picker.Search.ChooseSelectedCommand, picker.DefaultCommand);
        });
    }

    private static List<Button> RowButtons(Control root) =>
        root.GetVisualDescendants().OfType<PersonSearchBox>()
            .SelectMany(box => box.GetVisualDescendants().OfType<Button>()).ToList();

    [AvaloniaFact]
    public async Task Pickers_HideTheRowButton_MarkRollKeepsIt()
    {
        var store = CreateStore();
        await store.Students.AddAsync(new Student(0, "Ada", "Tester", "0400000000", null, true));

        WithTestScheduler(scheduler =>
        {

            var studentPicker = new StudentPickerViewModel(store.Students);
            var studentView = new StudentPickerView { DataContext = studentPicker };
            var contactPicker = new ContactPickerViewModel(store.Identities, "Select an emergency contact");
            var contactView = new ContactPickerView { DataContext = contactPicker };
            new Window { Content = new StackPanel { Children = { studentView, contactView } }, Width = 1000, Height = 1200 }.Show();

            studentPicker.Search.SearchQuery = "Ada";
            contactPicker.Search.SearchQuery = "Ada";
            scheduler.AdvanceByMs(300);
            Dispatcher.UIThread.RunJobs();

            Assert.Single(studentPicker.Search.SearchResults);
            Assert.All(RowButtons(studentView), b => Assert.False(b.IsEffectivelyVisible));
            Assert.All(RowButtons(contactView), b => Assert.False(b.IsEffectivelyVisible));

            // The real Mark Roll window still shows "Add to roll" on each result row.
            var markRoll = new MarkRollViewModel(
                new YogaClassManager.Core.Models.Classes.ClassRoll(0, new DateOnly(2026, 7, 20),
                    new YogaClassManager.Core.Models.Classes.ClassSchedule(1, DayOfWeek.Monday, new TimeOnly(9, 0), false), []),
                new InMemoryClassRollRepository(new InMemoryDataStore()), store.Students,
                new InMemoryPassRepository(new InMemoryDataStore()), new InMemoryTermRepository(new InMemoryDataStore()),
                new FakeToastService());
            var markRollWindow = new MarkRollWindow { DataContext = markRoll, Width = 900, Height = 700 };
            markRollWindow.Show();
            markRoll.Search.SearchQuery = "Ada";
            scheduler.AdvanceByMs(300);
            Dispatcher.UIThread.RunJobs();

            Assert.Single(markRoll.Search.SearchResults);
            Assert.Contains(RowButtons(markRollWindow), b => b.IsEffectivelyVisible &&
                                                           ToolTip.GetTip(b) as string == "Add to roll");
            markRollWindow.Close();
        });
    }

    [AvaloniaFact]
    public async Task DoubleClickingAResult_ChoosesIt()
    {
        var store = CreateStore();
        await store.Students.AddAsync(new Student(0, "Ada", "Tester", "0400000000", null, true));

        WithTestScheduler(scheduler =>
        {

            var picker = new StudentPickerViewModel(store.Students);
            Student? closedWith = null;
            picker.RequestClose += student => closedWith = student;
            var window = new Window { Content = new StudentPickerView { DataContext = picker }, Width = 600, Height = 600 };
            window.Show();

            picker.Search.SearchQuery = "Ada";
            scheduler.AdvanceByMs(300);
            Dispatcher.UIThread.RunJobs();

            var row = window.GetVisualDescendants().OfType<ListBoxItem>().Single();
            var centre = row.TranslatePoint(new Point(row.Bounds.Width / 2, row.Bounds.Height / 2), window)!.Value;
            for (var click = 0; click < 2; click++)
            {
                window.MouseDown(centre, MouseButton.Left, RawInputModifiers.None);
                window.MouseUp(centre, MouseButton.Left, RawInputModifiers.None);
            }
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("Ada", closedWith?.FirstName);
        });
    }
}
