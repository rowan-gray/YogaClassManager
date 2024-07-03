using YogaClassManager.Helpers;
using YogaClassManager.Models.Classes;

namespace YogaClassManager.Database.Models;

internal class TermMapping
{
    public int TermId { get; set; }
    public string TermName { get; set; }
    public string StartDate { get; set; }
    public string EndDate { get; set; }
    public string CatchupStartDate { get; set; }
    public string CatchupEndDate { get; set; }

    internal Term ToTerm()
    {
        return new Term(TermId, TermName, DateOnlyHelpers.GetDateOnly(StartDate), DateOnlyHelpers.GetDateOnly(EndDate),
            DateOnlyHelpers.GetNullableDateOnly(CatchupStartDate), DateOnlyHelpers.GetNullableDateOnly(CatchupEndDate),
            new List<TermClassSchedule>());
    }
}