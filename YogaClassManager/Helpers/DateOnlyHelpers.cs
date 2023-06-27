#nullable enable
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YogaClassManager.Helpers
{
    internal class DateOnlyHelpers
    {
        internal static DateOnly GetDateOnly(string date)
        {
            return DateOnly.ParseExact(date, "yyyy-MM-dd");
        }
        internal static DateOnly? GetNullableDateOnly(string? date)
        {
            if (date is null) return null;

            if (date.Trim().Length == 0) return null;

            return DateOnly.ParseExact(date, "yyyy-MM-dd");
        }
    }
}
