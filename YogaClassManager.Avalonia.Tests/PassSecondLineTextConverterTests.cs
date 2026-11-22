using System.Collections.ObjectModel;
using YogaClassManager.Avalonia.Converters;
using YogaClassManager.Core.Models.Classes;
using YogaClassManager.Core.Models.Passes;

namespace YogaClassManager.Avalonia.Tests;

/// <summary>Presentation-only formatting for the Students page's Passes list second line - kept out of
/// Core's Pass model (which has no UI concerns) and covered here instead.</summary>
public class PassSecondLineTextConverterTests
{
    private static readonly ObservableCollection<PassAlteration> NoAlterations = new();

    [Fact]
    public void DatedPass_ShowsItsDateRange()
    {
        var pass = new DatedPass(1, 1, 10, NoAlterations, 0, new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31));

        var text = PassSecondLineTextConverter.Instance.Convert(pass, typeof(string), null, null!);

        Assert.Equal("01/01/2026 - 31/03/2026", text);
    }

    [Theory]
    [InlineData(1, "1 class total")]
    [InlineData(10, "10 classes total")]
    public void CasualPass_ShowsTotalClassCount(int classCount, string expected)
    {
        var pass = new CasualPass(1, 1, classCount, NoAlterations, 0);

        var text = PassSecondLineTextConverter.Instance.Convert(pass, typeof(string), null, null!);

        Assert.Equal(expected, text);
    }

    [Fact]
    public void TermPass_ShowsTheTermName()
    {
        var schedule = new ClassSchedule(1, DayOfWeek.Monday, new TimeOnly(9, 0), false);
        var term = new Term(1, "Winter 2026", new DateOnly(2026, 6, 1), new DateOnly(2026, 8, 31), null, null, []);
        var pass = new TermPass(1, 1, 0, NoAlterations, term, new TermClassSchedule(schedule, 10, 0));

        var text = PassSecondLineTextConverter.Instance.Convert(pass, typeof(string), null, null!);

        Assert.Equal("Winter 2026", text);
    }

    [Fact]
    public void NullPass_ReturnsNull()
    {
        Assert.Null(PassSecondLineTextConverter.Instance.Convert(null, typeof(string), null, null!));
    }
}
