using CommunityToolkit.Mvvm.ComponentModel;

namespace YogaClassManager.Core.Models.People;

public partial class EmergencyContact : Identity
{
    [ObservableProperty] private int studentId;

    [ObservableProperty] private Relationship relationship;

    public EmergencyContact(int id, string firstName, string? lastName, string? phoneNumber, string? email,
        int studentId, Relationship relationship)
        : base(id, firstName, lastName, phoneNumber, email, true)
    {
        StudentId = studentId;
        Relationship = relationship;
    }

    public EmergencyContact(Identity identity, int studentId, Relationship relationship)
        : base(identity.Id, identity.FirstName, identity.LastName, identity.PhoneNumber, identity.Email, true)
    {
        StudentId = studentId;
        Relationship = relationship;
    }

    public static EmergencyContact Copy(EmergencyContact emergencyContact)
    {
        var identity = Copy((Identity)emergencyContact);
        return new EmergencyContact(identity, emergencyContact.StudentId, emergencyContact.Relationship);
    }
}
