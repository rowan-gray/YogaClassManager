using System.Collections.ObjectModel;
using YogaClassManager.Core.Models.Classes;
using YogaClassManager.Core.Models.Passes;

namespace YogaClassManager.Core.Tests;

/// <summary>The IsValid rules the Term/Pass edit dialogs rely on to refuse a bad save, and the boundary
/// dates of TermPass.GetPassUsagePriority (which pass the roll auto-selects).</summary>
public class ValidationRuleTests
{
    private static readonly DateOnly Start = new(2026, 2, 2);
    private static readonly DateOnly End = new(2026, 4, 10);
    private static readonly ClassSchedule Schedule = new(1, DayOfWeek.Monday, new TimeOnly(9, 0), false);

    private static Term TermWith(string name = "Term 1", DateOnly? start = null, DateOnly? end = null,
        DateOnly? catchupStart = null, DateOnly? catchupEnd = null)
    {
        return new Term(1, name, start ?? Start, end ?? End, catchupStart, catchupEnd, []);
    }

    private static ObservableCollection<PassAlteration> Alterations(params int[] amounts)
    {
        return new ObservableCollection<PassAlteration>(amounts.Select((a, i) => new PassAlteration(i + 1, 1, a, "Adjustment")));
    }

    // ---- Term.IsValid ----

    [Fact]
    public void Term_IsValid_WithoutCatchup()
    {
        Assert.True(TermWith().IsValid());
    }

    [Fact]
    public void Term_IsValid_WithCatchupBracketingTheTerm()
    {
        Assert.True(TermWith(catchupStart: Start.AddDays(-7), catchupEnd: End.AddDays(7)).IsValid());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Term_IsInvalid_WithABlankName(string name)
    {
        Assert.False(TermWith(name).IsValid());
    }

    [Fact]
    public void Term_IsInvalid_WhenStartAndEndAreTheSameDay()
    {
        Assert.False(TermWith(start: Start, end: Start).IsValid());
    }

    [Fact]
    public void Term_IsInvalid_WhenEndIsBeforeStart()
    {
        Assert.False(TermWith(start: End, end: Start).IsValid());
    }

    [Fact]
    public void Term_IsInvalid_WhenCatchupStartIsNotBeforeTheTerm()
    {
        Assert.False(TermWith(catchupStart: Start, catchupEnd: End.AddDays(7)).IsValid());
        Assert.False(TermWith(catchupStart: Start.AddDays(1), catchupEnd: End.AddDays(7)).IsValid());
    }

    [Fact]
    public void Term_IsInvalid_WhenCatchupEndIsNotAfterTheTerm()
    {
        Assert.False(TermWith(catchupStart: Start.AddDays(-7), catchupEnd: End).IsValid());
        Assert.False(TermWith(catchupStart: Start.AddDays(-7), catchupEnd: End.AddDays(-1)).IsValid());
    }

    // ---- Pass.IsValid ----

    [Fact]
    public void CasualPass_IsValid_WithClassesRemainingOrExactlyUsedUp()
    {
        Assert.True(new CasualPass(1, 1, 5, Alterations(), 0).IsValid());
        Assert.True(new CasualPass(1, 1, 5, Alterations(), 5).IsValid());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CasualPass_IsInvalid_WithoutAPositiveClassCount(int classCount)
    {
        Assert.False(new CasualPass(1, 1, classCount, Alterations(), 0).IsValid());
    }

    [Fact]
    public void CasualPass_IsInvalid_WhenMoreClassesAreUsedThanItHas()
    {
        Assert.False(new CasualPass(1, 1, 5, Alterations(), 6).IsValid());
    }

    [Fact]
    public void Pass_IsInvalid_WhenAnAlterationTakesRemainingBelowZero()
    {
        // 5 classes, 3 used: removing 2 leaves exactly 0 (fine), removing 3 leaves -1 (not).
        Assert.True(new CasualPass(1, 1, 5, Alterations(-2), 3).IsValid());
        Assert.False(new CasualPass(1, 1, 5, Alterations(-3), 3).IsValid());
        Assert.False(new DatedPass(1, 1, 5, Alterations(-3), 3, Start, End).IsValid());
    }

    [Fact]
    public void Pass_IsInvalid_WhenAlterationsRemoveEveryClass()
    {
        Assert.False(new CasualPass(1, 1, 5, Alterations(-5), 0).IsValid());
    }

    [Fact]
    public void Pass_IsValid_WhenABonusAlterationCoversExtraClassesUsed()
    {
        Assert.True(new CasualPass(1, 1, 5, Alterations(2), 7).IsValid());
    }

    [Fact]
    public void DatedPass_IsValid_ForASingleDay()
    {
        Assert.True(new DatedPass(1, 1, 1, Alterations(), 0, Start, Start).IsValid());
    }

    [Fact]
    public void DatedPass_IsInvalid_WhenEndIsBeforeStart()
    {
        Assert.False(new DatedPass(1, 1, 5, Alterations(), 0, End, Start).IsValid());
    }

    [Fact]
    public void DatedPass_IsInvalid_WithoutAPositiveClassCount()
    {
        Assert.False(new DatedPass(1, 1, 0, Alterations(), 0, Start, End).IsValid());
    }

    [Fact]
    public void TermPass_IsValid_UsesTheTermClassCount()
    {
        var term = TermWith();
        Assert.True(new TermPass(1, 1, 10, Alterations(), term, new TermClassSchedule(Schedule, 10, 0)).IsValid());
        Assert.False(new TermPass(1, 1, 11, Alterations(), term, new TermClassSchedule(Schedule, 10, 0)).IsValid());
        Assert.False(new TermPass(1, 1, 0, Alterations(), term, new TermClassSchedule(Schedule, 0, 0)).IsValid());
    }

    // ---- TermPass.GetPassUsagePriority boundaries ----

    private static TermPass TermPassFor(Term term, int classesUsed = 0)
    {
        return new TermPass(1, 1, classesUsed, Alterations(), term, new TermClassSchedule(Schedule, 10, 0));
    }

    [Fact]
    public void TermPass_Priority_IsInTermFromTheFirstDayToTheLastDayInclusive()
    {
        var pass = TermPassFor(TermWith(catchupStart: Start.AddDays(-7), catchupEnd: End.AddDays(7)));

        Assert.Equal(1, pass.GetPassUsagePriority(Schedule, Start));
        Assert.Equal(1, pass.GetPassUsagePriority(Schedule, End));
    }

    [Fact]
    public void TermPass_Priority_IsCatchupAfterTheTerm_UpToTheCatchupEndInclusive()
    {
        var pass = TermPassFor(TermWith(catchupStart: Start.AddDays(-7), catchupEnd: End.AddDays(7)));

        Assert.Equal(2, pass.GetPassUsagePriority(Schedule, End.AddDays(1)));
        Assert.Equal(2, pass.GetPassUsagePriority(Schedule, End.AddDays(7)));
        Assert.Null(pass.GetPassUsagePriority(Schedule, End.AddDays(8)));
    }

    [Fact]
    public void TermPass_Priority_IsCatchupBeforeTheTerm_FromTheCatchupStartInclusive()
    {
        var pass = TermPassFor(TermWith(catchupStart: Start.AddDays(-7), catchupEnd: End.AddDays(7)));

        Assert.Equal(3, pass.GetPassUsagePriority(Schedule, Start.AddDays(-1)));
        Assert.Equal(3, pass.GetPassUsagePriority(Schedule, Start.AddDays(-7)));
        Assert.Null(pass.GetPassUsagePriority(Schedule, Start.AddDays(-8)));
    }

    [Fact]
    public void TermPass_Priority_IsNullOutsideTheTerm_WhenThereIsNoCatchup()
    {
        var pass = TermPassFor(TermWith());

        Assert.Null(pass.GetPassUsagePriority(Schedule, Start.AddDays(-1)));
        Assert.Null(pass.GetPassUsagePriority(Schedule, End.AddDays(1)));
    }

    [Fact]
    public void TermPass_Priority_IsNull_OnceEveryClassIsUsed()
    {
        Assert.Equal(1, TermPassFor(TermWith(), classesUsed: 9).GetPassUsagePriority(Schedule, Start));
        Assert.Null(TermPassFor(TermWith(), classesUsed: 10).GetPassUsagePriority(Schedule, Start));
    }

    [Fact]
    public void TermPass_InTerm_OutranksDatedAndCasualPasses()
    {
        var termPass = TermPassFor(TermWith());
        var dated = new DatedPass(2, 1, 5, Alterations(), 0, Start, End);
        var casual = new CasualPass(3, 1, 5, Alterations(), 0);

        var date = Start.AddDays(7);
        var ranked = new Pass[] { casual, dated, termPass }
            .OrderBy(p => p.GetPassUsagePriority(Schedule, date))
            .ToArray();

        Assert.Equal([termPass, dated, casual], ranked);
    }
}
