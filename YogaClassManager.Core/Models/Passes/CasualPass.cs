using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using YogaClassManager.Core.Models.Classes;

namespace YogaClassManager.Core.Models.Passes;

public partial class CasualPass : Pass
{
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(NumberOfClasses)), NotifyPropertyChangedFor(nameof(ClassesRemaining)), NotifyPropertyChangedFor(nameof(IsDepleted))]
    private int classCount;

    public CasualPass(int id, int studentId, int classCount, ObservableCollection<PassAlteration> alterations,
        int classesUsed)
        : base(id, studentId, classesUsed, alterations)
    {
        ClassCount = classCount;
    }

    public CasualPass(Pass pass, int classCount)
        : base(pass.Id, pass.StudentId, pass.ClassesUsed, pass.Alterations)
    {
        ClassCount = classCount;
    }

    public override string PassName => "Casual Pass";

    public override int NumberOfClasses => ClassCount + Alterations.Sum(a => a.Amount);

    public static CasualPass Copy(CasualPass casualPass)
    {
        var pass = Copy((Pass)casualPass);
        return new CasualPass(pass, casualPass.ClassCount);
    }

    public override bool IsValid()
    {
        return base.IsValid() && ClassCount > 0 && NumberOfClasses > 0 && ClassesRemaining >= 0;
    }

    public override int? GetPassUsagePriority(ClassSchedule classSchedule, DateOnly date)
    {
        return ClassesRemaining <= 0 ? null : 10;
    }

    public override string ToString()
    {
        return NumberOfClasses == 1 ? "Casual Pass (1 class)" : $"Casual Pass ({NumberOfClasses} classes)";
    }
}
