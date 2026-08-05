using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using YogaClassManager.Core.Models.Classes;

namespace YogaClassManager.Core.Models.Passes;

public partial class TermPass : Pass
{
    [ObservableProperty] private Term term;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(NumberOfClasses)), NotifyPropertyChangedFor(nameof(ClassesRemaining)), NotifyPropertyChangedFor(nameof(IsDepleted))]
    private TermClassSchedule termClassSchedule;

    public TermPass(int id, int studentId, int classesUsed, ObservableCollection<PassAlteration> alterations,
        Term term, TermClassSchedule termClassSchedule)
        : base(id, studentId, classesUsed, alterations)
    {
        this.term = term;
        this.termClassSchedule = termClassSchedule;
    }

    public TermPass(Pass pass, Term term, TermClassSchedule termClassSchedule)
        : base(pass.Id, pass.StudentId, pass.ClassesUsed, pass.Alterations)
    {
        this.term = term;
        this.termClassSchedule = termClassSchedule;
    }

    public override string PassName => "Term Pass";

    public override int NumberOfClasses => TermClassSchedule.ClassCount + Alterations.Sum(a => a.Amount);

    public override bool IsExpired
    {
        get
        {
            var date = DateOnly.FromDateTime(DateTime.Now);
            return date > (Term.CatchupEndDate ?? Term.EndDate);
        }
    }

    public override DateOnly? ExpiryDate => Term.CatchupEndDate ?? Term.EndDate;

    public static TermPass Copy(TermPass termPass)
    {
        var pass = Copy((Pass)termPass);
        return new TermPass(pass, termPass.Term, termPass.TermClassSchedule);
    }

    public override bool IsValid()
    {
        return base.IsValid() && NumberOfClasses > 0 && ClassesRemaining >= 0;
    }

    public override int? GetPassUsagePriority(ClassSchedule classSchedule, DateOnly date)
    {
        if (!TermClassSchedule.ClassSchedule.Equals(classSchedule)) return null;
        if (ClassesRemaining <= 0) return null;

        if (Term.StartDate <= date && Term.EndDate >= date) return 1;
        if (Term.EndDate <= date && Term.CatchupEndDate >= date) return 2;
        if (Term.CatchupStartDate <= date && Term.StartDate >= date) return 3;

        return null;
    }

    public override string ToString()
    {
        return $"Term Pass ({Term.Name} - {TermClassSchedule.ClassSchedule})";
    }
}
