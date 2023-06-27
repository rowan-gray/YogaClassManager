namespace YogaClassManager.Models.Classes
{
    public class TermClassSchedule
    {
        public TermClassSchedule(ClassSchedule classSchedule, int classCount, int uses)
        {
            ClassSchedule = classSchedule;
            ClassCount = classCount;
            Uses = uses;
        }

        public ClassSchedule ClassSchedule { get; }
        public int ClassCount { get; set; }
        public int Uses { get; set; }

        public override string ToString()
        {
            return $"{ClassSchedule}: {ClassCount} classes";
        }
    }
}
