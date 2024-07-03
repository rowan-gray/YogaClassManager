using CommunityToolkit.Mvvm.ComponentModel;

namespace YogaClassManager.NewModels;

public class ValidatableObject<T> : ObservableObject
{
    private readonly Action propertyChangedAction;
    private IEnumerable<string> errors;
    private bool isValid;
    private T value;

    public ValidatableObject(T value)
        : this(value, new List<IValidationRule<T>>(), () => { })
    {
    }

    public ValidatableObject(T value, IEnumerable<IValidationRule<T>> validations)
        : this(value, validations, () => { })
    {
    }


    public ValidatableObject(T value, IEnumerable<IValidationRule<T>> validations, Action propertyChangedAction)
    {
        Validations = validations;
        this.value = value;
        this.propertyChangedAction = propertyChangedAction;
        isValid = true;
        errors = Enumerable.Empty<string>();
    }

    public IEnumerable<IValidationRule<T>> Validations { get; init; }

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

    public T Value
    {
        get => value;
        set
        {
            SetProperty(ref this.value, value);
            Validate();
            propertyChangedAction();
        }
    }

    public bool Validate()
    {
        Errors = Validations
                     ?.Where(v => !v.Check(Value))
                     .Select(v => v.ValidationMessage)
                     .ToArray()
                 ?? Enumerable.Empty<string>();
        IsValid = !Errors.Any();
        return IsValid;
    }

    public override string ToString()
    {
        return Value.ToString();
    }

    public static implicit operator T(ValidatableObject<T> o)
    {
        return o.Value;
    }
}