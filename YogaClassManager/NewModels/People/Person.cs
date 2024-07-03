using CommunityToolkit.Mvvm.ComponentModel;

namespace YogaClassManager.NewModels.People;
#nullable enable


public partial class Person : ObservableObject
{
    [ObservableProperty] private int id;

    [ObservableProperty] private bool isActive;

    public Person(int id, string firstName, string? lastName, string? phoneNumber, string? email, bool isActive)
    {
        Id = id;
        FirstName = new ValidatableObject<string>(firstName, new List<IValidationRule<string>>(),
            NotifyFullNameChanged);
        LastName = new ValidatableObject<string?>(lastName, new List<IValidationRule<string?>>(),
            NotifyFullNameChanged);
        PhoneNumber = new ValidatableObject<string?>(phoneNumber);
        Email = new ValidatableObject<string?>(email);
        IsActive = isActive;
    }

    public string FullName => $"{FirstName} {LastName}";

    public ValidatableObject<string> FirstName { get; init; }

    public ValidatableObject<string?> LastName { get; init; }

    private ValidatableObject<string?> PhoneNumber { get; }

    private ValidatableObject<string?> Email { get; }

    public void NotifyFullNameChanged()
    {
        OnPropertyChanged(FullName);
    }

    public static Person Copy(Person person)
    {
        return new Person(person.Id, person.FirstName, person.LastName, person.PhoneNumber, person.Email, true);
    }

    // internal virtual bool Validate()
    // {
    //     FirstName = FirstName.Trim();
    //     LastName = LastName?.Trim();
    //     LastName = LastName == "" ? null : LastName;
    //     PhoneNumber = PhoneNumber?.Trim();
    //     PhoneNumber = PhoneNumber == "" ? null : PhoneNumber;
    //     Email = Email?.Trim();
    //     Email = Email == "" ? null : Email;
    //     return FirstName is not null && FirstName.Length > 0 && (PhoneNumber is null
    //         || Regex.IsMatch(PhoneNumber is null ? "" : PhoneNumber, @"^\d{10}$|^\d{8}$"))
    //         && (Email is null
    //         || Regex.IsMatch(Email is null ? "" : Email, @"^\w+([-+.']\w+)*@\w+([-.]\w+)*\.\w+([-.]\w+)*$"))
    //         && (Email is not null || PhoneNumber is not null);
    // }
}