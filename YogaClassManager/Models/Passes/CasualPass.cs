using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YogaClassManager.Models.Classes;

namespace YogaClassManager.Models.Passes
{
    public partial class CasualPass : Pass
    {
        public CasualPass(int id, int studentId, int classCount, ObservableCollection<PassAlteration> alterations, int classesUsed)
            : base(id, studentId, classesUsed, alterations)
        {
            ClassCount = classCount;
        }

        public CasualPass(Pass pass, int classCount)
            : base(pass.Id, pass.StudentId, pass.ClassesUsed, pass.Alterations)
        {
            ClassCount = classCount;
        }

        public override string PassName { get => "Casual Pass"; }

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

        public static CasualPass Copy(CasualPass casualPass)
        {
            var pass = Pass.Copy(casualPass);

            return new(pass, casualPass.ClassCount);
        }

        public override bool IsValid()
        {
            return base.IsValid() && ClassCount > 0 && NumberOfClasses > 0 && ClassesRemaining >= 0;
        }

        public override string ToString()
        {
            if (NumberOfClasses == 1)
            {
                return $"Casual Pass ({NumberOfClasses} class)";
            }
            else
            {
                return $"Casual Pass ({NumberOfClasses} classes)";
            }
        }

        public override int? GetPassUsagePriority(ClassSchedule classSchedule, DateOnly date)
        {
            if (ClassesRemaining <= 0)
                return null;

            return 10;
        }

        public override bool IsExpired => base.IsExpired;
    }
}
