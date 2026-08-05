using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace YogaClassManager.Avalonia.Converters;

/// <summary>
///     Bridges DateOnly ViewModel properties to Avalonia's CalendarDatePicker/DatePicker, whose
///     SelectedDate is DateTime? - mirrors the MAUI app's own Converters/DateOnlyToDateTimeConverter.cs,
///     needed for the same reason (the picker control has no native DateOnly support).
/// </summary>
public class DateOnlyToDateTimeConverter : IValueConverter
{
    public static readonly DateOnlyToDateTimeConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            DateOnly date => date.ToDateTime(TimeOnly.MinValue),
            null => null,
            _ => AvaloniaProperty.UnsetValue
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            DateTime dateTime => DateOnly.FromDateTime(dateTime),
            DateTimeOffset dateTimeOffset => DateOnly.FromDateTime(dateTimeOffset.DateTime),
            null => null,
            _ => AvaloniaProperty.UnsetValue
        };
    }
}
