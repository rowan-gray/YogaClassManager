using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using YogaClassManager.Core.Models.Classes;

namespace YogaClassManager.Core.Models.Passes;

public partial class DatedPass : Pass
{
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(NumberOfClasses)), NotifyPropertyChangedFor(nameof(ClassesRemaining)), NotifyPropertyChangedFor(nameof(IsDepleted))]
    private int classCount;

    [ObservableProperty] private DateOnly startDate;

    [ObservableProperty] private DateOnly endDate;

    public DatedPass(int id, int studentId, int classCount, ObservableCollection<PassAlteration> alterations,
        int classesUsed, DateOnly startDate, DateOnly endDate)
        : base(id, studentId, classesUsed, alterations)
    {
        ClassCount = classCount;
        StartDate = startDate;
        EndDate = endDate;
    }

    public DatedPass(Pass pass, int classCount, DateOnly startDate, DateOnly endDate)
        : base(pass.Id, pass.StudentId, pass.ClassesUsed, pass.Alterations)
    {
        ClassCount = classCount;
        StartDate = startDate;
        EndDate = endDate;
    }

    public override string PassName => "Dated Pass";

    public override string? DateRangeDisplay => $"{StartDate:dd/MM/yyyy} - {EndDate:dd/MM/yyyy}";

    public override int NumberOfClasses => ClassCount + Alterations.Sum(a => a.Amount);

    public override bool IsExpired => DateOnly.FromDateTime(DateTime.Now) > EndDate;

    public override DateOnly? ExpiryDate => EndDate;

    public static DatedPass Copy(DatedPass datedPass)
    {
        var pass = Copy((Pass)datedPass);
        return new DatedPass(pass, datedPass.ClassCount, datedPass.StartDate, datedPass.EndDate);
    }

    public override bool IsValid()
    {
        return base.IsValid() && ClassCount > 0 && StartDate <= EndDate && NumberOfClasses > 0 &&
               ClassesRemaining >= 0;
    }

    public override int? GetPassUsagePriority(ClassSchedule classSchedule, DateOnly date)
    {
        if (ClassesRemaining <= 0)
            return null;

        return StartDate <= date && EndDate >= date ? 5 : null;
    }

    public override string ToString()
    {
        return $"Dated Pass ({StartDate:dd/MM/yy} to {EndDate:dd/MM/yy})";
    }
}
