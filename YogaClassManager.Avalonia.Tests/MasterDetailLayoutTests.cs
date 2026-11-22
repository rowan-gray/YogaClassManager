using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using YogaClassManager.Avalonia.Controls;

namespace YogaClassManager.Avalonia.Tests;

/// <summary>The main list's rows (and so the selection highlight) line up with the search box and
/// buttons above them, rather than running wider - both sit 8px in from the card's edges.</summary>
public class MasterDetailLayoutTests
{
    [AvaloniaFact]
    public void ListRows_LineUpWithTheListHeader()
    {
        var header = new TextBox { Watermark = "Search by name..." };
        var view = new MasterDetailView
        {
            Items = new ObservableCollection<string> { "Ada Lovelace" },
            ListHeader = header
        };
        var window = new Window { Content = view, Width = 1000, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var row = view.GetVisualDescendants().OfType<ListBoxItem>().Single();

        static (double Left, double Right) Edges(Visual visual, Visual root)
        {
            var topLeft = visual.TranslatePoint(new Point(0, 0), root)!.Value;
            return (topLeft.X, topLeft.X + visual.Bounds.Width);
        }

        var (headerLeft, headerRight) = Edges(header, window);
        var (rowLeft, rowRight) = Edges(row, window);

        Assert.Equal(headerLeft, rowLeft, precision: 1);
        Assert.Equal(headerRight, rowRight, precision: 1);

        // The gap below the list matches the gap above the header.
        var card = row.FindAncestorOfType<ListBox>()!.FindAncestorOfType<Border>(includeSelf: false)!;
        while (!card.Classes.Contains("Card"))
            card = card.FindAncestorOfType<Border>()!;
        var list = row.FindAncestorOfType<ListBox>()!;
        var cardTop = card.TranslatePoint(new Point(0, 0), window)!.Value.Y;
        var cardBottom = cardTop + card.Bounds.Height;
        var headerTop = header.TranslatePoint(new Point(0, 0), window)!.Value.Y;
        var listBottom = list.TranslatePoint(new Point(0, list.Bounds.Height), window)!.Value.Y;

        Assert.Equal(headerTop - cardTop, cardBottom - listBottom, precision: 1);
        window.Close();
    }
}
