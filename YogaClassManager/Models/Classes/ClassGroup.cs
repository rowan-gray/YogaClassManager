namespace YogaClassManager.Models.Classes;

public class ClassGroup
{
    public ClassGroup(int id, string name, List<ClassSchedule> classes)
    {
        Id = id;
        Name = name;
        Classes = classes;
    }

    public int Id { get; }
    public string Name { get; set; }
    public List<ClassSchedule> Classes { get; set; }

    public static ClassGroup Copy(ClassGroup classGroup)
    {
        return new ClassGroup(classGroup.Id, classGroup.Name, CopyList(classGroup.Classes));
    }

    private static List<ClassSchedule> CopyList(List<ClassSchedule> values)
    {
        List<ClassSchedule> list = new();
        foreach (var item in values) list.Add(ClassSchedule.Copy(item));

        return list;
    }
}