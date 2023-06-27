using YogaClassManager.Models.People;

namespace YogaClassManager.Models.Classes
{
    public class ClassRoll
    {
        public ClassRoll(int id, DateOnly date, ClassSchedule classSchedule, List<ClassRollEntry> students)
        {
            Id = id;
            this.Date = date;
            ClassSchedule = classSchedule;
            StudentEntries = students;
        }

        public int Id { get; }
        public DateOnly Date { get; set; }
        public ClassSchedule ClassSchedule { get; set; }

        public List<ClassRollEntry> StudentEntries { get; set; }
        public void setClassSchedule(ClassSchedule classSchedule)
        {
            ClassSchedule = classSchedule;
        }

        public override string ToString()
        {
            return $"{ClassSchedule} ({Date})";
        }
    }
}
