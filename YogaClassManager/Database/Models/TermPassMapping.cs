using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YogaClassManager.Database.Models
{
    internal class TermPassMapping
    {
        public int PassId { get; set; }
        public int TermId { get; set; }
        public int StudentId { get; set; }
        public int ClassId { get; set; }
        public int TimesUsed { get; set; }
    }
}
