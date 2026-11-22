using System.Globalization;
using Avalonia.Data.Converters;
using YogaClassManager.Core.Models.Passes;

namespace YogaClassManager.Avalonia.Converters;

/// <summary>The Students page's Passes list shows one subtype-specific detail on a pass row's second
/// line - a date range for DatedPass, the term name for TermPass, the total class count for
/// CasualPass. That's presentation, not domain data, so it lives here rather than as a display-string
/// property on Core's Pass model; null (not an empty string) when a subtype has nothing to add, so a
/// bound TextBlock's IsVisible can key off Text being null-or-empty the same way FilterCountTextConverter's
/// callers do, rather than needing a second converter.</summary>
public class PassSecondLineTextConverter : IValueConverter
{
    public static readonly PassSecondLineTextConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            DatedPass dated => $"{dated.StartDate:dd/MM/yyyy} - {dated.EndDate:dd/MM/yyyy}",
            CasualPass casual => casual.NumberOfClasses == 1 ? "1 class total" : $"{casual.NumberOfClasses} classes total",
            TermPass term => term.Term.Name,
            _ => null
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
