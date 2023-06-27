using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using YogaClassManager.Models.Classes;
using YogaClassManager.Models.Passes;

namespace YogaClassManager.Models.Passes
{
    public partial class Pass : ObservableObject, IUpdateable<Pass>, IIdentifiable
    {
        public Pass(int id, int studentId, int classesUsed, ObservableCollection<PassAlteration> alterations)
        {
            Id = id;
            StudentId = studentId;
            ClassesUsed = classesUsed;
            Alterations = alterations;
            Alterations.CollectionChanged += AlterationsChanged;
        }

        private void AlterationsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(ClassesRemaining));
        }

        public int Id { get; set; }
        public int StudentId { get; set; }
        public virtual int NumberOfClasses { get; set; }
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ClassesRemaining))]
        public int classesUsed;
        [ObservableProperty]
        private ObservableCollection<PassAlteration> alterations;
        public virtual bool IsExpired => false;

        public int ClassesRemaining => NumberOfClasses - ClassesUsed;
        public virtual string PassName { get; }

        public static Pass Copy(Pass pass)
        {
            ObservableCollection<PassAlteration> alterationsCopy = new();

            foreach (var alteration in pass.Alterations)
            {
                alterationsCopy.Add(PassAlteration.Copy(alteration));
            }

            return new(pass.Id, pass.StudentId, pass.ClassesUsed, alterationsCopy);
        }

        public virtual bool IsValid()
        {
            return true;
        }

        public virtual int? GetPassUsagePriority(ClassSchedule classSchedule, DateOnly date)
        {
            return null;
        }

        public void Update(Pass updatedData)
        {
            throw new NotImplementedException();
        }
    }
}