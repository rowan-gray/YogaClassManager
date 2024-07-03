#nullable enable

using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;

namespace YogaClassManager.Models.People;

public partial class Person : ObservableObject, IIdentifiable, IUpdateable<Person>
{
    [ObservableProperty] private string? email;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(FullName))]
    private string firstName = "";

    [ObservableProperty] private int id;

    [ObservableProperty] private bool isActive;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(FullName))]
    private string? lastName;

    [ObservableProperty] private string? phoneNumber;

    public Person(int id, string firstName, string? lastName, string? phoneNumber, string? email, bool isActive)
    {
        //TODO verify phoneNumber and email
        Id = id;
        FirstName = firstName;
        LastName = lastName;
        PhoneNumber = phoneNumber;
        Email = email;
        IsActive = isActive;
    }

    public Person(int id, Person person)
    {
        Id = id;
        FirstName = person.FirstName;
        LastName = person.LastName;
        PhoneNumber = person.PhoneNumber;
        Email = person.Email;
        IsActive = person.IsActive;
    }

    public string FullName => $"{FirstName} {LastName}";

    public void Update(Person person)
    {
        if (person is null)
            return;

        if (FirstName != person.FirstName)
            FirstName = person.FirstName;
        if (LastName != person.LastName)
            LastName = person.LastName;
        if (Email != person.Email)
            Email = person.Email;
        if (PhoneNumber != person.PhoneNumber)
            PhoneNumber = person.PhoneNumber;
        if (IsActive != person.IsActive)
            IsActive = person.IsActive;
    }

    public static Person Copy(Person person)
    {
        return new Person(person.Id, person.FirstName, person.LastName, person.PhoneNumber, person.Email, true);
    }

    internal virtual bool Validate()
    {
        FirstName = FirstName.Trim();
        LastName = LastName?.Trim();
        LastName = LastName == "" ? null : LastName;
        PhoneNumber = PhoneNumber?.Trim();
        PhoneNumber = PhoneNumber == "" ? null : PhoneNumber;
        Email = Email?.Trim();
        Email = Email == "" ? null : Email;
        return FirstName is not null && FirstName.Length > 0 && (PhoneNumber is null
                                                                 || Regex.IsMatch(
                                                                     PhoneNumber is null ? "" : PhoneNumber,
                                                                     @"^\d{10}$|^\d{8}$"))
               && (Email is null
                   || Regex.IsMatch(Email is null ? "" : Email, @"^\w+([-+.']\w+)*@\w+([-.]\w+)*\.\w+([-.]\w+)*$"))
               && (Email is not null || PhoneNumber is not null);
    }
}