using System.Globalization;
using YogaClassManager.Core.Models.Classes;

namespace YogaClassManager.Core.Tests;

/// <summary>How a class reads everywhere it's shown ("Monday: 9:00 am") - 12-hour with lowercase
/// am/pm regardless of the machine's culture.</summary>
public class ClassScheduleDisplayTests
{
    [Theory]
    [InlineData(9, 0, "9:00 am")]
    [InlineData(12, 0, "12:00 pm")]
    [InlineData(0, 30, "12:30 am")]
    [InlineData(18, 45, "6:45 pm")]
    public void FormatTime_Is12HourWithLowercaseAmPm(int hour, int minute, string expected)
    {
        Assert.Equal(expected, ClassSchedule.FormatTime(new TimeOnly(hour, minute)));
    }

    [Fact]
    public void ToString_IsDayThenTime_RegardlessOfCulture()
    {
        var schedule = new ClassSchedule(1, DayOfWeek.Wednesday, new TimeOnly(12, 0), false);
        var original = CultureInfo.CurrentCulture;
        try
        {
            // A culture whose own designators are uppercase "AM"/"PM".
            CultureInfo.CurrentCulture = new CultureInfo("en-US");
            Assert.Equal("Wednesday: 12:00 pm", schedule.ToString());
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
