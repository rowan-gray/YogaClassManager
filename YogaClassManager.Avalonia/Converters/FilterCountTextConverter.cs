using System.Globalization;
using Avalonia.Data.Converters;

namespace YogaClassManager.Avalonia.Converters;

/// <summary>"3 filters" / "1 filter" for FilterBar's filter button badge; null (not "0 filters") when
/// there's nothing active, so a bound TextBlock's IsVisible can key off Text being null-or-empty
/// rather than needing a second converter just to hide the "0 filters" case.</summary>
public class FilterCountTextConverter : IValueConverter
{
    public static readonly FilterCountTextConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is int count and > 0 ? $"{count} filter{(count == 1 ? "" : "s")}" : null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
