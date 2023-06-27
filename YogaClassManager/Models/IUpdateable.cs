namespace YogaClassManager.Models
{
    public interface IUpdateable<T>
    {
        public abstract void Update(T updatedData);
    }
}
