using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YogaClassManager.Database.Models
{
    internal class TermClassesMapping
    {
        public int TermId { get; set; }
        public int ClassId { get; set; }
        public int ClassCount { get; set; }
    }
}
