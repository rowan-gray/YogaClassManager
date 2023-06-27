#nullable enable 

using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using YogaClassManager.Models.Passes;

namespace YogaClassManager.Models.People
{
    public partial class Student : Person, IIdentifiable, IUpdateable<Student>
    {
        public Student(int id, string firstName, string lastName, string? phoneNumber, string? email,
            ObservableCollection<Pass> passes, ObservableCollection<EmergencyContact> emergencyContacts, 
            ObservableCollection<string> healthConcerns, bool isActive)
                    : base(id, firstName, lastName, phoneNumber, email, isActive)
        {
            Passes = passes;
            EmergencyContacts = emergencyContacts;
            HealthConcerns = healthConcerns;
        }
        public Student(Person person, ObservableCollection<Pass> passes, ObservableCollection<EmergencyContact> emergencyContacts, 
            ObservableCollection<string> healthConcerns)
                    : base(person.Id, person.FirstName, person.LastName, person.PhoneNumber, person.Email, person.IsActive)
        {
            Passes = passes;
            EmergencyContacts = emergencyContacts;
            HealthConcerns = healthConcerns;
        }

        [ObservableProperty]
        private ObservableCollection<Pass>? passes;
        [ObservableProperty]
        private ObservableCollection<EmergencyContact>? emergencyContacts;
        [ObservableProperty]
        private ObservableCollection<string>? healthConcerns;


        public ObservableCollection<Pass>? ObservablePasses { get => passes; }
        public ObservableCollection<EmergencyContact>? ObservableEmergencyContacts { get => emergencyContacts; }
        public ObservableCollection<string>? ObservableHealthConcerns { get => healthConcerns; }

        public override string ToString()
        {
            return $"{FirstName} {LastName} - Email: {Email}, Phone Number {PhoneNumber}, Passes {Passes}, Emergency Contacts: {EmergencyContacts}, Health Concerns {HealthConcerns}";
        }

        public static Student Copy(Student student)
        {
            if (student is null)
            {
                throw new ArgumentException(nameof(student));
            }
            
            var person = Person.Copy(student);

#pragma warning disable CS8604
            return new Student(person,
                               student.Passes is not null ? new(student.Passes) : null,
                               student.EmergencyContacts is not null ? new(student.EmergencyContacts) : null,
                               student.HealthConcerns is not null ? new(student.HealthConcerns) : null);
#pragma warning restore CS8604
        }

        internal override bool Validate()
        {
            return base.Validate() && LastName is not null && LastName.Length > 0;
        }

        public void Update(Student student)
        {
            base.Update((Person)student);

            if (student.Passes is not null)
                Passes = student.Passes;
            if (student.EmergencyContacts is not null)
                EmergencyContacts = student.EmergencyContacts;
            if (student.HealthConcerns is not null)
                HealthConcerns = student.HealthConcerns;
        }
    }
}
