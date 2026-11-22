using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using YogaClassManager.Avalonia.Controls;

namespace YogaClassManager.Avalonia.Tests;

/// <summary>Skeleton placeholders stand in for a list while it loads and has nothing to show - and
/// only then, so a refresh of an already-populated list doesn't swap its rows for grey bars.</summary>
public class SkeletonLoadingTests
{
    private static T Render<T>(T control) where T : Control
    {
        new Window { Content = control, Width = 900, Height = 700 }.Show();
        Dispatcher.UIThread.RunJobs();
        return control;
    }

    // The list's skeleton - not MasterDetailView's details-pane skeleton (DetailSkeleton), which also
    // contains a SkeletonList.
    private static SkeletonList Skeleton(Control root) =>
        Assert.Single(root.GetVisualDescendants().OfType<SkeletonList>()
            .Where(s => !s.GetVisualAncestors().OfType<StackPanel>().Any(p => p.Name == "DetailSkeleton")));

    [AvaloniaFact]
    public void SkeletonList_BuildsTheRequestedRows_WithOrWithoutASecondLine()
    {
        var twoLine = Render(new SkeletonList { Rows = 4 });
        Assert.Equal(4, twoLine.Children.Count);
        Assert.All(twoLine.Children, row =>
            Assert.Equal(2, ((StackPanel)((Border)row).Child!).Children.Count));

        var oneLine = Render(new SkeletonList { Rows = 2, ShowSubtitle = false });
        Assert.All(oneLine.Children, row =>
            Assert.Single(((StackPanel)((Border)row).Child!).Children));
    }

    [AvaloniaFact]
    public void MasterDetailView_ShowsTheSkeleton_OnlyWhileBusyAndEmpty()
    {
        var items = new ObservableCollection<string>();
        var view = Render(new MasterDetailView { Items = items, IsBusy = true });
        var skeleton = Skeleton(view);
        Assert.True(skeleton.IsVisible);

        // A refresh of a list that already has rows keeps the rows, not grey bars.
        items.Add("Ada");
        Dispatcher.UIThread.RunJobs();
        Assert.False(skeleton.IsVisible);

        items.Clear();
        view.IsBusy = false;
        Dispatcher.UIThread.RunJobs();
        Assert.False(skeleton.IsVisible); // loaded, genuinely empty: the empty text's turn
    }

    [AvaloniaFact]
    public void LinkedRecordsPanel_ShowsTheSkeleton_NotTheEmptyText_WhileLoading()
    {
        var panel = Render(new LinkedRecordsPanel
        {
            Header = "Passes",
            EmptyText = "No passes match the current filters.",
            ItemsSource = new ObservableCollection<string>(),
            IsLoading = true
        });
        var emptyText = panel.GetVisualDescendants().OfType<TextBlock>()
            .Single(t => t.Text == "No passes match the current filters.");

        Assert.True(Skeleton(panel).IsVisible);
        Assert.False(emptyText.IsVisible);

        panel.IsLoading = false;
        Dispatcher.UIThread.RunJobs();

        Assert.False(Skeleton(panel).IsVisible);
        Assert.True(emptyText.IsVisible);
    }
}
