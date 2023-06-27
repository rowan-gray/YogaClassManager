using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YogaClassManager.Models.People;

namespace YogaClassManager.Database.Models
{
    internal class StudentMapping : PersonMapping
    {
        public int StudentId { set => PersonId = value; get => PersonId; }

        public Student ToStudent()
        {
            return new Student(PersonId, FirstName, LastName, PhoneNumber, Email, null, null, null, IsActive);
        }
    }
}
