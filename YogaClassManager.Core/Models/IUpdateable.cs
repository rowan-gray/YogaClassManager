namespace YogaClassManager.Core.Models;

public interface IUpdateable<in T>
{
    void Update(T updatedData);
}
