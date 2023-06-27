using YogaClassManager.Models.Passes;

namespace YogaClassManager.Models.People
{
    public class ClassRollEntry
    {
        public ClassRollEntry(Student student, Pass pass)
        {
            Student = student;
            Pass = pass;
        }

        public Student Student { get; }
        public Pass Pass { get; set; }
    }
}
