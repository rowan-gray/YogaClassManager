using System.Globalization;
using YogaClassManager.Avalonia.Converters;
using YogaClassManager.Core.Filters;

namespace YogaClassManager.Avalonia.Tests;

/// <summary>SortByButton's field labels come straight from each filter's sort enum - this is what
/// turns "LastAttendance" into "Last Attendance".</summary>
public class EnumDisplayTextConverterTests
{
    [Theory]
    [InlineData("FirstName", "First Name")]
    [InlineData("LastAttendance", "Last Attendance")]
    [InlineData("DayOfWeek", "Day Of Week")]
    [InlineData("Date", "Date")]
    [InlineData("firstName", "First Name")]
    [InlineData("IDNumber", "ID Number")]
    [InlineData("", "")]
    public void ToDisplayText_CapitalisesAndSpacesWords(string identifier, string expected)
    {
        Assert.Equal(expected, EnumDisplayTextConverter.ToDisplayText(identifier));
    }

    [Fact]
    public void Convert_FormatsEnumMembers()
    {
        var text = EnumDisplayTextConverter.Instance.Convert(StudentSortOptions.LastAttendance, typeof(string), null,
            CultureInfo.InvariantCulture);

        Assert.Equal("Last Attendance", text);
    }
}
