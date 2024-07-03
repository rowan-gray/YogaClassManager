namespace YogaClassManager.NewModels;

public interface IValidationRule<T>
{
    string ValidationMessage { get; set; }
    bool Check(T value);
}