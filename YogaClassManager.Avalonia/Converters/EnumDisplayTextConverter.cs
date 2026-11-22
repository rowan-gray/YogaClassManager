using System.Globalization;
using System.Text;
using Avalonia.Data.Converters;

namespace YogaClassManager.Avalonia.Converters;

/// <summary>
///     Turns an enum member (or any PascalCase identifier) into display text: Capital Case, with a
///     space before each capital - "FirstName" -> "First Name", "DayOfWeek" -> "Day Of Week". Used for
///     SortByButton's field labels, which come straight from each filter's sort enum.
/// </summary>
public class EnumDisplayTextConverter : IValueConverter
{
    public static readonly EnumDisplayTextConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is null ? null : ToDisplayText(value.ToString());
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    public static string ToDisplayText(string? identifier)
    {
        if (string.IsNullOrEmpty(identifier))
            return "";

        var text = new StringBuilder(identifier.Length + 4);
        for (var i = 0; i < identifier.Length; i++)
        {
            var c = identifier[i];
            if (i == 0)
            {
                text.Append(char.ToUpperInvariant(c));
                continue;
            }

            // Break before a capital that starts a new word: after a lowercase letter/digit
            // ("firstName"), or ending a run of capitals ("IDNumber" -> "ID Number").
            if (char.IsUpper(c) && (!char.IsUpper(identifier[i - 1]) ||
                                    (i + 1 < identifier.Length && char.IsLower(identifier[i + 1]))))
                text.Append(' ');

            text.Append(c);
        }

        return text.ToString();
    }
}
