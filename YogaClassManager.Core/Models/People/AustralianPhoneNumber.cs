using System.Text.RegularExpressions;

namespace YogaClassManager.Core.Models.People;

/// <summary>Accepts the ways an Australian phone number is commonly written and normalises it to bare
/// digits in national format, which is how phone numbers are stored.</summary>
public static partial class AustralianPhoneNumber
{
    /// <summary>
    ///     Normalises <paramref name="input" /> if it's a valid Australian phone number. Spaces, dashes,
    ///     dots and brackets are ignored, and a +61 / 0061 prefix (with or without the "(0)" people
    ///     sometimes write after it) becomes a leading 0. Accepted, after that:
    ///     <list type="bullet">
    ///         <item>mobiles and landlines with an area code: 04, 02, 03, 07, 08 + 8 digits</item>
    ///         <item>local landlines without an area code: 8 digits, not starting with 0 or 1</item>
    ///         <item>1300 / 1800 numbers (10 digits) and 13 numbers (6 digits)</item>
    ///     </list>
    /// </summary>
    /// <example>"+61 (0)412 345 678" and "0412-345-678" both give "0412345678"; "(02) 9876 5432" gives
    /// "0298765432".</example>
    public static bool TryNormalise(string input, out string normalised)
    {
        var digits = Separators().Replace(input, "");

        if (digits.StartsWith("+61"))
            digits = "0" + digits[3..];
        else if (digits.StartsWith("0061"))
            digits = "0" + digits[4..];
        if (digits.StartsWith("00") && digits.Length == 11) // "+61 (0)4..." -> "004..."
            digits = digits[1..];

        if (Valid().IsMatch(digits))
        {
            normalised = digits;
            return true;
        }

        normalised = input;
        return false;
    }

    [GeneratedRegex(@"[\s\-.()]")]
    private static partial Regex Separators();

    [GeneratedRegex(@"^(0[23478]\d{8}|[2-9]\d{7}|1[38]00\d{6}|13\d{4})$")]
    private static partial Regex Valid();
}
