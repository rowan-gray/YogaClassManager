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

        // A valid phone number is stored normalised ("0412 345 678" -> "0412345678"); an invalid one is
        // left as typed.
        var phoneNumberValid = true;
        if (PhoneNumber is not null)
        {
            phoneNumberValid = AustralianPhoneNumber.TryNormalise(PhoneNumber, out var normalised);
            PhoneNumber = normalised;
        }

        return FirstName.Length > 0
               && phoneNumberValid
               && (Email is null || EmailFormat().IsMatch(Email))
               && (Email is not null || PhoneNumber is not null);
    }

    [GeneratedRegex(@"^\w+([-+.']\w+)*@\w+([-.]\w+)*\.\w+([-.]\w+)*$")]
    private static partial Regex EmailFormat();

    public override string ToString()
    {
        return FullName;
    }
}
