using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using YogaClassManager.Avalonia.Controls;

namespace YogaClassManager.Avalonia.Tests;

/// <summary>While the main list loads, the details pane shows a skeleton (the first record is about to
/// be auto-selected) instead of flashing its "Select a ..." prompt.</summary>
public class DetailSkeletonTests
{
    private static (MasterDetailView View, StackPanel Skeleton, TextBlock Prompt) Render(bool busy, object? selected)
    {
        var items = new ObservableCollection<string>();
        if (selected is string s) items.Add(s);
        var view = new MasterDetailView
        {
            Items = items, IsBusy = busy, SelectedItem = selected, NoSelectionText = "Select a student."
        };
        new Window { Content = view, Width = 1000, Height = 600 }.Show();
        Dispatcher.UIThread.RunJobs();

        var skeleton = view.GetVisualDescendants().OfType<StackPanel>().Single(p => p.Name == "DetailSkeleton");
        var prompt = view.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == "Select a student.");
        return (view, skeleton, prompt);
    }

    [AvaloniaFact]
    public void WhileTheListLoads_TheDetailsShowASkeleton_NotThePrompt()
    {
        var (_, skeleton, prompt) = Render(busy: true, selected: null);
        Assert.True(skeleton.IsVisible);
        Assert.False(prompt.IsVisible);
    }

    [AvaloniaFact]
    public void OnceLoadedWithNothingSelected_ThePromptShows()
    {
        var (view, skeleton, prompt) = Render(busy: true, selected: null);
        view.IsBusy = false;
        Dispatcher.UIThread.RunJobs();

        Assert.False(skeleton.IsVisible);
        Assert.True(prompt.IsVisible);

        // Centred in the details pane, both ways.
        var pane = (Control)prompt.GetVisualParent()!;
        var centre = prompt.Bounds.Center;
        Assert.Equal(pane.Bounds.Width / 2, centre.X, precision: 0);
        Assert.Equal(pane.Bounds.Height / 2, centre.Y, precision: 0);
    }

    [AvaloniaFact]
    public void ARefreshWithARecordSelected_KeepsShowingThatRecord()
    {
        var (_, skeleton, prompt) = Render(busy: true, selected: "Ada");
        Assert.False(skeleton.IsVisible);
        Assert.False(prompt.IsVisible);
    }
}
