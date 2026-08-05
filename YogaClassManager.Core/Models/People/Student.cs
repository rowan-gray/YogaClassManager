using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using YogaClassManager.Core.Data;
using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models.Passes;
using YogaClassManager.Core.Repositories;

namespace YogaClassManager.Core.Models.People;

/// <summary>
///     Passes/EmergencyContacts are live, filter-scoped queries against the owning repository (see
///     AttachChildRepositories) rather than pre-fetched snapshots - null until attached (e.g. a freshly
///     constructed, not-yet-persisted Student has nothing to query). HealthConcerns remains a plain
///     eager collection since it has no entity/repository of its own.
/// </summary>
public partial class Student : Identity, IUpdateable<Student>
{
    [ObservableProperty] private IDbModel<Pass, PassFilter>? passes;

    [ObservableProperty] private IDbModel<EmergencyContact, EmergencyContactFilter>? emergencyContacts;

    [ObservableProperty] private ObservableCollection<string> healthConcerns = new();

    public Student()
        : this(-1, "", "", null, null, true)
    {
    }

    public Student(int id, string firstName, string lastName, string? phoneNumber, string? email, bool isActive)
        : base(id, firstName, lastName, phoneNumber, email, isActive)
    {
    }

    public Student(Identity identity)
        : base(identity.Id, identity.FirstName, identity.LastName, identity.PhoneNumber, identity.Email, identity.IsActive)
    {
    }

    /// <summary>Attaches live, StudentId-scoped queries for Passes/EmergencyContacts - called by the
    /// repository layer whenever it hands out a Student that's backed by a store row.</summary>
    public void AttachChildRepositories(IPassRepository passRepository,
        IEmergencyContactRepository emergencyContactRepository)
    {
        Passes = passRepository.Query(new PassFilter { StudentId = Id, IncludeExpired = true, IncludeDepleted = true });
        EmergencyContacts = emergencyContactRepository.Query(new EmergencyContactFilter { StudentId = Id });
    }

    public static Student Copy(Student student)
    {
        var identity = Copy((Identity)student);

        return new Student(identity)
        {
            Passes = student.Passes,
            EmergencyContacts = student.EmergencyContacts,
            HealthConcerns = new ObservableCollection<string>(student.HealthConcerns)
        };
    }

    public void Update(Student updatedData)
    {
        base.Update(updatedData);

        Passes = updatedData.Passes;
        EmergencyContacts = updatedData.EmergencyContacts;
        HealthConcerns = updatedData.HealthConcerns;
    }

    public override bool Validate()
    {
        return base.Validate() && LastName is not null && LastName.Length > 0;
    }

    public override string ToString()
    {
        return
            $"{FirstName} {LastName} - Email: {Email}, Phone Number: {PhoneNumber}, Health Concerns: {HealthConcerns.Count}";
    }
}
