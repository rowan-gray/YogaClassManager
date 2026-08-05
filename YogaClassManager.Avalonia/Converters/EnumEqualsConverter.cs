using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace YogaClassManager.Avalonia.Converters;

/// <summary>Two-way bindable "does this value equal that fixed option" check, for wiring a fixed set
/// of RadioButtons to a single enum-valued property (e.g. FilterBar's Ascending/Descending pair) -
/// ConverterParameter is the already-boxed option value to compare/assign, not a string to parse, so
/// this works for any enum without needing to know its type ahead of time.</summary>
public class EnumEqualsConverter : IValueConverter
{
    public static readonly EnumEqualsConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is not null && value.Equals(parameter);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? parameter : BindingOperations.DoNothing;
    }
}
