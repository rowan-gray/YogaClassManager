using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YogaClassManager.Models.Passes;

namespace YogaClassManager.Database.Models
{
    internal class PassAlterationsMapping
    {
        public int PassAlterationId { get; set; }
        public int PassId { get; set; }
        public int AlterationCount { get; set; }
        public string AlterationReason { get; set; }

        internal PassAlteration ToPassAlteration()
        {
            return new(PassAlterationId, PassId, AlterationCount, AlterationReason);
        }
    }
}
