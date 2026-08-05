using System.Data;
using Dapper;

namespace YogaClassManager.Core.SQLite.Data;

/// <summary>DateOnly stored as TEXT in ISO yyyy-MM-dd form - matches the existing schema's
/// Date/StartDate/EndDate columns and sorts correctly as plain strings.</summary>
public sealed class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
{
    public override void SetValue(IDbDataParameter parameter, DateOnly value)
    {
        parameter.Value = value.ToString("yyyy-MM-dd");
    }

    public override DateOnly Parse(object value)
    {
        return DateOnly.ParseExact((string)value, "yyyy-MM-dd");
    }
}

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

/// <summary>TimeOnly stored as INTEGER minutes-since-midnight - matches ClassSchedule.Time's existing
/// CHECK(0 &lt;= Time &lt; 1440).</summary>
public sealed class TimeOnlyTypeHandler : SqlMapper.TypeHandler<TimeOnly>
{
    public override void SetValue(IDbDataParameter parameter, TimeOnly value)
    {
        parameter.Value = (long)(value.Hour * 60 + value.Minute);
    }

    public override TimeOnly Parse(object value)
    {
        return TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(Convert.ToInt64(value)));
    }
}

public sealed class NullableTimeOnlyTypeHandler : SqlMapper.TypeHandler<TimeOnly?>
{
    public override void SetValue(IDbDataParameter parameter, TimeOnly? value)
    {
        parameter.Value = value is null ? DBNull.Value : (long)(value.Value.Hour * 60 + value.Value.Minute);
    }

    public override TimeOnly? Parse(object value)
    {
        return value is null or DBNull ? null : TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(Convert.ToInt64(value)));
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

        SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());
        SqlMapper.AddTypeHandler(new NullableDateOnlyTypeHandler());
        SqlMapper.AddTypeHandler(new TimeOnlyTypeHandler());
        SqlMapper.AddTypeHandler(new NullableTimeOnlyTypeHandler());
        // DayOfWeek stored as plain INTEGER (0-6) - Dapper maps enums to their underlying int by
        // default, and DayOfWeek's own underlying values (Sunday=0..Saturday=6) already match the
        // schema's CHECK(0 <= Day < 7) - no custom handler needed.
    }
}
