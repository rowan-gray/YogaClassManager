using System.Globalization;
using Avalonia.Data.Converters;
using YogaClassManager.Core.Models.Passes;

namespace YogaClassManager.Avalonia.Converters;

/// <summary>The Students page's "Classes attended" list shows which pass (if any) a class was used
/// against, with a subtype-specific detail folded in for DatedPass/TermPass - "Dated Pass (start -
/// end)", "Term Pass (term name)" - since PassName alone ("Dated Pass") doesn't say which one when a
/// student has more than one of the same kind. CasualPass has no equivalent distinguishing detail, so
/// it falls back to its plain PassName. Always returns non-null text (including "No pass"), unlike
/// PassSecondLineTextConverter, so the caller needs no separate null-check TextBlock/IsVisible pair.</summary>
public class PassUsedTextConverter : IValueConverter
{
    public static readonly PassUsedTextConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            DatedPass dated => $"Dated Pass ({dated.StartDate:dd/MM/yyyy} - {dated.EndDate:dd/MM/yyyy})",
            TermPass term => $"Term Pass ({term.Term.Name})",
            Pass pass => pass.PassName,
            _ => "No pass"
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
