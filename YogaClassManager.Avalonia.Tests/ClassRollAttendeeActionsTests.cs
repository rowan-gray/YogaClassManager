using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ReactiveUI;
using YogaClassManager.Avalonia.Controls;
using YogaClassManager.Avalonia.ViewModels.ClassRolls;
using YogaClassManager.Avalonia.ViewModels.Shared;
using YogaClassManager.Core.Dummy;
using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models.Classes;
using YogaClassManager.Core.Models.Passes;
using YogaClassManager.Core.Models.People;

namespace YogaClassManager.Avalonia.Tests;

/// <summary>The Class Rolls page's quick attendee actions (Add / Change pass / Remove, saved at once) and
/// the "..." button only appearing when a list has row actions.</summary>
public class ClassRollAttendeeActionsTests
{
    private sealed record Fixture(ClassRollsViewModel ViewModel, InMemoryClassRollRepository Rolls,
        InMemoryStudentRepository Students, InMemoryPassRepository Passes, ScriptedDialogService Dialogs,
        int RollId, Student Ada, int AdaPassId);

    private static async Task<Fixture> CreateAsync()
    {
        var store = new InMemoryDataStore();
        var identities = new InMemoryIdentityRepository(store);
        var passes = new InMemoryPassRepository(store);
        var students = new InMemoryStudentRepository(store, identities, passes, new InMemoryEmergencyContactRepository(store));
        var schedules = new InMemoryClassScheduleRepository(store);
        var rolls = new InMemoryClassRollRepository(store);

        var scheduleId = await schedules.AddAsync(new ClassSchedule(0, DayOfWeek.Monday, new TimeOnly(9, 0), false));
        var schedule = (await schedules.Query(new() { Id = (uint)scheduleId }).LoadSingle())!;

        var adaId = await students.AddAsync(new Student(0, "Ada", "Lovelace", "0400000000", null, true));
        var ada = (await students.Query(new() { Id = (uint)adaId }).LoadSingle())!;
        var adaPassId = await passes.AddAsync(new CasualPass(0, adaId, 10, [], 0));
        var adaPass = (await passes.Query(new PassFilter { Id = (uint)adaPassId, IncludeExpired = true, IncludeDepleted = true }).LoadSingle())!;
        var rollId = await rolls.AddAsync(new ClassRoll(0, new DateOnly(2026, 7, 20), schedule, [new ClassRollEntry(ada, adaPass)]));

        var dialogs = new ScriptedDialogService();
        var viewModel = new ClassRollsViewModel(new AppScreen(), rolls, students, passes, new InMemoryTermRepository(store),
            dialogs, new FakeToastService(), new FakeRollWindowService(), new FakeNavigator());
        await viewModel.RefreshCommand.Execute();
        viewModel.Selection = viewModel.Items.Single(r => r.Id == rollId);
        return new Fixture(viewModel, rolls, students, passes, dialogs, rollId, ada, adaPassId);
    }

    private static async Task<List<ClassRollEntry>> EntriesInRepository(Fixture f) =>
        (await f.Rolls.Query(new ClassRollFilter { Id = (uint)f.RollId }).LoadSingle())!.StudentEntries;

    [Fact]
    public async Task Add_PutsTheStudentOnTheRoll_WithTheirPass()
    {
        var f = await CreateAsync();
        var graceId = await f.Students.AddAsync(new Student(0, "Grace", "Hopper", "0411111111", null, true));
        var grace = (await f.Students.Query(new() { Id = (uint)graceId }).LoadSingle())!;
        var gracePassId = await f.Passes.AddAsync(new CasualPass(0, graceId, 5, [], 0));
        f.Dialogs.Answer<StudentPickerViewModel>(grace);

        await f.ViewModel.AddAttendeeCommand.Execute();

        var entries = await EntriesInRepository(f);
        Assert.Equal(gracePassId, entries.Single(e => e.Student.Id == graceId).Pass?.Id);
        Assert.Equal(2, f.ViewModel.Attendees.Count); // the detail pane refreshed in place
        Assert.Equal(f.RollId, f.ViewModel.Selection?.Id); // and kept the selection
    }

    [Fact]
    public async Task Add_WithNoPass_AndAddPassCancelled_AddsNobody()
    {
        var f = await CreateAsync();
        var graceId = await f.Students.AddAsync(new Student(0, "Grace", "Hopper", "0411111111", null, true));
        f.Dialogs.Answer<StudentPickerViewModel>((await f.Students.Query(new() { Id = (uint)graceId }).LoadSingle())!);
        // PassEditViewModel unanswered = "Add pass" cancelled.

        await f.ViewModel.AddAttendeeCommand.Execute();

        Assert.Single(await EntriesInRepository(f));
        Assert.Contains(f.Dialogs.Shown, d => d is PassEditViewModel);
    }

    [Fact]
    public async Task ChangePass_SavesTheNewlyChosenPass()
    {
        var f = await CreateAsync();
        var newPassId = await f.Passes.AddAsync(new CasualPass(0, f.Ada.Id, 20, [], 0));
        var newPass = (await f.Passes.Query(new PassFilter { Id = (uint)newPassId, IncludeExpired = true, IncludeDepleted = true }).LoadSingle())!;
        f.Dialogs.Answer<PassPickerViewModel>(newPass);

        await f.ViewModel.ChangeAttendeePassCommand.Execute(f.ViewModel.Attendees.Single());

        Assert.Equal(newPassId, (await EntriesInRepository(f)).Single().Pass?.Id);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Remove_AsksFirst_AndOnlyRemovesWhenConfirmed(bool confirmed)
    {
        var f = await CreateAsync();
        f.Dialogs.Answer<ConfirmViewModel>(confirmed);

        await f.ViewModel.RemoveAttendeeCommand.Execute(f.ViewModel.Attendees.Single());

        Assert.Equal(confirmed ? 0 : 1, (await EntriesInRepository(f)).Count);
        Assert.Equal(confirmed ? 0 : 1, f.ViewModel.Attendees.Count);
    }

    private static Button OverflowButton(LinkedRecordsPanel panel)
    {
        new Window { Content = panel, Width = 600, Height = 400 }.Show();
        Dispatcher.UIThread.RunJobs();
        return panel.GetVisualDescendants().OfType<Button>().Single(b => b.Classes.Contains("Overflow"));
    }

    [AvaloniaFact]
    public void TheMoreActionsButton_OnlyShowsWhenTheListHasRowActions()
    {
        var noActions = OverflowButton(new LinkedRecordsPanel
        {
            Header = "Attendees", ItemsSource = new ObservableCollection<string> { "Ada" }
        });
        Assert.False(noActions.IsVisible);

        ICommand remove = ReactiveCommand.Create<object>(_ => { });
        var withRemove = OverflowButton(new LinkedRecordsPanel
        {
            Header = "Attendees", ItemsSource = new ObservableCollection<string> { "Ada" }, RemoveCommand = remove
        });
        Assert.True(withRemove.IsVisible);
    }
}
