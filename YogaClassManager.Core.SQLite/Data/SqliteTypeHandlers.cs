using System.Data;
using Dapper;

namespace YogaClassManager.Core.SQLite.Data;

/// <summary>DateOnly stored as TEXT in ISO yyyy-MM-dd form - matches the schema's Date/StartDate/EndDate
/// columns and sorts correctly as plain strings. Handles non-nullable DateOnly too: Dapper registers a
/// value-type handler for both T and T?, so this one covers both.</summary>
public sealed class NullableDateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly?>
{
    public override void SetValue(IDbDataParameter parameter, DateOnly? value)
    {
        parameter.Value = value is null ? DBNull.Value : value.Value.ToString("yyyy-MM-dd");
    }

    public override DateOnly? Parse(object value)
    {
        return value is null or DBNull ? null : DateOnly.ParseExact((string)value, "yyyy-MM-dd");
    }
}

public static class SqliteTypeHandlers
{
    private static int registered;

    /// <summary>Idempotent - Dapper's type handler registry is process-global, so this only needs to
    /// run once no matter how many SqliteDataStore instances get created (e.g. one per test).</summary>
    public static void EnsureRegistered()
    {
        if (Interlocked.Exchange(ref registered, 1) == 1)
            return;

        SqlMapper.AddTypeHandler(new NullableDateOnlyTypeHandler());
        // No TimeOnly handler: ClassSchedule.Time is INTEGER minutes since midnight, and every
        // repository converts TimeOnly to/from minutes itself, so Dapper never sees a TimeOnly.
        // DayOfWeek stored as plain INTEGER (0-6) - Dapper maps enums to their underlying int by
        // default, and DayOfWeek's own underlying values (Sunday=0..Saturday=6) already match the
        // schema's CHECK(0 <= Day < 7) - no custom handler needed.
    }
}
