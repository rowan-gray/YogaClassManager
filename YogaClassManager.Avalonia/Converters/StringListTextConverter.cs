using System.Globalization;
using Avalonia.Data.Converters;

namespace YogaClassManager.Avalonia.Converters;

/// <summary>Joins a list of strings for one-line display ("Asthma, Bad knee") - e.g. a student's
/// health concerns under their name on Mark Roll's attendee rows. Null for an empty or missing list,
/// so a bound TextBlock can hide itself via StringConverters.IsNotNullOrEmpty.</summary>
public class StringListTextConverter : IValueConverter
{
    public static readonly StringListTextConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not IEnumerable<string> items)
            return null;

        var text = string.Join(", ", items.Where(i => !string.IsNullOrWhiteSpace(i)));
        return text.Length == 0 ? null : text;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
