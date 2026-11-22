using Avalonia;
using YogaClassManager.Avalonia.Views.Shared;
using Avalonia.VisualTree;
using Avalonia.Threading;
using Avalonia.Headless.XUnit;
using Avalonia.Controls;
using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.Reactive.Testing;
using ReactiveUI;
using YogaClassManager.Avalonia.Converters;
using YogaClassManager.Avalonia.ViewModels.Shared;
using YogaClassManager.Core.Dummy;
using YogaClassManager.Core.Models.Classes;
using YogaClassManager.Core.Models.Passes;
using YogaClassManager.Core.Models.People;

namespace YogaClassManager.Avalonia.Tests;

/// <summary>What Mark Roll shows and does for each attendee: health concerns under the name, the
/// pass's dates, and backing out cleanly when "Add pass" is cancelled.</summary>
public class MarkRollAttendeeTests
{
    private static readonly ObservableCollection<PassAlteration> NoAlterations = new();
    private static readonly ClassSchedule Monday9am = new(1, DayOfWeek.Monday, new TimeOnly(9, 0), false);

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

    private sealed record Fixture(InMemoryDataStore Store, InMemoryStudentRepository Students,
        InMemoryPassRepository Passes);

    private static Fixture CreateStore()
    {
        var store = new InMemoryDataStore();
        var identities = new InMemoryIdentityRepository(store);
        var passes = new InMemoryPassRepository(store);
        var students = new InMemoryStudentRepository(store, identities, passes,
            new InMemoryEmergencyContactRepository(store));
        return new Fixture(store, students, passes);
    }

    private static Student AddStudent(Fixture fixture, string firstName, bool withPass, params string[] healthConcerns)
    {
        var id = fixture.Students.AddAsync(new Student(0, firstName, "Tester", "0400000000", null, true))
            .GetAwaiter().GetResult();
        if (withPass)
            fixture.Passes.AddAsync(new CasualPass(0, id, 10, [], 0)).GetAwaiter().GetResult();
        foreach (var concern in healthConcerns)
            fixture.Students.AddHealthConcernAsync(id, concern).GetAwaiter().GetResult();

        // A fresh copy without HealthConcerns populated, as a roll query or search hands it out.
        return new Student(id, firstName, "Tester", "0400000000", null, true);
    }

    private static MarkRollViewModel Open(Fixture fixture, params ClassRollEntry[] entries) =>
        new(new ClassRoll(0, new DateOnly(2026, 7, 20), Monday9am, entries.ToList()),
            new InMemoryClassRollRepository(fixture.Store), fixture.Students, fixture.Passes,
            new InMemoryTermRepository(fixture.Store), new FakeToastService());

    [Fact]
    public void ExistingAttendees_HaveTheirHealthConcernsLoaded()
    {
        var fixture = CreateStore();
        var ada = AddStudent(fixture, "Ada", withPass: false, "Asthma", "Bad knee");

        var viewModel = Open(fixture, new ClassRollEntry(ada, null));

        Assert.Equal(["Asthma", "Bad knee"], viewModel.Entries.Single().Student.HealthConcerns.OrderBy(c => c));
    }

    [Fact]
    public void AnAddedStudent_HasTheirHealthConcernsLoaded()
    {
        WithTestScheduler(scheduler =>
        {
            var fixture = CreateStore();
            AddStudent(fixture, "Ada", withPass: true, "Asthma");
            var viewModel = Open(fixture);

            viewModel.Search.SearchQuery = "Ada";
            scheduler.AdvanceByMs(300);
            viewModel.Search.ChooseSelectedCommand.Execute().Subscribe();

            Assert.Equal(["Asthma"], viewModel.Entries.Single().Student.HealthConcerns);
        });
    }

    [Fact]
    public void CancellingAddPass_DoesNotAddTheStudent_AndKeepsTheSearch()
    {
        WithTestScheduler(scheduler =>
        {
            var fixture = CreateStore();
            AddStudent(fixture, "Ada", withPass: false);
            var viewModel = Open(fixture);

            viewModel.Search.SearchQuery = "Ada";
            scheduler.AdvanceByMs(300);
            viewModel.Search.ChooseSelectedCommand.Execute().Subscribe();

            // No usable pass, so Mark Roll is waiting on its "Add pass" dialog - cancel it.
            var addPass = Assert.IsType<PassEditViewModel>(viewModel.Dialogs.ActiveDialog);
            addPass.CancelCommand.Execute().Subscribe();

            Assert.Empty(viewModel.Entries);
            Assert.False(viewModel.IsDirty);
            Assert.Equal("Ada", viewModel.Search.SearchQuery);
            Assert.Equal("Ada", Assert.Single(viewModel.Search.SearchResults).FirstName);
        });
    }

    [Fact]
    public void PassDateRange_ForADatedPass_IsItsStartAndEnd()
    {
        var pass = new DatedPass(1, 1, 10, NoAlterations, 0, new DateOnly(2026, 2, 1), new DateOnly(2026, 3, 31));

        Assert.Equal("01/02/2026 - 31/03/2026", Convert(pass));
    }

    [Fact]
    public void PassDateRange_ForATermPass_RunsToTheCatchUpEnd_WhenThereIsOne()
    {
        var withCatchUp = new Term(1, "Winter", new DateOnly(2026, 6, 1), new DateOnly(2026, 8, 31),
            new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 14), []);
        var withoutCatchUp = new Term(2, "Spring", new DateOnly(2026, 9, 1), new DateOnly(2026, 11, 30), null, null, []);

        Assert.Equal("01/06/2026 - 14/09/2026",
            Convert(new TermPass(1, 1, 0, NoAlterations, withCatchUp, new TermClassSchedule(Monday9am, 10, 0))));
        Assert.Equal("01/09/2026 - 30/11/2026",
            Convert(new TermPass(2, 1, 0, NoAlterations, withoutCatchUp, new TermClassSchedule(Monday9am, 10, 0))));
    }

    [Fact]
    public void PassDateRange_ForACasualPass_IsNull_SoTheLineHides()
    {
        Assert.Null(Convert(new CasualPass(1, 1, 10, NoAlterations, 0)));
    }

    [Fact]
    public void HealthConcernsText_JoinsThem_OrIsNullWhenThereAreNone()
    {
        var converter = StringListTextConverter.Instance;

        Assert.Equal("Asthma, Bad knee",
            converter.Convert(new[] { "Asthma", "Bad knee" }, typeof(string), null, CultureInfo.InvariantCulture));
        Assert.Null(converter.Convert(Array.Empty<string>(), typeof(string), null, CultureInfo.InvariantCulture));
    }

    private static object? Convert(Pass pass) =>
        PassDateRangeTextConverter.Instance.Convert(pass, typeof(string), null, CultureInfo.InvariantCulture);

    /// <summary>A long list of health concerns used to run off the attendee row: the text sat in a
    /// horizontal StackPanel, which gives it unlimited width, so it never wrapped.</summary>
    [AvaloniaFact]
    public void LongHealthConcerns_WrapWithinTheAttendeeRow()
    {
        var fixture = CreateStore();
        var ada = AddStudent(fixture, "Ada", withPass: false,
            "Asthma (carries an inhaler)", "Recent left knee reconstruction - no deep flexion",
            "Lower back pain", "Pregnant (second trimester)", "High blood pressure");
        var viewModel = Open(fixture, new ClassRollEntry(ada, null));

        var window = new MarkRollWindow { DataContext = viewModel, Width = 420, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var text = window.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "HealthConcernsText");
        var row = text.FindAncestorOfType<Grid>()!.FindAncestorOfType<Grid>()!; // the attendee row

        Assert.True(text.TextLayout.TextLines.Count > 1, "the health concerns didn't wrap");
        Assert.True(text.TranslatePoint(new global::Avalonia.Point(text.Bounds.Width, 0), row)!.Value.X <= row.Bounds.Width + 0.5,
            "the health concerns run past the attendee row");

        window.Close();
    }
}
