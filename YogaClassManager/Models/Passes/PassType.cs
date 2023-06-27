namespace YogaClassManager.Models.Passes
{
    public class PassType
    {
        public PassType(int id, string name, int classCount)
        {
            Id = id;
            Name = name;
            ClassCount = classCount;
        }

        public int Id { get; private set; }
        public string Name { get; private set; }
        public int ClassCount { get; private set; }

        public PassType Copy()
        {
            return (PassType)this.MemberwiseClone();
        }
    }
}