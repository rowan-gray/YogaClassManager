using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using YogaClassManager.Avalonia.ViewModels.Identities;
using YogaClassManager.Avalonia.Views.Identities;
using YogaClassManager.Core.Dummy;
using YogaClassManager.Core.Models.People;

namespace YogaClassManager.Avalonia.Tests;

/// <summary>The People page's "Student" and "Emergency contact for" cards space their content the
/// same: the same gap under the heading, and nothing extra below the last link.</summary>
public class IdentityCardSpacingTests
{
    /// <summary>Renders the People page with Judi selected - a student AND Kenneth's emergency contact, so
    /// both cards show - and waits for her links to load.</summary>
    private static async Task<Window> RenderJudiAsync()
    {
        var store = new InMemoryDataStore();
        var identities = new InMemoryIdentityRepository(store);
        var contacts = new InMemoryEmergencyContactRepository(store);
        var students = new InMemoryStudentRepository(store, identities, new InMemoryPassRepository(store), contacts);

        // Judi is a student AND Kenneth's emergency contact, so both cards show.
        var judiId = await students.AddAsync(new Student(0, "Judi", "Dench", "0400000000", null, true));
        var kennethId = await students.AddAsync(new Student(0, "Kenneth", "Branagh", "0411111111", null, true));
        await students.LinkEmergencyContactAsync(kennethId, judiId, Relationship.Friend);

        var viewModel = new IdentitiesViewModel(new AppScreen(), identities, students, contacts,
            new ScriptedDialogService(), new FakeToastService(), new FakeNavigator());
        await viewModel.RefreshCommand.Execute();
        var window = new Window { Content = new IdentitiesView { DataContext = viewModel }, Width = 1200, Height = 800 };
        window.Show();
        viewModel.Selection = viewModel.Items.Single(i => i.Id == judiId);

        // The linked-data load runs off the trigger asynchronously; pump until both cards are filled.
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while ((viewModel.LinkedStudent is null || !viewModel.HasEmergencyContactLinks) && DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static Border CardWithHeading(Window window, string heading)
    {
        var title = window.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == heading && t.Opacity > 0);
        return title.GetVisualAncestors().OfType<Border>().First(b => b.Classes.Contains("Card"));
    }

    [AvaloniaFact]
    public async Task StudentAndEmergencyContactCards_HaveTheSameSpacing()
    {
        var window = await RenderJudiAsync();

        (double HeadingGap, double BottomGap) Spacing(string heading)
        {
            var title = window.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == heading && t.Opacity > 0);
            var card = CardWithHeading(window, heading);
            var links = card.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("RecordLink")).ToList();
            double Top(Visual v) => v.TranslatePoint(new Point(0, 0), card)!.Value.Y;

            var headingGap = Top(links.First()) - (Top(title) + title.Bounds.Height);
            var bottomGap = card.Bounds.Height - (Top(links.Last()) + links.Last().Bounds.Height);
            return (headingGap, bottomGap);
        }

        var student = Spacing("Student");
        var contactFor = Spacing("Emergency contact for");

        Assert.Equal(student.HeadingGap, contactFor.HeadingGap, precision: 1);
        Assert.Equal(student.BottomGap, contactFor.BottomGap, precision: 1);
        window.Close();
    }

    /// <summary>The loading placeholder for these cards must be exactly as tall as the card it becomes, or
    /// the details pane jumps when the links arrive.</summary>
    [AvaloniaFact]
    public async Task TheLoadingPlaceholderCard_IsAsTallAsTheStudentCard()
    {
        var window = await RenderJudiAsync();
        var studentCard = CardWithHeading(window, "Student");
        // The placeholder is the card holding the invisible "Student" caption that sizes its heading bar.
        // (Matching "any Card containing a Skeleton" also hits MasterDetailView's own detail skeleton.)
        var placeholderCaption = window.GetVisualDescendants().OfType<TextBlock>()
            .Single(t => t.Text == "Student" && t.Opacity == 0);
        var placeholderCard = placeholderCaption.GetVisualAncestors().OfType<Border>().First(b => b.Classes.Contains("Card"));

        // Loading has finished, so the placeholder is hidden; show it alongside the real card to measure both.
        placeholderCard.IsVisible = true;
        Dispatcher.UIThread.RunJobs();

        Assert.True(studentCard.Bounds.Height > 0);
        Assert.Equal(studentCard.Bounds.Height, placeholderCard.Bounds.Height, precision: 1);
        window.Close();
    }
}
