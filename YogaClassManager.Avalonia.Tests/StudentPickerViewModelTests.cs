using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Microsoft.Reactive.Testing;
using ReactiveUI;
using YogaClassManager.Avalonia.Controls;
using YogaClassManager.Avalonia.Services;
using YogaClassManager.Avalonia.ViewModels.Shared;
using YogaClassManager.Core.Dummy;
using YogaClassManager.Core.Models.People;

namespace YogaClassManager.Avalonia.Tests;

/// <summary>
///     The student picker (Dashboard quick actions) is the Mark Roll search with "choose" meaning
///     "close with this student" - same keyboard contract as MarkRollKeyboardTests, against a
///     TestScheduler so the 250ms search throttle resolves deterministically.
/// </summary>
public class StudentPickerViewModelTests
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

    private static (StudentPickerViewModel Picker, List<Student?> Closed) Create(params string[] firstNames)
    {
        var store = new InMemoryDataStore();
        var identityRepository = new InMemoryIdentityRepository(store);
        var passRepository = new InMemoryPassRepository(store);
        var studentRepository = new InMemoryStudentRepository(store, identityRepository, passRepository,
            new InMemoryEmergencyContactRepository(store));

        foreach (var firstName in firstNames)
            studentRepository.AddAsync(new Student(0, firstName, "Tester", "0400000000", null, true))
                .GetAwaiter().GetResult();

        var picker = new StudentPickerViewModel(studentRepository);
        var closed = new List<Student?>();
        picker.RequestClose += closed.Add;
        return (picker, closed);
    }

    [Fact]
    public void Enter_ChoosesTheOnlyMatch_AndClosesWithIt()
    {
        WithTestScheduler(scheduler =>
        {
            var (picker, closed) = Create("Ada", "Grace");

            picker.Search.SearchQuery = "Ada";
            scheduler.AdvanceByMs(300);
            picker.Search.ChooseSelectedCommand.Execute().Subscribe();

            Assert.Equal("Ada", Assert.Single(closed)!.FirstName);
        });
    }

    [Fact]
    public void TheDialogDefaultCommand_IsTheSearchsEnterBehaviour()
    {
        var (picker, _) = Create("Ada");

        // DialogHost's Enter and the Select button both run DefaultCommand - it must choose exactly
        // what Enter in the search box would.
        Assert.Same(picker.Search.ChooseSelectedCommand, picker.DefaultCommand);
    }

    [Fact]
    public void ARowsButton_ChoosesThatRow()
    {
        WithTestScheduler(scheduler =>
        {
            var (picker, closed) = Create("Ada", "Grace");

            picker.Search.SearchQuery = "Tester";
            scheduler.AdvanceByMs(300);
            var grace = picker.Search.SearchResults.Single(s => s.FirstName == "Grace");
            picker.Search.ChooseCommand.Execute(grace).Subscribe();

            Assert.Same(grace, Assert.Single(closed));
        });
    }

    /// <summary>Inside a dialog, Enter reaches both the search box's own KeyBinding and DialogHost's
    /// DefaultCommand handling. They're the same command and whichever runs first marks the key
    /// handled, so a real Enter must choose the only match exactly once.</summary>
    [AvaloniaFact]
    public void RealEnterInTheOpenPicker_ClosesOnceWithTheOnlyMatch()
    {
        WithTestScheduler(scheduler =>
        {
            var (picker, closed) = Create("Ada", "Grace");
            var dialogs = new DialogService();
            var window = new Window { Content = new DialogHost { DialogService = dialogs }, Width = 800, Height = 600 };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var result = dialogs.ShowDialogAsync(picker);
            Dispatcher.UIThread.RunJobs();

            picker.Search.SearchQuery = "Ada";
            scheduler.AdvanceByMs(300);
            Dispatcher.UIThread.RunJobs();

            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();

            Assert.True(result.IsCompleted);
            Assert.Equal("Ada", result.Result!.FirstName);
            Assert.Single(closed);
        });
    }
}
