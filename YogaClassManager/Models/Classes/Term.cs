using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace YogaClassManager.Models.Classes
{
    public partial class Term : ObservableObject, IUpdateable<Term>, IIdentifiable
    {
        public Term(int id, string name, DateOnly startDate, DateOnly endDate, DateOnly? catchupStartDate, DateOnly? catchupEndDate, List<TermClassSchedule> classes)
        {
            Id = id;
            Name = name;
            StartDate = startDate;
            EndDate = endDate;
            CatchupStartDate = catchupStartDate;
            CatchupEndDate = catchupEndDate;
            Classes = new(classes);
        }

        [ObservableProperty]
        private int id;
        [ObservableProperty]
        private string name;
        [ObservableProperty]
        private DateOnly startDate;
        [ObservableProperty]
        private DateOnly endDate;
        [ObservableProperty]
        private DateOnly? catchupStartDate;
        [ObservableProperty]
        private DateOnly? catchupEndDate;
        [ObservableProperty]
        private ObservableCollection<TermClassSchedule> classes;

        public static Term Copy(Term term)
        {
            return (Term)term.MemberwiseClone();
        }

        public override string ToString()
        {
            return $"{Name}: {StartDate}-{EndDate}";
        }

        public bool IsValid()
        {
            return StartDate < EndDate && (CatchupStartDate is null || CatchupStartDate < StartDate) && (CatchupEndDate is null || CatchupEndDate > EndDate) && Name is not null && Name?.Trim().Length != 0;
        }

        public void Update(Term updatedData)
        {
            Id = updatedData.Id;
            Name = updatedData.Name;
            StartDate = updatedData.StartDate;
            EndDate = updatedData.EndDate;
            CatchupStartDate = updatedData.CatchupStartDate;
            CatchupEndDate = updatedData.CatchupEndDate;
            Classes.Clear();
            foreach (var classSchedule in updatedData.Classes)
            {
                Classes.Add(classSchedule);
            }
        }
    }
}