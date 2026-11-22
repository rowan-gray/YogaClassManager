using System.Globalization;
using Avalonia.Data.Converters;

namespace YogaClassManager.Avalonia.Converters;

/// <summary>Item-text converter for a dropdown whose options include a leading `null` "no selection"
/// entry (e.g. a nullable DayOfWeek filter's `null`-prefixed options list) - renders that entry as
/// "No Selection" instead of a blank row, and every other item via its own ToString(). Pair with
/// `Classes.Muted="{Binding Converter={x:Static conv:ObjectConverters.IsNull}}"` on the same TextBlock
/// so the "No Selection" row also reads as secondary/grey text.</summary>
public class NullableOptionTextConverter : IValueConverter
{
    public static readonly NullableOptionTextConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value?.ToString() ?? "No Selection";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
