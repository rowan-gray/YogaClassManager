using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ReactiveUI;
using YogaClassManager.Avalonia.Controls;
using YogaClassManager.Avalonia.Converters;

namespace YogaClassManager.Avalonia.Tests;

/// <summary>A filter's date input can be emptied again: its clear button writes null back through the
/// same DateOnly converter the views bind with, and is disabled while there's nothing to clear.</summary>
public class DateFilterFieldTests
{
    private sealed class Filters : ReactiveObject
    {
        private DateOnly? dateFrom;

        public DateOnly? DateFrom
        {
            get => dateFrom;
            set => this.RaiseAndSetIfChanged(ref dateFrom, value);
        }
    }

    private static (DateFilterField Field, Button Clear, Filters Filters) Render(DateOnly? initial)
    {
        var filters = new Filters { DateFrom = initial };
        var field = new DateFilterField { Label = "From", DataContext = filters };
        field.Bind(DateFilterField.SelectedDateProperty,
            new Binding(nameof(Filters.DateFrom)) { Converter = DateOnlyToDateTimeConverter.Instance });
        new Window { Content = field }.Show();
        Dispatcher.UIThread.RunJobs();

        var clear = field.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "ClearButton");
        return (field, clear, filters);
    }

    [AvaloniaFact]
    public void Clearing_EmptiesTheBoundDate()
    {
        var (_, clear, filters) = Render(new DateOnly(2026, 5, 1));
        Assert.True(clear.IsEnabled);

        clear.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Null(filters.DateFrom);
        Assert.False(clear.IsEnabled);
    }

    [AvaloniaFact]
    public void TheClearButton_IsDisabledWithNoDate_AndEnablesOncePicked()
    {
        var (field, clear, filters) = Render(null);
        Assert.False(clear.IsEnabled);

        filters.DateFrom = new DateOnly(2026, 5, 1);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(new DateTime(2026, 5, 1), field.SelectedDate);
        Assert.True(clear.IsEnabled);
    }

    [AvaloniaFact]
    public void TheClearButton_IsNamedAfterItsField()
    {
        var (_, clear, _) = Render(null);
        Assert.Equal("Clear From", AutomationProperties.GetName(clear));
    }
}
