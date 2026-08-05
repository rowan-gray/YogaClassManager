using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;

namespace YogaClassManager.Core.Models.People;

public partial class Identity : ObservableObject, IIdentifiable, IUpdateable<Identity>
{
    [ObservableProperty] private int id;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(FullName))]
    private string firstName = "";

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(FullName))]
    private string? lastName;

    [ObservableProperty] private string? phoneNumber;

    [ObservableProperty] private string? email;

    [ObservableProperty] private bool isActive;

    public Identity()
        : this(-1, "", null, null, null, true)
    {
    }

    public Identity(int id, string firstName, string? lastName, string? phoneNumber, string? email, bool isActive)
    {
        Id = id;
        FirstName = firstName;
        LastName = lastName;
        PhoneNumber = phoneNumber;
        Email = email;
        IsActive = isActive;
    }

    public Identity(int id, Identity identity)
    {
        Id = id;
        FirstName = identity.FirstName;
        LastName = identity.LastName;
        PhoneNumber = identity.PhoneNumber;
        Email = identity.Email;
        IsActive = identity.IsActive;
    }

    public string FullName => LastName is null ? FirstName : $"{FirstName} {LastName}";

    public virtual void Update(Identity identity)
    {
        if (FirstName != identity.FirstName)
            FirstName = identity.FirstName;
        if (LastName != identity.LastName)
            LastName = identity.LastName;
        if (Email != identity.Email)
            Email = identity.Email;
        if (PhoneNumber != identity.PhoneNumber)
            PhoneNumber = identity.PhoneNumber;
        if (IsActive != identity.IsActive)
            IsActive = identity.IsActive;
    }

    public static Identity Copy(Identity identity)
    {
        return new Identity(identity.Id, identity.FirstName, identity.LastName, identity.PhoneNumber, identity.Email,
            identity.IsActive);
    }

    public virtual bool Validate()
    {
        FirstName = FirstName.Trim();
        LastName = LastName?.Trim();
        LastName = LastName == "" ? null : LastName;
        PhoneNumber = PhoneNumber?.Trim();
        PhoneNumber = PhoneNumber == "" ? null : PhoneNumber;
        Email = Email?.Trim();
        Email = Email == "" ? null : Email;

        return FirstName.Length > 0
               && (PhoneNumber is null || Regex.IsMatch(PhoneNumber, @"^\d{10}$|^\d{8}$"))
               && (Email is null || Regex.IsMatch(Email, @"^\w+([-+.']\w+)*@\w+([-.]\w+)*\.\w+([-.]\w+)*$"))
               && (Email is not null || PhoneNumber is not null);
    }

    public override string ToString()
    {
        return FullName;
    }
}
