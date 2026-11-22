using System.Collections.ObjectModel;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using YogaClassManager.Avalonia.Controls;
using YogaClassManager.Avalonia.Services;

namespace YogaClassManager.Avalonia.Tests;

/// <summary>Narrow-window behaviour: the side menu collapses, and the list never out-sizes the details.</summary>
public class ResponsiveLayoutTests
{
    [AvaloniaFact]
    public void ANarrowWindow_ForcesTheMenuClosed_AsAnOverlay_AndWideningRestoresIt()
    {
        var viewModel = new MainWindowViewModel(new AppScreen(), new DialogService());
        Assert.True(viewModel.IsPaneOpen);

        viewModel.IsNarrow = true;
        Assert.False(viewModel.IsPaneOpen);
        Assert.Equal(SplitViewDisplayMode.CompactOverlay, viewModel.PaneDisplayMode);

        // Opening it while narrow overlays the page; choosing a page closes it again.
        viewModel.ToggleNavCommand.Execute().Subscribe();
        Assert.True(viewModel.IsPaneOpen);
        viewModel.NavItems[0].Command.Execute().Subscribe(_ => { }, _ => { });
        Assert.False(viewModel.IsPaneOpen);

        viewModel.IsNarrow = false;
        Assert.True(viewModel.IsPaneOpen); // back to how it was left while wide
        Assert.Equal(SplitViewDisplayMode.CompactInline, viewModel.PaneDisplayMode);
    }

    [AvaloniaFact]
    public void ClosingTheMenuWhileWide_IsRemembered_AcrossANarrowWindow()
    {
        var viewModel = new MainWindowViewModel(new AppScreen(), new DialogService());
        viewModel.ToggleNavCommand.Execute().Subscribe(); // closed by choice, while wide

        viewModel.IsNarrow = true;
        viewModel.IsNarrow = false;

        Assert.False(viewModel.IsPaneOpen);
    }

    private static (double List, double Details) MasterDetail(double windowWidth)
    {
        var view = new MasterDetailView { Items = new ObservableCollection<string> { "Ada" } };
        new Window { Content = view, Width = windowWidth, Height = 500 }.Show();
        Dispatcher.UIThread.RunJobs();

        var columns = ((Grid)view.Content!).ColumnDefinitions;
        return (columns[0].ActualWidth, columns[2].ActualWidth);
    }

    [AvaloniaFact]
    public void InANarrowWindow_TheListIsNeverWiderThanTheDetails()
    {
        var (list, details) = MasterDetail(windowWidth: 500);
        Assert.True(list <= details + 0.5, $"list {list} wider than details {details}");
        Assert.True(list < 320, "the list should give up width in a narrow window");
    }

    [AvaloniaFact]
    public void InAWideWindow_TheListStaysCappedAt320()
    {
        var (list, details) = MasterDetail(windowWidth: 1400);
        Assert.Equal(320, list, precision: 1);
        Assert.True(details > list);
    }
}
