using System.Globalization;
using Avalonia.Data.Converters;
using YogaClassManager.Core.Models.Passes;

namespace YogaClassManager.Avalonia.Converters;

/// <summary>
///     The dates a pass is usable between, e.g. "01/02/2026 - 31/03/2026" - shown on Mark Roll's
///     attendee rows and in its pass picker. DatedPass: its start and end date. TermPass: the term's
///     start to <see cref="Pass.ExpiryDate" /> (the catch-up end if the term has one, else the term
///     end - the same date IsExpired compares against). CasualPass has no dates, so null - letting a
///     bound TextBlock hide itself via StringConverters.IsNotNullOrEmpty.
/// </summary>
public class PassDateRangeTextConverter : IValueConverter
{
    public static readonly PassDateRangeTextConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            DatedPass dated => Format(dated.StartDate, dated.EndDate),
            TermPass term => Format(term.Term.StartDate, term.ExpiryDate ?? term.Term.EndDate),
            _ => null
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    private static string Format(DateOnly start, DateOnly end) => $"{start:dd/MM/yyyy} - {end:dd/MM/yyyy}";
}
