#nullable enable
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using YogaClassManager.Models.Classes;

namespace YogaClassManager.Models.Passes
{
    public partial class TermPass : Pass
    {
        public TermPass(int id, int studentId, int classesUsed, ObservableCollection<PassAlteration> alterations, Term term, TermClassSchedule termClassSchedule)
                    : base(id, studentId, classesUsed, alterations)
        {
            Term = term;
            TermClassSchedule = termClassSchedule;
        }

        public TermPass(Pass pass, Term term, TermClassSchedule termClassSchedule)
                    : base(pass.Id, pass.StudentId, pass.ClassesUsed, pass.Alterations)
        {
            Term = term;
            TermClassSchedule = termClassSchedule;
        }
        public override string PassName { get => "Term Pass"; }
        public override int NumberOfClasses
        {
            get
            {
                if (TermClassSchedule == null)
                {
                    return 0;
                }
                
                var classCount = TermClassSchedule.ClassCount;
                foreach (var alteration in Alterations)
                {
                    classCount += alteration.Amount;
                }
                return classCount;
            }
        }

        [ObservableProperty]
        private Term? _term;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(NumberOfClasses))]
        private TermClassSchedule? _termClassSchedule;

        public static TermPass Copy(TermPass termPass)
        {
            var pass = Pass.Copy(termPass);

            return new(pass, termPass.Term, termPass.TermClassSchedule);
        }

        public override bool IsValid()
        {
            return base.IsValid() && Term != null && TermClassSchedule != null && NumberOfClasses > 0 && ClassesRemaining >= 0;
        }

        public override string ToString()
        {
            return $"Term Pass ({Term.Name} - {TermClassSchedule.ClassSchedule})";
        }

        public override int? GetPassUsagePriority(ClassSchedule classSchedule, DateOnly date)
        {
            if (!TermClassSchedule.ClassSchedule.Equals(classSchedule))
            {
                return null;
            }

            if (ClassesRemaining <= 0)
                return null;

            if (Term.StartDate <= date && Term.EndDate >= date)
            {
                return 1;
            }

            if (Term.EndDate <= date && Term.CatchupEndDate >= date)
            {
                return 2;
            }

            if (Term.CatchupStartDate <= date && Term.StartDate >= date)
            {
                return 3;
            }

            return null;
        }

        public override bool IsExpired
        {
            get {
                var date = DateOnly.FromDateTime(DateTime.Now);

                if (date > Term.EndDate && Term.CatchupEndDate is null || date > Term.CatchupEndDate)
                {
                    return true;
                }

                return false;
            }
        }
    }
}
