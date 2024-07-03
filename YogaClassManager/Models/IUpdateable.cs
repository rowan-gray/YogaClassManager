namespace YogaClassManager.Models;

public interface IUpdateable<T>
{
    public void Update(T updatedData);
}