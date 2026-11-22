using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using YogaClassManager.Avalonia.Controls;
using YogaClassManager.Avalonia.ViewModels.Shared;
using YogaClassManager.Avalonia.Views.Shared;
using YogaClassManager.Core.Dummy;
using YogaClassManager.Core.Models.Classes;
using YogaClassManager.Core.Models.People;

namespace YogaClassManager.Avalonia.Tests;

/// <summary>The attendee counts - beside "Attendees" in the Mark Roll window and in the Class Rolls
/// details pane - stay current as students are added and removed, and a roll's list-row count follows
/// a replaced attendee list.</summary>
public class AttendeeCountTests
{
    private static readonly ClassSchedule Monday9am = new(1, DayOfWeek.Monday, new TimeOnly(9, 0), false);

    private static Student NewStudent(int id, string firstName) => new(id, firstName, "Tester", "0400000000", null, true);

    /// <summary>The Caption-styled text right after a header TextBlock reading <paramref name="header" />.</summary>
    private static string? CountBeside(Control root, string header)
    {
        var headerBlock = root.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == header);
        var siblings = ((Panel)headerBlock.GetVisualParent()!).Children;
        var count = (TextBlock)siblings[siblings.IndexOf(headerBlock) + 1];
        Assert.Contains("Caption", count.Classes);
        return count.IsVisible ? count.Text : null;
    }

    [AvaloniaFact]
    public void MarkRoll_ShowsHowManyAreOnTheRoll_AsTheyChange()
    {
        var store = new InMemoryDataStore();
        var identities = new InMemoryIdentityRepository(store);
        var passes = new InMemoryPassRepository(store);
        var students = new InMemoryStudentRepository(store, identities, passes, new InMemoryEmergencyContactRepository(store));
        var viewModel = new MarkRollViewModel(
            new ClassRoll(0, new DateOnly(2026, 7, 20), Monday9am,
                [new ClassRollEntry(NewStudent(1, "Ada"), null), new ClassRollEntry(NewStudent(2, "Grace"), null)]),
            new InMemoryClassRollRepository(store), students, passes, new InMemoryTermRepository(store),
            new FakeToastService());
        var window = new MarkRollWindow { DataContext = viewModel, Width = 900, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("2", CountBeside(window, "Attendees"));

        viewModel.Entries.Add(new ClassRollEntry(NewStudent(3, "Joan"), null));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("3", CountBeside(window, "Attendees"));

        viewModel.Entries.Clear();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("0", CountBeside(window, "Attendees"));
    }

    [AvaloniaFact]
    public void LinkedRecordsPanel_WithShowCount_CountsItsItems_AsTheyChange()
    {
        var items = new ObservableCollection<string> { "Ada", "Grace" };
        var panel = new LinkedRecordsPanel { Header = "Attendees", ShowCount = true, ItemsSource = items };
        new Window { Content = panel, Width = 600, Height = 400 }.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("2", CountBeside(panel, "Attendees"));

        items.Remove("Ada");
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("1", CountBeside(panel, "Attendees"));

        // A refresh clears and refills in place.
        items.Clear();
        items.Add("Joan");
        items.Add("Kim");
        items.Add("Sam");
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("3", CountBeside(panel, "Attendees"));
    }

    [AvaloniaFact]
    public void LinkedRecordsPanel_HidesTheCount_UnlessAskedFor()
    {
        var panel = new LinkedRecordsPanel { Header = "Health concerns", ItemsSource = new[] { "Asthma" } };
        new Window { Content = panel, Width = 600, Height = 400 }.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Null(CountBeside(panel, "Health concerns"));
    }

    [Fact]
    public void ReplacingARollsAttendees_NotifiesTheListRowsCount()
    {
        var roll = new ClassRoll(1, new DateOnly(2026, 7, 20), Monday9am, []);
        var changed = new List<string?>();
        ((INotifyPropertyChanged)roll).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        roll.StudentEntries = [new ClassRollEntry(NewStudent(1, "Ada"), null)];

        Assert.Equal([nameof(ClassRoll.StudentEntries)], changed);
    }
}
