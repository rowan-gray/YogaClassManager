using Microsoft.Reactive.Testing;
using ReactiveUI;
using YogaClassManager.Avalonia.ViewModels.Shared;
using YogaClassManager.Core.Dummy;
using YogaClassManager.Core.Models.Classes;
using YogaClassManager.Core.Models.Passes;
using YogaClassManager.Core.Models.People;

namespace YogaClassManager.Avalonia.Tests;

/// <summary>
///     The keyboard path through Mark Roll (UI_REVIEW.md finding U7: "the roll search is mouse-only",
///     and it is the most-repeated action in the app). Runs the debounced search against a
///     TestScheduler so the 250ms throttle resolves deterministically.
/// </summary>
public class MarkRollKeyboardTests
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

    private static MarkRollViewModel CreateWithStudents(params string[] firstNames)
    {
        var store = new InMemoryDataStore();
        var identityRepository = new InMemoryIdentityRepository(store);
        var passRepository = new InMemoryPassRepository(store);
        var studentRepository = new InMemoryStudentRepository(store, identityRepository, passRepository,
            new InMemoryEmergencyContactRepository(store));

        foreach (var firstName in firstNames)
        {
            var studentId = studentRepository
                .AddAsync(new Student(0, firstName, "Tester", "0400000000", null, true))
                .GetAwaiter().GetResult();

            // Each student needs exactly one usable pass, so AddStudentAsync auto-selects it. With none,
            // it opens the "add a pass" dialog and waits on a TaskCompletionSource no test can answer.
            passRepository.AddAsync(new CasualPass(0, studentId, 10, [], 0)).GetAwaiter().GetResult();
        }

        var roll = new ClassRoll(0, new DateOnly(2026, 7, 20),
            new ClassSchedule(1, DayOfWeek.Monday, new TimeOnly(9, 0), false), []);

        return new MarkRollViewModel(roll, new InMemoryClassRollRepository(store), studentRepository,
            passRepository, new InMemoryTermRepository(store), new FakeToastService());
    }

    private static void Search(MarkRollViewModel viewModel, TestScheduler scheduler, string query)
    {
        viewModel.SearchQuery = query;
        scheduler.AdvanceByMs(300);
    }

    [Fact]
    public void BeforeAnythingIsTyped_TheResultsListExplainsWhatToDo()
    {
        var viewModel = CreateWithStudents("Ada");

        Assert.False(viewModel.HasSearchResults);
        Assert.Equal("Type a name to find a student.", viewModel.SearchEmptyText);
    }

    [Fact]
    public void ASearchThatMatchesNothing_SaysSo_RatherThanGoingBlank()
    {
        WithTestScheduler(scheduler =>
        {
            var viewModel = CreateWithStudents("Ada");

            Search(viewModel, scheduler, "zzz");

            Assert.False(viewModel.HasSearchResults);
            Assert.Equal("No students match this search.", viewModel.SearchEmptyText);
        });
    }

    [Fact]
    public void EnterInTheSearchBox_AddsTheTopMatch_WithNothingSelected()
    {
        WithTestScheduler(scheduler =>
        {
            var viewModel = CreateWithStudents("Ada");

            Search(viewModel, scheduler, "Ada");

            Assert.True(viewModel.HasSearchResults);
            Assert.Null(viewModel.SelectedSearchResult);

            viewModel.AddSelectedStudentCommand.Execute().Subscribe();

            var entry = Assert.Single(viewModel.Entries);
            Assert.Equal("Ada", entry.Student.FirstName);
        });
    }

    [Fact]
    public void EnterOnAHighlightedRow_AddsThatRow_NotTheFirstOne()
    {
        WithTestScheduler(scheduler =>
        {
            var viewModel = CreateWithStudents("Ada", "Grace");

            Search(viewModel, scheduler, "Tester");
            Assert.Equal(2, viewModel.SearchResults.Count);

            viewModel.SelectedSearchResult = viewModel.SearchResults.Single(s => s.FirstName == "Grace");
            viewModel.AddSelectedStudentCommand.Execute().Subscribe();

            var entry = Assert.Single(viewModel.Entries);
            Assert.Equal("Grace", entry.Student.FirstName);
        });
    }

    [Fact]
    public void AddingCannotRun_WhileThereIsNothingToAdd()
    {
        var viewModel = CreateWithStudents("Ada");

        var canExecute = true;
        using (viewModel.AddSelectedStudentCommand.CanExecute.Subscribe(value => canExecute = value))
        {
            Assert.False(canExecute);
        }
    }
}
