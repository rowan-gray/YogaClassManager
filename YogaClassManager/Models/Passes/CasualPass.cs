using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using YogaClassManager.Models.Classes;

namespace YogaClassManager.Models.Passes;

public partial class CasualPass : Pass
{
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(NumberOfClasses))]
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

    public override int NumberOfClasses
    {
        get
        {
            var classCount = ClassCount;
            foreach (var alteration in Alterations) classCount += alteration.Amount;
            return classCount;
        }
    }

    public override bool IsExpired => base.IsExpired;

    public static CasualPass Copy(CasualPass casualPass)
    {
        var pass = Pass.Copy(casualPass);

        return new CasualPass(pass, casualPass.ClassCount);
    }

    public override bool IsValid()
    {
        return base.IsValid() && ClassCount > 0 && NumberOfClasses > 0 && ClassesRemaining >= 0;
    }

    public override string ToString()
    {
        if (NumberOfClasses == 1)
            return $"Casual Pass ({NumberOfClasses} class)";
        return $"Casual Pass ({NumberOfClasses} classes)";
    }

    public override int? GetPassUsagePriority(ClassSchedule classSchedule, DateOnly date)
    {
        if (ClassesRemaining <= 0)
            return null;

        return 10;
    }
}