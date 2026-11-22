using System.Collections.ObjectModel;
using System.Reactive.Linq;
using YogaClassManager.Avalonia.ViewModels;
using YogaClassManager.Avalonia.ViewModels.Shared;
using YogaClassManager.Core.Dummy;
using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models.Classes;
using YogaClassManager.Core.Models.Passes;
using YogaClassManager.Core.Models.People;

namespace YogaClassManager.Avalonia.Tests;

/// <summary>
///     The edit dialogs' Save (Term, Pass, Identity, Student, Class, Alteration): what they close with,
///     and that an invalid form stays open with a message instead of closing. The validity rules
///     themselves are pinned in Core's ValidationRuleTests/PersonValidationTests; these check the
///     dialogs build the model from the form correctly and honour them.
/// </summary>
public class EditDialogSaveTests
{
    private static readonly DateOnly Start = new(2026, 2, 2);
    private static readonly DateOnly End = new(2026, 4, 10);
    private static readonly ClassSchedule Monday = new(1, DayOfWeek.Monday, new TimeOnly(9, 0), false);

    private static List<T?> RecordCloses<T>(DialogViewModelBase<T> dialog)
    {
        var closed = new List<T?>();
        dialog.RequestClose += closed.Add;
        return closed;
    }

    // ---- TermEditViewModel ----

    [Fact]
    public async Task TermEdit_NewTerm_ClosesWithTheFormValues_AndNoCatchupWhenItIsOff()
    {
        var dialog = new TermEditViewModel { Name = "Term 1", StartDate = Start, EndDate = End, HasCatchup = false };
        var closed = RecordCloses(dialog);

        await dialog.SaveCommand.Execute();

        var term = Assert.Single(closed)!;
        Assert.Equal(0, term.Id);
        Assert.Equal("Term 1", term.Name);
        Assert.Equal(Start, term.StartDate);
        Assert.Equal(End, term.EndDate);
        Assert.Null(term.CatchupStartDate);
        Assert.Null(term.CatchupEndDate);
    }

    [Fact]
    public async Task TermEdit_IncludesCatchupDates_WhenCatchupIsOn()
    {
        var dialog = new TermEditViewModel
        {
            Name = "Term 1", StartDate = Start, EndDate = End,
            HasCatchup = true, CatchupStartDate = Start.AddDays(-7), CatchupEndDate = End.AddDays(7)
        };
        var closed = RecordCloses(dialog);

        await dialog.SaveCommand.Execute();

        var term = Assert.Single(closed)!;
        Assert.Equal(Start.AddDays(-7), term.CatchupStartDate);
        Assert.Equal(End.AddDays(7), term.CatchupEndDate);
    }

    [Theory]
    [InlineData("   ", 0, 1, false)] // blank name
    [InlineData("Term 1", 0, 0, false)] // start == end
    [InlineData("Term 1", 0, 1, true)] // catchup that doesn't bracket the term (set below)
    public async Task TermEdit_StaysOpenWithAMessage_WhenInvalid(string name, int startOffset, int endOffset, bool badCatchup)
    {
        var dialog = new TermEditViewModel
        {
            Name = name, StartDate = Start.AddDays(startOffset), EndDate = Start.AddDays(endOffset),
            HasCatchup = badCatchup, CatchupStartDate = Start, CatchupEndDate = Start.AddDays(endOffset)
        };
        var closed = RecordCloses(dialog);

        await dialog.SaveCommand.Execute();

        Assert.Empty(closed);
        Assert.False(string.IsNullOrEmpty(dialog.ValidationError));
    }

    /// <summary>Turning catchup off must drop dates that would otherwise be invalid, not block the save.</summary>
    [Fact]
    public async Task TermEdit_IgnoresInvalidCatchupDates_WhenCatchupIsOff()
    {
        var dialog = new TermEditViewModel
        {
            Name = "Term 1", StartDate = Start, EndDate = End,
            HasCatchup = false, CatchupStartDate = End, CatchupEndDate = Start
        };
        var closed = RecordCloses(dialog);

        await dialog.SaveCommand.Execute();

        Assert.Null(Assert.Single(closed)!.CatchupStartDate);
    }

    [Fact]
    public async Task TermEdit_EditingATerm_KeepsItsIdAndLinkedClasses_AndCanRemoveCatchup()
    {
        var existing = new Term(42, "Term 1", Start, End, Start.AddDays(-7), End.AddDays(7),
            [new TermClassSchedule(Monday, 10, 3)]);
        var dialog = new TermEditViewModel(existing);
        var closed = RecordCloses(dialog);

        Assert.False(dialog.IsNew);
        Assert.True(dialog.HasCatchup);
        dialog.Name = "Term 1 (renamed)";
        dialog.HasCatchup = false;
        await dialog.SaveCommand.Execute();

        var term = Assert.Single(closed)!;
        Assert.Equal(42, term.Id);
        Assert.Equal("Term 1 (renamed)", term.Name);
        Assert.Null(term.CatchupEndDate);
        var termClass = Assert.Single(term.Classes);
        Assert.Equal(Monday, termClass.ClassSchedule);
        Assert.Equal(10, termClass.ClassCount);
    }

    // ---- PassEditViewModel ----

    private static (PassEditViewModel Dialog, List<Pass?> Closed, ScriptedDialogService Dialogs) CreatePassDialog(
        Pass? editing = null, params Term[] terms)
    {
        var store = new InMemoryDataStore();
        var termRepository = new InMemoryTermRepository(store);
        foreach (var term in terms)
            termRepository.AddAsync(term).GetAwaiter().GetResult();

        var dialogs = new ScriptedDialogService();
        var dialog = new PassEditViewModel(7, "Annie Tester", dialogs, termRepository, editing);
        return (dialog, RecordCloses(dialog), dialogs);
    }

    [Fact]
    public async Task PassEdit_NewCasualPass_ClosesWithACasualPassForTheStudent()
    {
        var (dialog, closed, _) = CreatePassDialog();
        dialog.Kind = PassKind.Casual;
        dialog.ClassCount = 10;

        await dialog.SaveCommand.Execute();

        var pass = Assert.IsType<CasualPass>(Assert.Single(closed));
        Assert.Equal(0, pass.Id);
        Assert.Equal(7, pass.StudentId);
        Assert.Equal(10, pass.ClassCount);
    }

    [Fact]
    public async Task PassEdit_NewDatedPass_ClosesWithTheChosenDates()
    {
        var (dialog, closed, _) = CreatePassDialog();
        dialog.Kind = PassKind.Dated;
        dialog.ClassCount = 8;
        dialog.StartDate = Start;
        dialog.EndDate = End;

        await dialog.SaveCommand.Execute();

        var pass = Assert.IsType<DatedPass>(Assert.Single(closed));
        Assert.Equal((8, Start, End), (pass.ClassCount, pass.StartDate, pass.EndDate));
    }

    [Fact]
    public async Task PassEdit_DatedPass_StaysOpen_WhenEndIsBeforeStart()
    {
        var (dialog, closed, _) = CreatePassDialog();
        dialog.Kind = PassKind.Dated;
        dialog.StartDate = End;
        dialog.EndDate = Start;

        await dialog.SaveCommand.Execute();

        Assert.Empty(closed);
        Assert.False(string.IsNullOrEmpty(dialog.ValidationError));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task PassEdit_StaysOpen_WithoutAPositiveClassCount(int classCount)
    {
        var (dialog, closed, _) = CreatePassDialog();
        dialog.ClassCount = classCount;

        await dialog.SaveCommand.Execute();

        Assert.Empty(closed);
        Assert.False(string.IsNullOrEmpty(dialog.ValidationError));
    }

    [Fact]
    public async Task PassEdit_TermPass_StaysOpen_WhenThereIsNoTermToChoose()
    {
        var (dialog, closed, _) = CreatePassDialog();
        dialog.Kind = PassKind.Term;

        await dialog.SaveCommand.Execute();

        Assert.Empty(closed);
        Assert.Equal("Select a term and a class.", dialog.ValidationError);
    }

    [Fact]
    public async Task PassEdit_TermPass_DefaultsToTheFirstTermsFirstClass_AndClosesWithIt()
    {
        var term = new Term(0, "Term 1", Start, End, null, null, [new TermClassSchedule(Monday, 10, 0)]);
        var (dialog, closed, _) = CreatePassDialog(null, term);
        dialog.Kind = PassKind.Term;

        Assert.Equal(term.Id, dialog.SelectedTerm?.Id);
        Assert.Equal(10, dialog.ClassesRemainingPreview);
        await dialog.SaveCommand.Execute();

        var pass = Assert.IsType<TermPass>(Assert.Single(closed));
        Assert.Equal(term.Id, pass.Term.Id);
        Assert.Equal(Monday, pass.TermClassSchedule.ClassSchedule);
        Assert.Equal(10, pass.NumberOfClasses);
    }

    [Fact]
    public async Task PassEdit_EditingAPass_KeepsItsIdUsageAndAlterations()
    {
        var existing = new CasualPass(15, 7, 10, new ObservableCollection<PassAlteration>
        {
            new(1, 15, 2, "Bonus")
        }, classesUsed: 4);
        var (dialog, closed, _) = CreatePassDialog(existing);

        Assert.False(dialog.IsNew);
        Assert.Equal(8, dialog.ClassesRemainingPreview);
        dialog.ClassCount = 12;
        await dialog.SaveCommand.Execute();

        var pass = Assert.IsType<CasualPass>(Assert.Single(closed));
        Assert.Equal(15, pass.Id);
        Assert.Equal(4, pass.ClassesUsed);
        Assert.Equal(12, pass.ClassCount);
        Assert.Equal(2, Assert.Single(pass.Alterations).Amount);
    }

    /// <summary>Lowering a used pass's class count below what's already been used must be refused.</summary>
    [Fact]
    public async Task PassEdit_StaysOpen_WhenTheClassCountDropsBelowClassesAlreadyUsed()
    {
        var existing = new CasualPass(15, 7, 10, new ObservableCollection<PassAlteration>(), classesUsed: 6);
        var (dialog, closed, _) = CreatePassDialog(existing);
        dialog.ClassCount = 5;

        Assert.Equal(-1, dialog.ClassesRemainingPreview);
        await dialog.SaveCommand.Execute();

        Assert.Empty(closed);
        Assert.False(string.IsNullOrEmpty(dialog.ValidationError));
    }

    [Fact]
    public async Task PassEdit_AddingAndRemovingAnAlteration_UpdatesThePreviewAndTheSavedPass()
    {
        var (dialog, closed, dialogs) = CreatePassDialog();
        dialog.ClassCount = 5;
        var bonus = new PassAlteration(0, 0, 3, "Make-up classes");
        dialogs.Answer<AlterationEditViewModel>(bonus);

        await dialog.AddAlterationCommand.Execute();
        Assert.Equal(8, dialog.ClassesRemainingPreview);

        await dialog.RemoveAlterationCommand.Execute(bonus);
        Assert.Equal(5, dialog.ClassesRemainingPreview);

        await dialog.AddAlterationCommand.Execute();
        await dialog.SaveCommand.Execute();
        Assert.Equal(8, Assert.Single(closed)!.NumberOfClasses);
    }

    [Fact]
    public async Task PassEdit_CancellingTheAlterationDialog_AddsNothing()
    {
        var (dialog, _, _) = CreatePassDialog();

        await dialog.AddAlterationCommand.Execute();

        Assert.Empty(dialog.Alterations);
    }

    // ---- IdentityEditViewModel ----

    [Fact]
    public async Task IdentityEdit_ClosesWithTheTidiedFormValues()
    {
        var dialog = new IdentityEditViewModel(new Identity(), isNew: true)
        {
            FirstName = " Ava ", LastName = "Lee", PhoneNumber = "0412 345 678", Email = ""
        };
        var closed = RecordCloses(dialog);

        await dialog.SaveCommand.Execute();

        var identity = Assert.Single(closed)!;
        Assert.Equal("Ava", identity.FirstName);
        Assert.Equal("Lee", identity.LastName);
        Assert.Equal("0412345678", identity.PhoneNumber);
        Assert.Null(identity.Email);
        Assert.Equal("Add identity", dialog.Title);
    }

    [Fact]
    public async Task IdentityEdit_Editing_KeepsTheIdAndArchivedState()
    {
        var dialog = new IdentityEditViewModel(new Identity(7, "Ava", "Lee", "0412345678", null, false), isNew: false)
        {
            FirstName = "Eva"
        };
        var closed = RecordCloses(dialog);

        await dialog.SaveCommand.Execute();

        var identity = Assert.Single(closed)!;
        Assert.Equal(7, identity.Id);
        Assert.False(identity.IsActive);
        Assert.Equal("Eva", identity.FirstName);
        Assert.Equal("Edit identity", dialog.Title);
    }

    [Fact]
    public async Task IdentityEdit_InvalidForm_StaysOpenWithAMessage_UntilFixed()
    {
        var dialog = new IdentityEditViewModel(new Identity(), isNew: true) { FirstName = "Ava", PhoneNumber = "12345" };
        var closed = RecordCloses(dialog);

        await dialog.SaveCommand.Execute();

        Assert.Empty(closed);
        Assert.NotNull(dialog.ValidationError);

        dialog.PhoneNumber = "0412345678";
        await dialog.SaveCommand.Execute();

        Assert.Single(closed);
    }

    // ---- StudentEditViewModel ----

    private static (StudentEditViewModel Dialog, ScriptedDialogService Dialogs) StudentEdit(Student student, bool isNew)
    {
        var dialogs = new ScriptedDialogService();
        var identities = new InMemoryIdentityRepository(new InMemoryDataStore());
        return (new StudentEditViewModel(student, isNew, identities, dialogs), dialogs);
    }

    [Fact]
    public async Task StudentEdit_ClosesWithTheTidiedFormValues()
    {
        var (dialog, _) = StudentEdit(new Student(), isNew: true);
        dialog.FirstName = "Ava";
        dialog.LastName = " Lee ";
        dialog.Email = "ava@example.com";
        var closed = RecordCloses(dialog);

        await dialog.SaveCommand.Execute();

        var student = Assert.Single(closed)!;
        Assert.Equal("Lee", student.LastName);
        Assert.Equal("ava@example.com", student.Email);
        Assert.False(dialog.IsConvertingExistingIdentity);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public async Task StudentEdit_WithoutALastName_StaysOpenWithAMessage(string? lastName)
    {
        var (dialog, _) = StudentEdit(new Student(), isNew: true);
        dialog.FirstName = "Ava";
        dialog.LastName = lastName;
        dialog.Email = "ava@example.com";
        var closed = RecordCloses(dialog);

        await dialog.SaveCommand.Execute();

        Assert.Empty(closed);
        Assert.NotNull(dialog.ValidationError);
    }

    [Fact]
    public async Task StudentEdit_UsingAnExistingIdentity_FillsTheFormAndTakesItsId()
    {
        var (dialog, dialogs) = StudentEdit(new Student(), isNew: true);
        dialogs.Answer<IdentityPickerViewModel>(new Identity(42, "Ava", "Lee", "0412345678", "ava@example.com", true));

        await dialog.UseExistingIdentityCommand.Execute();

        Assert.Equal(42, dialog.Id);
        Assert.True(dialog.IsConvertingExistingIdentity);
        Assert.Equal("Ava", dialog.FirstName);
        Assert.Equal("Lee", dialog.LastName);
        Assert.Equal("0412345678", dialog.PhoneNumber);
        Assert.Equal("ava@example.com", dialog.Email);

        var closed = RecordCloses(dialog);
        await dialog.SaveCommand.Execute();
        Assert.Equal(42, Assert.Single(closed)!.Id);
    }

    [Fact]
    public async Task StudentEdit_CancellingThePicker_LeavesTheFormAlone()
    {
        var (dialog, dialogs) = StudentEdit(new Student(), isNew: true);
        dialog.FirstName = "Typed";

        await dialog.UseExistingIdentityCommand.Execute();

        Assert.Single(dialogs.Shown);
        Assert.Equal("Typed", dialog.FirstName);
        Assert.False(dialog.IsConvertingExistingIdentity);
    }

    [Fact]
    public async Task StudentEdit_UseExistingIdentity_IsOnlyOfferedWhenAdding()
    {
        var (editing, _) = StudentEdit(new Student(5, "Ava", "Lee", "0412345678", null, true), isNew: false);
        var (adding, _) = StudentEdit(new Student(), isNew: true);

        Assert.False(await editing.UseExistingIdentityCommand.CanExecute.FirstAsync());
        Assert.True(await adding.UseExistingIdentityCommand.CanExecute.FirstAsync());
    }

    // ---- ClassScheduleEditViewModel ----

    private static async Task<InMemoryClassScheduleRepository> SchedulesWith(params ClassSchedule[] schedules)
    {
        var repository = new InMemoryClassScheduleRepository(new InMemoryDataStore());
        foreach (var schedule in schedules)
            await repository.AddAsync(schedule);
        return repository;
    }

    [Fact]
    public async Task ClassScheduleEdit_NewClass_DefaultsToMonday9am_AndClosesWithTheFormValues()
    {
        var dialog = new ClassScheduleEditViewModel(await SchedulesWith());
        Assert.Equal(DayOfWeek.Monday, dialog.Day);
        Assert.Equal(new TimeSpan(9, 0, 0), dialog.Time);

        dialog.Day = DayOfWeek.Thursday;
        dialog.Time = new TimeSpan(18, 30, 0);
        var closed = RecordCloses(dialog);

        await dialog.SaveCommand.Execute();

        var schedule = Assert.Single(closed)!;
        Assert.Equal(0, schedule.Id);
        Assert.Equal(DayOfWeek.Thursday, schedule.Day);
        Assert.Equal(new TimeOnly(18, 30), schedule.Time);
        Assert.False(schedule.IsArchived);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)] // an archived class still holds its slot
    public async Task ClassScheduleEdit_ATakenSlot_StaysOpenWithAMessage(bool existingIsArchived)
    {
        var repository = await SchedulesWith(new ClassSchedule(0, DayOfWeek.Monday, new TimeOnly(9, 0), existingIsArchived));
        var dialog = new ClassScheduleEditViewModel(repository);
        var closed = RecordCloses(dialog);

        await dialog.SaveCommand.Execute();

        Assert.Empty(closed);
        Assert.Equal("There's already a class on Monday at 09:00.", dialog.ValidationError);
    }

    [Fact]
    public async Task ClassScheduleEdit_Editing_CanKeepItsOwnSlot()
    {
        var repository = await SchedulesWith(new ClassSchedule(0, DayOfWeek.Monday, new TimeOnly(9, 0), false));
        var existing = Assert.Single(await repository.Query(new ClassScheduleFilter()).LoadMultiple());
        var dialog = new ClassScheduleEditViewModel(repository, existing) { IsArchived = true };
        var closed = RecordCloses(dialog);

        await dialog.SaveCommand.Execute();

        var schedule = Assert.Single(closed)!;
        Assert.Equal(existing.Id, schedule.Id);
        Assert.True(schedule.IsArchived);
        Assert.Equal("Edit class", dialog.Title);
    }

    // ---- AlterationEditViewModel ----

    [Fact]
    public async Task AlterationEdit_ClosesWithTheAdjustment()
    {
        var dialog = new AlterationEditViewModel(passId: 3) { Amount = -2, Reason = "Refund" };
        var closed = RecordCloses(dialog);

        await dialog.SaveCommand.Execute();

        var alteration = Assert.Single(closed)!;
        Assert.Equal(3, alteration.PassId);
        Assert.Equal(-2, alteration.Amount);
        Assert.Equal("Refund", alteration.Reason);
    }

    [Fact]
    public async Task AlterationEdit_AZeroAmount_StaysOpenWithAMessage()
    {
        var dialog = new AlterationEditViewModel(passId: 3) { Amount = 0 };
        var closed = RecordCloses(dialog);

        await dialog.SaveCommand.Execute();

        Assert.Empty(closed);
        Assert.Equal("Amount must not be zero.", dialog.ValidationError);
    }
}
