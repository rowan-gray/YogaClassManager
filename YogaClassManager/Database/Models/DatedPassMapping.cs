namespace YogaClassManager.Database.Models;

internal class DatedPassMapping
{
    public int PassId { get; set; }
    public int StudentId { get; set; }
    public int TimesUsed { get; set; }
    public int ClassCount { get; set; }
    public string StartDate { get; set; }
    public string EndDate { get; set; }
}