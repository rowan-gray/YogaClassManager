#nullable enable

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using YogaClassManager.Models.Passes;

namespace YogaClassManager.Models.People;

public partial class Student : Person, IIdentifiable, IUpdateable<Student>
{
    [ObservableProperty] private ObservableCollection<EmergencyContact>? emergencyContacts;

    [ObservableProperty] private ObservableCollection<string>? healthConcerns;

    [ObservableProperty] private ObservableCollection<Pass>? passes;

    public Student(int id, string firstName, string lastName, string? phoneNumber, string? email,
        ObservableCollection<Pass> passes, ObservableCollection<EmergencyContact> emergencyContacts,
        ObservableCollection<string> healthConcerns, bool isActive)
        : base(id, firstName, lastName, phoneNumber, email, isActive)
    {
        Passes = passes;
        EmergencyContacts = emergencyContacts;
        HealthConcerns = healthConcerns;
    }

    public Student(Person person, ObservableCollection<Pass> passes,
        ObservableCollection<EmergencyContact> emergencyContacts,
        ObservableCollection<string> healthConcerns)
        : base(person.Id, person.FirstName, person.LastName, person.PhoneNumber, person.Email, person.IsActive)
    {
        Passes = passes;
        EmergencyContacts = emergencyContacts;
        HealthConcerns = healthConcerns;
    }


    public ObservableCollection<Pass>? ObservablePasses => passes;
    public ObservableCollection<EmergencyContact>? ObservableEmergencyContacts => emergencyContacts;
    public ObservableCollection<string>? ObservableHealthConcerns => healthConcerns;

    public void Update(Student student)
    {
        base.Update(student);

        if (student.Passes is not null)
            Passes = student.Passes;
        if (student.EmergencyContacts is not null)
            EmergencyContacts = student.EmergencyContacts;
        if (student.HealthConcerns is not null)
            HealthConcerns = student.HealthConcerns;
    }

    public override string ToString()
    {
        return
            $"{FirstName} {LastName} - Email: {Email}, Phone Number {PhoneNumber}, Passes {Passes}, Emergency Contacts: {EmergencyContacts}, Health Concerns {HealthConcerns}";
    }

    public static Student Copy(Student student)
    {
        if (student is null) throw new ArgumentException(nameof(student));

        var person = Person.Copy(student);

#pragma warning disable CS8604
        return new Student(person,
            student.Passes is not null ? new ObservableCollection<Pass>(student.Passes) : null,
            student.EmergencyContacts is not null
                ? new ObservableCollection<EmergencyContact>(student.EmergencyContacts)
                : null,
            student.HealthConcerns is not null ? new ObservableCollection<string>(student.HealthConcerns) : null);
#pragma warning restore CS8604
    }

    internal override bool Validate()
    {
        return base.Validate() && LastName is not null && LastName.Length > 0;
    }
}