using Avalonia;
using Avalonia.Controls;

namespace YogaClassManager.Avalonia.Controls;

/// <summary>
///     Lays out children in rows, packing as many as fit per row using each child's own MinWidth (a
///     child with no MinWidth set always fits), then stretches every child in a row to share the
///     available width equally - unlike a plain WrapPanel, which shrinks each child to its own
///     desired size instead of filling the row. Used for the Emergency contacts/Health concerns
///     panels on the Student detail page: side-by-side, evenly filling the available width, when
///     there's room for both at their MinWidth; stacked full-width otherwise.
/// </summary>
public class FillWrapPanel : Panel
{
    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<FillWrapPanel, double>(nameof(Spacing));

    // Remembered from Measure - Arrange's finalSize is always a concrete rect (Avalonia never calls
    // ArrangeOverride with an infinite size), so this is the only place "was I given a bounded height"
    // can be observed. Drives whether rows stretch to fill (bounded - e.g. inside a fixed-size Window)
    // or size to their own content (unbounded - e.g. inside a page's outer ScrollViewer, where
    // stretching to "fill" infinite height would be meaningless).
    private bool heightBoundedAtMeasure;

    static FillWrapPanel()
    {
        SpacingProperty.Changed.AddClassHandler<FillWrapPanel>((panel, _) => panel.InvalidateMeasure());
    }

    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        heightBoundedAtMeasure = !double.IsInfinity(availableSize.Height);

        var rows = BuildRows(availableSize.Width);
        var bounded = !double.IsInfinity(availableSize.Width);
        var totalHeight = 0.0;
        var maxRowWidth = 0.0;

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var itemWidth = bounded
                ? (availableSize.Width - Spacing * (row.Count - 1)) / row.Count
                : double.PositiveInfinity;

            var rowHeight = 0.0;
            var measuredRowWidth = 0.0;

            foreach (var child in row)
            {
                child.Measure(new Size(itemWidth, availableSize.Height));
                rowHeight = Math.Max(rowHeight, child.DesiredSize.Height);
                measuredRowWidth += child.DesiredSize.Width;
            }

            measuredRowWidth += Spacing * (row.Count - 1);

            totalHeight += rowHeight + (i > 0 ? Spacing : 0);
            maxRowWidth = Math.Max(maxRowWidth, bounded ? availableSize.Width : measuredRowWidth);
        }

        return new Size(bounded ? availableSize.Width : maxRowWidth, totalHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var rows = BuildRows(finalSize.Width);
        var y = 0.0;

        // Bounded height (e.g. this panel sizing to fill a fixed-height Window row): give every row an
        // equal share of the full available height, so content designed to stretch (e.g. a Grid's "*"
        // row containing a ListBox) actually fills it instead of being arranged at whatever smaller
        // height it happened to report back from Measure.
        var fillRowHeight = heightBoundedAtMeasure && rows.Count > 0
            ? (finalSize.Height - Spacing * (rows.Count - 1)) / rows.Count
            : (double?)null;

        foreach (var row in rows)
        {
            var itemWidth = (finalSize.Width - Spacing * (row.Count - 1)) / row.Count;

            double rowHeight;
            if (fillRowHeight is { } fill)
            {
                rowHeight = fill;
            }
            else
            {
                rowHeight = 0.0;
                foreach (var child in row)
                    rowHeight = Math.Max(rowHeight, child.DesiredSize.Height);
            }

            var x = 0.0;
            foreach (var child in row)
            {
                child.Arrange(new Rect(x, y, itemWidth, rowHeight));
                x += itemWidth + Spacing;
            }

            y += rowHeight + Spacing;
        }

        return finalSize;
    }

    private List<List<Control>> BuildRows(double availableWidth)
    {
        var bounded = !double.IsInfinity(availableWidth);
        var rows = new List<List<Control>>();
        var current = new List<Control>();
        var currentMinWidth = 0.0;

        foreach (var child in Children)
        {
            if (!child.IsVisible)
                continue;

            var childMinWidth = child.MinWidth > 0 ? child.MinWidth : 0;
            var candidateWidth = currentMinWidth + (current.Count > 0 ? Spacing : 0) + childMinWidth;

            if (current.Count > 0 && bounded && candidateWidth > availableWidth)
            {
                rows.Add(current);
                current = [child];
                currentMinWidth = childMinWidth;
            }
            else
            {
                current.Add(child);
                currentMinWidth = candidateWidth;
            }
        }

        if (current.Count > 0)
            rows.Add(current);

        return rows;
    }
}
