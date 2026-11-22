using System.Reactive.Linq;
using Microsoft.Reactive.Testing;
using ReactiveUI;
using YogaClassManager.Avalonia.ViewModels;
using YogaClassManager.Avalonia.ViewModels.Shared;
using YogaClassManager.Core.Dummy;
using YogaClassManager.Core.Models;
using YogaClassManager.Core.Models.Classes;
using YogaClassManager.Core.Models.People;

namespace YogaClassManager.Avalonia.Tests;

/// <summary>
///     The pick-something dialogs - the identity picker (emergency contacts, merging, "use existing
///     identity"), the term's class-link picker - and the merge preview: what they offer, and what they
///     close with. The identity picker's 200ms search throttle runs against a TestScheduler.
/// </summary>
public class PickerDialogTests
{
    private static List<T?> RecordCloses<T>(DialogViewModelBase<T> dialog)
    {
        var closed = new List<T?>();
        dialog.RequestClose += closed.Add;
        return closed;
    }

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

    // ---- IdentityPickerViewModel ----

    private sealed record People(InMemoryIdentityRepository Identities, int Zoe, int Amy, int AmyB, int Archived, int Student);

    private static People CreatePeople()
    {
        var store = new InMemoryDataStore();
        var identities = new InMemoryIdentityRepository(store);
        var students = new InMemoryStudentRepository(store, identities, new InMemoryPassRepository(store),
            new InMemoryEmergencyContactRepository(store));

        int Add(Identity identity) => identities.AddAsync(identity).GetAwaiter().GetResult();
        return new People(identities,
            Zoe: Add(new Identity(0, "Zoe", "Ng", "0412345678", null, true)),
            Amy: Add(new Identity(0, "Amy", "Wu", "0412345679", null, true)),
            AmyB: Add(new Identity(0, "Amy", "Brown", "0412345670", null, true)),
            Archived: Add(new Identity(0, "Old", "Contact", "0412345671", null, false)),
            Student: students.AddAsync(new Student(0, "Sam", "Student", "0412345672", null, true)).GetAwaiter().GetResult());
    }

    private static IEnumerable<int> Ids(IdentityPickerViewModel picker) => picker.Results.Select(p => p.Id);

    [Fact]
    public void IdentityPicker_OpensWithEveryActivePerson_ByFirstThenLastName()
    {
        var people = CreatePeople();

        var picker = new IdentityPickerViewModel(people.Identities, "Pick someone");

        Assert.Equal([people.AmyB, people.Amy, people.Student, people.Zoe], Ids(picker));
        Assert.False(picker.HasNoResults);
        Assert.Equal("Pick someone", picker.Title);
    }

    [Fact]
    public void IdentityPicker_LeavesOutExcludedPeopleAndStudents()
    {
        var people = CreatePeople();

        var picker = new IdentityPickerViewModel(people.Identities, excludeIds: [people.Zoe], excludeStudents: true);

        Assert.Equal([people.AmyB, people.Amy], Ids(picker));
    }

    [Fact]
    public void IdentityPicker_SearchFiltersByFullName_AndShowArchivedAddsArchivedPeople()
    {
        WithTestScheduler(scheduler =>
        {
            var people = CreatePeople();
            var picker = new IdentityPickerViewModel(people.Identities);

            picker.SearchQuery = " amy w ";
            scheduler.AdvanceByMs(300);
            Assert.Equal([people.Amy], Ids(picker));

            picker.SearchQuery = "contact";
            scheduler.AdvanceByMs(300);
            Assert.Empty(picker.Results);
            Assert.True(picker.HasNoResults);

            picker.ShowArchived = true;
            scheduler.AdvanceByMs(300);
            Assert.Equal([people.Archived], Ids(picker));
            Assert.False(picker.HasNoResults);
        });
    }

    [Fact]
    public async Task IdentityPicker_SelectNeedsASelection_AndClosesWithIt()
    {
        var people = CreatePeople();
        var picker = new IdentityPickerViewModel(people.Identities);
        var closed = RecordCloses(picker);

        Assert.False(await picker.SelectCommand.CanExecute.FirstAsync());

        picker.SelectedItem = picker.Results.First(p => p.Id == people.Zoe);
        Assert.True(await picker.SelectCommand.CanExecute.FirstAsync());
        await picker.SelectCommand.Execute();

        Assert.Equal(people.Zoe, Assert.Single(closed)!.Id);
    }

    // ---- ClassLinkViewModel ----

    [Fact]
    public async Task ClassLink_OffersTheClassesNotAlreadyLinked_AndClosesWithTheChoiceAndCount()
    {
        var schedules = new InMemoryClassScheduleRepository(new InMemoryDataStore());
        var linked = await schedules.AddAsync(new ClassSchedule(0, DayOfWeek.Monday, new TimeOnly(9, 0), false));
        var available = await schedules.AddAsync(new ClassSchedule(0, DayOfWeek.Tuesday, new TimeOnly(9, 0), false));

        var dialog = new ClassLinkViewModel(schedules, "Term 1", [linked]);
        var closed = RecordCloses(dialog);

        Assert.Equal("Term 1", dialog.TermName);
        Assert.Equal(10, dialog.ClassCount);
        Assert.Equal(available, Assert.Single(dialog.AvailableSchedules).Id);
        Assert.False(await dialog.SaveCommand.CanExecute.FirstAsync());

        dialog.SelectedSchedule = dialog.AvailableSchedules[0];
        dialog.ClassCount = 8;
        await dialog.SaveCommand.Execute();

        var selection = Assert.Single(closed)!;
        Assert.Equal(available, selection.Schedule.Id);
        Assert.Equal(8, selection.ClassCount);
    }

    // ---- MergeConfirmViewModel ----

    [Fact]
    public async Task MergeConfirm_SummarisesWhatMoves_AndConfirmClosesWithTrue()
    {
        var survivor = new Identity(1, "Ava", "Lee", "0412345678", null, true);
        var duplicate = new Identity(2, "Ava", "L", "0412345678", null, true);
        var dialog = new MergeConfirmViewModel(survivor, duplicate, new IdentityLinkageSummary(true, 2, 3, 4));
        var closed = RecordCloses(dialog);

        Assert.Contains("Ava L will be merged into Ava Lee.", dialog.Summary);
        Assert.Contains("a Student: yes", dialog.Summary);
        Assert.Contains("linked as an emergency contact: 2 time(s)", dialog.Summary);
        Assert.Contains("holding passes: 3", dialog.Summary);
        Assert.Contains("with attendance records: 4", dialog.Summary);
        Assert.Same(dialog.ConfirmCommand, dialog.DefaultCommand);

        await dialog.ConfirmCommand.Execute();

        Assert.True(Assert.Single(closed));
    }

    [Fact]
    public async Task MergeConfirm_CancelClosesWithFalse()
    {
        var identity = new Identity(1, "Ava", "Lee", "0412345678", null, true);
        var dialog = new MergeConfirmViewModel(identity, identity, new IdentityLinkageSummary(false, 0, 0, 0));
        var closed = RecordCloses(dialog);

        Assert.Contains("a Student: no", dialog.Summary);

        await dialog.CancelCommand.Execute();

        Assert.False(Assert.Single(closed));
    }
}
