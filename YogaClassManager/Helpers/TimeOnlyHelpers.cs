using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YogaClassManager.Helpers
{
    internal static class TimeOnlyHelpers
    {
        internal static TimeOnly GetTimeOnlyFromMinutes(int minutes)
        {
            return TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(minutes));
        }
    }
}
