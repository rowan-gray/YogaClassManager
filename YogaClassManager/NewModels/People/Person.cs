using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using YogaClassManager.Models;


namespace YogaClassManager.NewModels.People;
#nullable enable


public partial class Person : ObservableValidatableObject
{
    [RegularExpression(@"^\w+([-+.']\w+)*@\w+([-.]\w+)*\.\w+([-.]\w+)*$")]
    [ObservableProperty] [Required] private string? email;

    [Required]
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(FullName))]
    private string firstName = "";

    [ObservableProperty] private int id;

    [ObservableProperty] private bool isActive;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(FullName))]
    private string? lastName;

    [RegularExpression(@"^\d{10}$|^\d{8}$")]
    [ObservableProperty] private string? phoneNumber;

    public Person() : this(-1, "", null, null, null, true)
    {
    }
    
    public Person(int id, string firstName, string? lastName, string? phoneNumber, string? email, bool isActive)
    {
        Id = id;
        FirstName = firstName;
        LastName = lastName;
        PhoneNumber = phoneNumber;
        Email = email;
        IsActive = isActive;
    }

    public string FullName => $"{FirstName} {LastName}";
    
    public void NotifyFullNameChanged()
    {
        OnPropertyChanged(FullName);
    }

    public static Person Copy(Person person)
    {
        return new Person(person.Id, person.FirstName, person.LastName, person.PhoneNumber, person.Email, true);
    }
}