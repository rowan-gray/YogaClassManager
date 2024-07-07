using CommunityToolkit.Mvvm.ComponentModel;

namespace YogaClassManager.NewModels;

public class ValidatableObject<T>(
    IEnumerable<IValidationRule<T>> validations)
    : ObservableObject
{
    private IEnumerable<string> errors = [];
    private bool isValid = true;

    public ValidatableObject()
        : this(new List<IValidationRule<T>>())
    {
    }


    public IEnumerable<IValidationRule<T>> Validations { get; init; } = validations;

    public IEnumerable<string> Errors
    {
        get => errors;
        private set => SetProperty(ref errors, value);
    }

    public bool IsValid
    {
        get => isValid;
        private set => SetProperty(ref isValid, value);
    }

    public bool Validate(T value)
    {
        Errors = Validations
                     ?.Where(v => !v.Check(value))
                     .Select(v => v.ValidationMessage)
                     .ToArray()
                 ?? Enumerable.Empty<string>();
        IsValid = !Errors.Any();
        return IsValid;
    }
}