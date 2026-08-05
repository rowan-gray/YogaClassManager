using System.Collections.ObjectModel;
using YogaClassManager.Core.Models.Classes;
using YogaClassManager.Core.Models.Passes;

namespace YogaClassManager.Core.Tests;

public class PassUsagePriorityTests
{
    private static readonly ClassSchedule Schedule = new(1, DayOfWeek.Monday, new TimeOnly(9, 0), false);
    private static readonly ClassSchedule OtherSchedule = new(2, DayOfWeek.Tuesday, new TimeOnly(10, 0), false);
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Now);

    [Fact]
    public void CasualPass_ReturnsLowPriority_WhenClassesRemain()
    {
        var pass = new CasualPass(1, 1, 5, new ObservableCollection<PassAlteration>(), 0);
        Assert.Equal(10, pass.GetPassUsagePriority(Schedule, Today));
    }

    [Fact]
    public void CasualPass_ReturnsNull_WhenDepleted()
    {
        var pass = new CasualPass(1, 1, 5, new ObservableCollection<PassAlteration>(), 5);
        Assert.Null(pass.GetPassUsagePriority(Schedule, Today));
    }

    [Fact]
    public void DatedPass_ReturnsPriority_OnlyWithinDateRangeAndRemaining()
    {
        var pass = new DatedPass(1, 1, 5, new ObservableCollection<PassAlteration>(), 0, Today.AddDays(-5),
            Today.AddDays(5));
        Assert.Equal(5, pass.GetPassUsagePriority(Schedule, Today));
        Assert.Null(pass.GetPassUsagePriority(Schedule, Today.AddDays(10)));
    }

    [Fact]
    public void TermPass_ReturnsNull_ForADifferentClassSchedule()
    {
        var term = new Term(1, "Term", Today.AddDays(-10), Today.AddDays(70), null, null, []);
        var termClass = new TermClassSchedule(Schedule, 10, 0);
        var pass = new TermPass(1, 1, 0, new ObservableCollection<PassAlteration>(), term, termClass);

        Assert.Null(pass.GetPassUsagePriority(OtherSchedule, Today));
        Assert.Equal(1, pass.GetPassUsagePriority(Schedule, Today));
    }

    [Fact]
    public void TermPass_ReturnsCatchupPriorities_OutsideTermDates()
    {
        var term = new Term(1, "Term", Today.AddDays(-40), Today.AddDays(-10), Today.AddDays(-45),
            Today.AddDays(10), []);
        var termClass = new TermClassSchedule(Schedule, 10, 0);
        var pass = new TermPass(1, 1, 0, new ObservableCollection<PassAlteration>(), term, termClass);

        // After the term but within the catchup window.
        Assert.Equal(2, pass.GetPassUsagePriority(Schedule, Today));
    }

    [Fact]
    public void CasualPass_HasNoExpiryDate()
    {
        var pass = new CasualPass(1, 1, 5, new ObservableCollection<PassAlteration>(), 0);
        Assert.Null(pass.ExpiryDate);
    }

    [Fact]
    public void DatedPass_ExpiryDate_MatchesEndDate()
    {
        var pass = new DatedPass(1, 1, 5, new ObservableCollection<PassAlteration>(), 0, Today.AddDays(-5),
            Today.AddDays(5));
        Assert.Equal(Today.AddDays(5), pass.ExpiryDate);
    }

    [Fact]
    public void TermPass_ExpiryDate_PrefersCatchupEndDate_OverTermEndDate()
    {
        var term = new Term(1, "Term", Today.AddDays(-40), Today.AddDays(-10), Today.AddDays(-45),
            Today.AddDays(10), []);
        var termClass = new TermClassSchedule(Schedule, 10, 0);
        var pass = new TermPass(1, 1, 0, new ObservableCollection<PassAlteration>(), term, termClass);

        Assert.Equal(Today.AddDays(10), pass.ExpiryDate);
    }

    [Fact]
    public void TermPass_ExpiryDate_FallsBackToTermEndDate_WhenNoCatchup()
    {
        var term = new Term(1, "Term", Today.AddDays(-40), Today.AddDays(-10), null, null, []);
        var termClass = new TermClassSchedule(Schedule, 10, 0);
        var pass = new TermPass(1, 1, 0, new ObservableCollection<PassAlteration>(), term, termClass);

        Assert.Equal(Today.AddDays(-10), pass.ExpiryDate);
    }
}
