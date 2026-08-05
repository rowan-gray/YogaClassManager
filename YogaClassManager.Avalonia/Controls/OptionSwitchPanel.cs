using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace YogaClassManager.Avalonia.Controls;

/// <summary>
///     Shows exactly one child, chosen by matching each child's attached Option value against
///     SelectedOption - a direct port of the MAUI app's Components/OptionsLayout.cs (custom
///     ILayoutManager that measures/arranges only the matching child). Used for type-discriminated
///     forms, e.g. switching between Dated/Casual/Term pass fields.
/// </summary>
public class OptionSwitchPanel : Panel
{
    public static readonly StyledProperty<object?> SelectedOptionProperty =
        AvaloniaProperty.Register<OptionSwitchPanel, object?>(nameof(SelectedOption));

    public static readonly AttachedProperty<object?> OptionProperty =
        AvaloniaProperty.RegisterAttached<OptionSwitchPanel, Control, object?>("Option");

    static OptionSwitchPanel()
    {
        SelectedOptionProperty.Changed.AddClassHandler<OptionSwitchPanel>((panel, _) => panel.InvalidateMeasure());
        OptionProperty.Changed.AddClassHandler<Control>((control, _) =>
        {
            if (control.GetVisualParent() is OptionSwitchPanel panel)
                panel.InvalidateMeasure();
        });
    }

    public object? SelectedOption
    {
        get => GetValue(SelectedOptionProperty);
        set => SetValue(SelectedOptionProperty, value);
    }

    public static void SetOption(Control control, object? value)
    {
        control.SetValue(OptionProperty, value);
    }

    public static object? GetOption(Control control)
    {
        return control.GetValue(OptionProperty);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var visibleChild = GetVisibleChild();
        var measured = default(Size);

        foreach (var child in Children)
        {
            if (child == visibleChild)
            {
                child.Measure(availableSize);
                measured = child.DesiredSize;
            }
            else
            {
                child.Measure(default);
            }
        }

        return measured;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var visibleChild = GetVisibleChild();

        foreach (var child in Children)
            child.Arrange(child == visibleChild ? new Rect(finalSize) : default);

        return finalSize;
    }

    private Control? GetVisibleChild()
    {
        foreach (var child in Children)
        {
            if (Equals(GetOption(child), SelectedOption))
                return child;
        }

        return null;
    }
}
