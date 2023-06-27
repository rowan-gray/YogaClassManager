#nullable enable


namespace YogaClassManager.Models.People
{
    public class EmergencyContact : Person
    {
        public EmergencyContact(int id, string firstName, string lastName, string phoneNumber, string? email, int studentId, Relationship relationship) : base(id, firstName, lastName, phoneNumber, email, true)
        {
            StudentId = studentId;
            Relationship = relationship;
        }

        public EmergencyContact(Person person, int studentId, Relationship relationship) : base(person.Id, person.FirstName, person.LastName, person.PhoneNumber, person.Email, true)
        {
            StudentId = studentId;
            Relationship = relationship;
        }

        public int StudentId { get; internal set; }
        public Relationship Relationship { get; set; }

        public static EmergencyContact Copy(EmergencyContact emergencyContact)
        {
            var person = Person.Copy(emergencyContact);

            return new(person, emergencyContact.StudentId, emergencyContact.Relationship);
        }
    }
}
