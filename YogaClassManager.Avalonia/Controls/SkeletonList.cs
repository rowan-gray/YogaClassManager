using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.LogicalTree;

namespace YogaClassManager.Avalonia.Controls;

/// <summary>
///     Skeleton loading placeholder for a list: <see cref="Rows" /> grey rows shaped like the real
///     rows (a title bar and, with <see cref="ShowSubtitle" />, a shorter second-line bar), padded
///     like a list row so the layout doesn't jump when the real items replace it. Shown in place of an
///     empty list while it loads (MasterDetailView, LinkedRecordsPanel) - a fast load then changes
///     "shape" into "content" rather than flashing a progress bar. Static on purpose: a looping
///     shimmer would break the app's ~200ms animation ceiling (UI_STYLE_GUIDE.md, "Animation").
/// </summary>
public class SkeletonList : StackPanel
{
    public static readonly StyledProperty<int> RowsProperty =
        AvaloniaProperty.Register<SkeletonList, int>(nameof(Rows), 6);

    public static readonly StyledProperty<bool> ShowSubtitleProperty =
        AvaloniaProperty.Register<SkeletonList, bool>(nameof(ShowSubtitle), true);

    // Varied widths so the placeholder reads as "a list of names", not a striped block.
    private static readonly double[] TitleWidths = [150, 120, 170, 135, 160, 110];
    private static readonly double[] SubtitleWidths = [110, 90, 125, 100, 80, 115];

    private Thickness rowPadding = new(12, 9, 12, 12); // FluentTheme's ListBoxItemPadding

    static SkeletonList()
    {
        RowsProperty.Changed.AddClassHandler<SkeletonList>((list, _) => list.Rebuild());
        ShowSubtitleProperty.Changed.AddClassHandler<SkeletonList>((list, _) => list.Rebuild());
    }

    public SkeletonList()
    {
        // Decorative: screen readers shouldn't walk a list of grey bars.
        IsHitTestVisible = false;
        Rebuild();
    }

    public int Rows
    {
        get => GetValue(RowsProperty);
        set => SetValue(RowsProperty, value);
    }

    public bool ShowSubtitle
    {
        get => GetValue(ShowSubtitleProperty);
        set => SetValue(ShowSubtitleProperty, value);
    }

    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);

        // Resources resolve only once the control is in the tree.
        if (this.TryFindResource("ListBoxItemPadding", ActualThemeVariant, out var padding) &&
            padding is Thickness thickness)
        {
            rowPadding = thickness;
            Rebuild();
        }
    }

    private void Rebuild()
    {
        Children.Clear();
        for (var i = 0; i < Rows; i++)
        {
            var lines = new StackPanel { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Left };
            lines.Children.Add(Bar(14, TitleWidths[i % TitleWidths.Length]));
            if (ShowSubtitle)
                lines.Children.Add(Bar(10, SubtitleWidths[i % SubtitleWidths.Length]));

            // Same padding a real row gets from ListBoxItem, so swapping skeleton for items doesn't jump.
            Children.Add(new Border { Child = lines, Padding = rowPadding });
        }
    }

    private static Border Bar(double height, double width)
    {
        var bar = new Border { Height = height, Width = width };
        bar.Classes.Add("Skeleton");
        return bar;
    }
}
