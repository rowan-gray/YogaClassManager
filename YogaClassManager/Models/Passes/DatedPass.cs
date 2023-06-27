#nullable enable

using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using YogaClassManager.Models.Classes;

namespace YogaClassManager.Models.Passes
{
    public partial class DatedPass : Pass
    {
        public DatedPass(int id, int studentId, int classCount, ObservableCollection<PassAlteration> alterations, int classesUsed, DateOnly startDate, DateOnly endDate)
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

        public override string PassName { get => "Dated Pass"; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(NumberOfClasses))]
        private int classCount;
        public override int NumberOfClasses
        {
            get
            {
                var classCount = ClassCount;
                foreach (var alteration in Alterations)
                {
                    classCount += alteration.Amount;
                }
                return classCount;
            }
        }
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }

        public static DatedPass Copy(DatedPass datedPass)
        {
            var pass = Pass.Copy(datedPass);

            return new(pass, datedPass.ClassCount, datedPass.StartDate, datedPass.EndDate);
        }

        public override bool IsValid()
        {
            return base.IsValid() && ClassCount > 0 && StartDate <= EndDate && NumberOfClasses > 0 && ClassesRemaining >= 0;
        }

        public override string ToString()
        {
            return $"Dated Pass ({StartDate.ToString("dd/MM/yy")} to {EndDate.ToString("dd/MM/yy")})";
        }

        public override int? GetPassUsagePriority(ClassSchedule classSchedule, DateOnly date)
        {
            if (ClassesRemaining <= 0)
                return null;

            if (StartDate <= date && EndDate >= date)
            {
                return 5;
            }
            else
            {
                return null;
            }
        }

        public override bool IsExpired 
        {
            get
            {
                var date = DateOnly.FromDateTime(DateTime.Now);
                return date > EndDate;
            }
        }
    }
}
