using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YogaClassManager.Helpers;
using YogaClassManager.Models.Classes;

namespace YogaClassManager.Database.Models
{
    internal class TermClassScheduleMapping
    {
        public int ClassId { get; set; }
        public int Day { get; set; }
        public int Time { get; set; }
        public bool IsActive { get; set; }
        public int ClassCount { get; set; }
        public int Uses { get; set; }

        public TermClassSchedule ToTermClassSchedule()
        {
            ClassSchedule classSchedule = new ClassSchedule(ClassId, (DayOfWeek)Day, TimeOnlyHelpers.GetTimeOnlyFromMinutes(Time), IsActive);
            return new TermClassSchedule(classSchedule, ClassCount, Uses);
        }
    }
}
