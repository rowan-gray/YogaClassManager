using System.Collections.ObjectModel;
using YogaClassManager.Core.Models.Classes;
using YogaClassManager.Core.Models.Passes;

namespace YogaClassManager.Core.SQLite.Hydration;

/// <summary>Constructs the correct concrete CasualPass/DatedPass/TermPass instance from a PassStatus
/// row plus its (separately, batch-loaded) alterations. There is no discriminator column on Pass
/// itself - Kind is computed by the PassStatus/PassDetails view from which subtype table has a
/// matching row, following the same pattern the schema already uses for DatedPass/TermPass.</summary>
internal static class PassHydrator
{
    public static Pass Hydrate(PassStatusRow row, ObservableCollection<PassAlteration> alterations)
    {
        return row.Kind switch
        {
            "Casual" => new CasualPass(row.PassId, row.StudentId, row.CasualClassCount!.Value, alterations,
                row.ClassesUsed),

            "Dated" => new DatedPass(row.PassId, row.StudentId, row.DatedClassCount!.Value, alterations,
                row.ClassesUsed, row.DatedStartDate!.Value, row.DatedEndDate!.Value),

            "Term" => new TermPass(row.PassId, row.StudentId, row.ClassesUsed, alterations,
                new Term(row.TermId!.Value, row.TermName!, row.TermStartDate!.Value, row.TermEndDate!.Value,
                    row.TermCatchupStartDate, row.TermCatchupEndDate, classes: []),
                new TermClassSchedule(
                    new ClassSchedule(row.TermClassScheduleId!.Value, (DayOfWeek)row.TermClassDay!.Value,
                        TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(row.TermClassTime!.Value)),
                        isArchived: row.TermClassIsActive == 0),
                    row.TermClassCount ?? 0, row.TermClassUsesCount ?? 0)),

            _ => throw new InvalidOperationException($"Unknown Pass kind '{row.Kind}' for PassId {row.PassId}.")
        };
    }
}
