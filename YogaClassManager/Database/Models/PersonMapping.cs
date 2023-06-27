using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YogaClassManager.Models.People;

namespace YogaClassManager.Database.Models
{
    internal class PersonMapping
    {
        public int PersonId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string PhoneNumber { get; set; }
        public string Email { get; set; }
        public bool IsActive { get; set; }
        public Person ToPerson()
        {
            return new Person(PersonId, FirstName, LastName, PhoneNumber, Email, IsActive);
        }
    }
}
