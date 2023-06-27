using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YogaClassManager.Models.People;

namespace YogaClassManager.Database.Models
{
    internal class EmergencyContactMapping
    {
        public int PersonId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string PhoneNumber { get; set; } 
        public string Email { get; set; } 
        public bool IsActive { get; set; }
        public int StudentId { get; set; }
        public int Relationship { get; set; }

        public EmergencyContact ToEmergencyContact()
        {
            Person person = new(PersonId, FirstName, LastName, PhoneNumber, Email, IsActive);
            return new(person, StudentId, (Relationship)Relationship);
        }
    }
}
