using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;

namespace YogaClassManager.Avalonia.Controls;

/// <summary>
///     A labelled date input for a sort/filter flyout, with a clear button that sets it back to "no date"
///     (CalendarDatePicker can't be emptied once a date is picked). Use it for every date in a filter;
///     form dates in dialogs (Term/Pass edit, Mark Roll) are required values and keep a plain picker.
///     <see cref="SelectedDate" /> binds two-way by default, through DateOnlyToDateTimeConverter for a
///     DateOnly? ViewModel property, exactly as a bare CalendarDatePicker's SelectedDate did.
/// </summary>
public partial class DateFilterField : UserControl
{
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<DateFilterField, string?>(nameof(Label));

    public static readonly StyledProperty<DateTime?> SelectedDateProperty =
        AvaloniaProperty.Register<DateFilterField, DateTime?>(nameof(SelectedDate),
            defaultBindingMode: BindingMode.TwoWay);

    public DateFilterField()
    {
        InitializeComponent();
    }

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public DateTime? SelectedDate
    {
        get => GetValue(SelectedDateProperty);
        set => SetValue(SelectedDateProperty, value);
    }

    private void OnClearClick(object? sender, RoutedEventArgs e)
    {
        SelectedDate = null;
    }
}
