using CommunityToolkit.Mvvm.ComponentModel;

namespace YogaClassManager.NewModels.People;
#nullable enable


public partial class Person : ObservableObject
{
    [ObservableProperty] private int id;

    [ObservableProperty] private bool isActive;

    public Person() : this(-1, "", null, null, null, true)
    {
    }
    
    public Person(int id, string firstName, string? lastName, string? phoneNumber, string? email, bool isActive)
    {
        Id = id;
        FirstNameValidation = new ValidatableObject<string>(firstName, new List<IValidationRule<string>>(),
            NotifyFullNameChanged);
        LastNameValidation  = new ValidatableObject<string?>(lastName, new List<IValidationRule<string?>>(),
            NotifyFullNameChanged);
        PhoneNumberValidation  = new ValidatableObject<string?>(phoneNumber);
        EmailValidation  = new ValidatableObject<string?>(email);
        IsActive = isActive;
    }

    public string FullName => $"{FirstName} {LastName}";

    public ValidatableObject<string> FirstNameValidation { get; init; }
    
    public string FirstName
    {
        set => FirstNameValidation.Value = value;
        get => FirstNameValidation.Value;
    }

    public ValidatableObject<string?> LastNameValidation { get; init; }
    
    public string? LastName
    {
        set => LastNameValidation.Value = value;
        get => LastNameValidation.Value;
    }

    private ValidatableObject<string?> PhoneNumberValidation { get; }
    
    public string? PhoneNumber
    {
        set => PhoneNumberValidation.Value = value;
        get => PhoneNumberValidation.Value;
    }

    private ValidatableObject<string?> EmailValidation { get; }
    
    public string? Email
    {
        set => EmailValidation.Value = value;
        get => EmailValidation.Value;
    }

    public void NotifyFullNameChanged()
    {
        OnPropertyChanged(FullName);
    }

    public static Person Copy(Person person)
    {
        return new Person(person.Id, person.FirstName, person.LastName, person.PhoneNumber, person.Email, true);
    }
}