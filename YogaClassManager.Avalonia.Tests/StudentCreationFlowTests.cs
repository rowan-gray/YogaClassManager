using System.Collections.ObjectModel;
using YogaClassManager.Avalonia.Services;
using YogaClassManager.Avalonia.ViewModels.Shared;
using YogaClassManager.Core.Dummy;
using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models.Passes;
using YogaClassManager.Core.Models.People;

namespace YogaClassManager.Avalonia.Tests;

/// <summary>The shared "add a student" flow (Students page Add, Dashboard quick action), and the pass
/// dialog it often leads to.</summary>
public class StudentCreationFlowTests
{
    private sealed record Fixture(InMemoryStudentRepository Students, InMemoryIdentityRepository Identities,
        ScriptedDialogService Dialogs, StudentCreationFlow Flow);

    private static Fixture Create()
    {
        var store = new InMemoryDataStore();
        var identities = new InMemoryIdentityRepository(store);
        var students = new InMemoryStudentRepository(store, identities, new InMemoryPassRepository(store),
            new InMemoryEmergencyContactRepository(store));
        var dialogs = new ScriptedDialogService();
        return new Fixture(students, identities, dialogs,
            new StudentCreationFlow(students, identities, dialogs, new FakeToastService()));
    }

    private static Student NewJudi() => new(0, "Judi", "Dench", "0400000000", null, true);

    private static async Task<int> StudentCount(Fixture fixture, string firstName) =>
        (await fixture.Students.Query(new StudentFilter { NameFilter = firstName }).LoadMultiple()).Count;

    [Fact]
    public async Task ASameNamedStudent_GetsAWarning_NotTheUseExistingPrompt()
    {
        var fixture = Create();
        await fixture.Students.AddAsync(NewJudi());
        fixture.Dialogs.Answer<StudentEditViewModel>(NewJudi());

        await fixture.Flow.RunAsync();

        Assert.Contains(fixture.Dialogs.Shown, dialog => dialog is ConfirmViewModel);
        Assert.DoesNotContain(fixture.Dialogs.Shown, dialog => dialog is PromoteConfirmViewModel);
    }

    [Fact]
    public async Task DecliningTheWarning_CreatesNothing()
    {
        var fixture = Create();
        await fixture.Students.AddAsync(NewJudi());
        fixture.Dialogs.Answer<StudentEditViewModel>(NewJudi()); // ConfirmViewModel unanswered = declined

        var result = await fixture.Flow.RunAsync();

        Assert.Null(result);
        Assert.Equal(1, await StudentCount(fixture, "Judi"));
    }

    [Fact]
    public async Task ConfirmingTheWarning_CreatesASeparateStudent()
    {
        var fixture = Create();
        var existingId = await fixture.Students.AddAsync(NewJudi());
        fixture.Dialogs.Answer<StudentEditViewModel>(NewJudi()).Answer<ConfirmViewModel>(true);

        var result = await fixture.Flow.RunAsync();

        Assert.NotNull(result);
        Assert.NotEqual(existingId, result);
        Assert.Equal(2, await StudentCount(fixture, "Judi"));
    }

    [Fact]
    public void PassDialogTitle_NamesWhoThePassIsFor()
    {
        var terms = new InMemoryTermRepository(new InMemoryDataStore());
        var dialogs = new ScriptedDialogService();

        Assert.Equal("Add pass for Judi Dench",
            new PassEditViewModel(1, "Judi Dench", dialogs, terms).Title);
        Assert.Equal("Edit pass for Judi Dench",
            new PassEditViewModel(1, "Judi Dench", dialogs, terms,
                new CasualPass(5, 1, 10, new ObservableCollection<PassAlteration>(), 0)).Title);
    }
}
