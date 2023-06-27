using CommunityToolkit.Mvvm.ComponentModel;

namespace YogaClassManager.Models.Classes
{
    public partial class ClassSchedule : ObservableObject, IUpdateable<ClassSchedule>, IIdentifiable
    {
        public ClassSchedule(int id, DayOfWeek day, TimeOnly time, bool isArchived)
        {
            Id = id;
            Day = day;
            Time = time;
            IsArchived = isArchived;
        }

        [ObservableProperty]
        private int id;
        [ObservableProperty]
        private DayOfWeek day;
        [ObservableProperty]
        private TimeOnly time;
        [ObservableProperty]
        private bool isArchived;

        public static ClassSchedule Copy(ClassSchedule l)
        {
            return (ClassSchedule)l.MemberwiseClone();
        }

        public override string ToString()
        {
            return $"{Day}: {Time}";
        }

        public override bool Equals(object obj)
        {
            if (obj is null)
                return false;
            if (obj is not ClassSchedule)
                return false;

            var other = (ClassSchedule)obj;
            return other.Id == Id && other.Day == Day && other.Time == Time && other.IsArchived == IsArchived;
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode() ^ Day.GetHashCode() ^ Time.GetHashCode() ^ (IsArchived.GetHashCode() + 1);
        }

        public void Update(ClassSchedule updatedData)
        {
            Day = updatedData.Day;
            Time = updatedData.Time;
            IsArchived = updatedData.IsArchived;
        }
    }
}
