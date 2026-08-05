using System.Reflection;

namespace YogaClassManager.Core.Data;

/// <summary>
///     Attaches a string value (e.g. a database column/keyword) to an enum member.
///     A future SQL-backed IDbModel can read this to build ORDER BY clauses etc.
///     without any change to the enum's callers.
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public class StringValueAttribute : Attribute
{
    public StringValueAttribute(string value)
    {
        StringValue = value;
    }

    public string StringValue { get; }

    public static string GetStringValue(Enum value)
    {
        var type = value.GetType();
        var fieldInfo = type.GetField(value.ToString());

        if (fieldInfo is null)
            return value.ToString();

        var attributes = fieldInfo.GetCustomAttributes(typeof(StringValueAttribute), false) as StringValueAttribute[];

        return attributes is { Length: > 0 } ? attributes[0].StringValue : value.ToString();
    }
}
