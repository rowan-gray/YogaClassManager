namespace YogaClassManager.Core.SQLite.Hydration;

/// <summary>Row shape of the PassStatus view (see Schema/Migrations/002_CoreModelGaps.sql) - one row
/// per Pass with its subtype-specific columns (nullable, populated only for the matching Kind) plus
/// the SQL-computed NumberOfClasses/ClassesUsed/ClassesRemaining/IsDepleted/IsExpired.</summary>
internal sealed class PassStatusRow
{
    public int PassId { get; set; }
    public int StudentId { get; set; }
    public string Kind { get; set; } = "";

    public int? CasualClassCount { get; set; }

    public int? DatedClassCount { get; set; }
    public DateOnly? DatedStartDate { get; set; }
    public DateOnly? DatedEndDate { get; set; }

    public int? TermId { get; set; }
    public int? TermClassScheduleId { get; set; }
    public string? TermName { get; set; }
    public DateOnly? TermStartDate { get; set; }
    public DateOnly? TermEndDate { get; set; }
    public DateOnly? TermCatchupStartDate { get; set; }
    public DateOnly? TermCatchupEndDate { get; set; }
    public int? TermClassCount { get; set; }
    public int? TermClassUsesCount { get; set; }
    public int? TermClassDay { get; set; }
    public int? TermClassTime { get; set; }
    public int? TermClassIsActive { get; set; }

    public int NumberOfClasses { get; set; }
    public int ClassesUsed { get; set; }
    public int ClassesRemaining { get; set; }
    public int IsDepleted { get; set; }
    public int IsExpired { get; set; }
}

internal sealed class PassAlterationRow
{
    public int PassAlterationId { get; set; }
    public int PassId { get; set; }
    public int AlerationCount { get; set; }
    public string? AlterationReason { get; set; }
}
