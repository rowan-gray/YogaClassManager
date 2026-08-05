namespace YogaClassManager.Core.SQLite.Data;

/// <summary>Small shared helpers used across every repository's Query/LoadMultiple implementation.</summary>
internal static class SqlFilterBuilder
{
    /// <summary>Appends a deterministic tiebreaker (the entity's own PK) to whatever ORDER BY the
    /// filter produced, so LIMIT/OFFSET paging (as GrowableCollection performs via repeated
    /// LoadMultiple(count, skip: currentCount) calls) can never silently reorder/duplicate/skip rows
    /// across calls - unlike LINQ's OrderBy over an in-memory collection, SQL's OFFSET/LIMIT over a
    /// non-fully-deterministic ORDER BY is genuinely unsafe. This is a deliberate, documented
    /// improvement over the in-memory backend (which has no such tiebreaker), not a hidden behavior
    /// change.</summary>
    public static string AppendPkTiebreaker(string orderByClause, string pkColumn)
    {
        return $"{orderByClause}, {pkColumn} ASC";
    }

    /// <summary>Escapes '%', '_', and the escape character itself, so a literal % or _ in
    /// user-supplied filter text isn't treated as a SQL LIKE wildcard. Use with `LIKE @pattern ESCAPE '\'`.</summary>
    private static string EscapeLikeValue(string value)
    {
        return value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
    }

    public static string ContainsPattern(string value)
    {
        return "%" + EscapeLikeValue(value) + "%";
    }

    public static string StartsWithPattern(string value)
    {
        return EscapeLikeValue(value) + "%";
    }
}
