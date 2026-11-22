-- ============================================================================
-- PassDetails.TermClassUsesCount comes from TermClassUses' COUNT aggregate, so
-- SQLite gives it no declared type: it reads as NULL for every non-term pass
-- and INTEGER otherwise. Dapper builds its row mapper from the first row's
-- types, so a NULL first row broke hydration of every later term pass.
-- COALESCE makes it an INTEGER on every row (PassHydrator already treats a
-- missing count as 0).
-- ============================================================================
DROP VIEW IF EXISTS "PassDetails";
CREATE VIEW "PassDetails" AS
SELECT
    Pass.PassId              AS PassId,
    Pass.StudentId           AS StudentId,
    CASE
        WHEN CasualPass.PassId IS NOT NULL THEN 'Casual'
        WHEN DatedPass.PassId  IS NOT NULL THEN 'Dated'
        WHEN TermPass.PassId   IS NOT NULL THEN 'Term'
    END                      AS Kind,
    CasualPass.ClassCount    AS CasualClassCount,
    DatedPass.ClassCount     AS DatedClassCount,
    DatedPass.StartDate      AS DatedStartDate,
    DatedPass.EndDate        AS DatedEndDate,
    TermPass.TermId          AS TermId,
    TermPass.ClassId         AS TermClassScheduleId,
    Term.TermName            AS TermName,
    Term.StartDate           AS TermStartDate,
    Term.EndDate             AS TermEndDate,
    Term.CatchupStartDate    AS TermCatchupStartDate,
    Term.CatchupEndDate      AS TermCatchupEndDate,
    TermClasses.ClassCount   AS TermClassCount,
    COALESCE(TermClassUses.Uses, 0) AS TermClassUsesCount,
    ClassSchedule.Day        AS TermClassDay,
    ClassSchedule.Time       AS TermClassTime,
    ClassSchedule.IsActive   AS TermClassIsActive
FROM Pass
LEFT JOIN CasualPass    ON CasualPass.PassId = Pass.PassId
LEFT JOIN DatedPass     ON DatedPass.PassId  = Pass.PassId
LEFT JOIN TermPass      ON TermPass.PassId   = Pass.PassId
LEFT JOIN Term          ON Term.TermId       = TermPass.TermId
LEFT JOIN TermClasses   ON TermClasses.TermId = TermPass.TermId AND TermClasses.ClassId = TermPass.ClassId
LEFT JOIN TermClassUses ON TermClassUses.TermId = TermPass.TermId AND TermClassUses.ClassId = TermPass.ClassId
LEFT JOIN ClassSchedule ON ClassSchedule.ClassScheduleId = TermPass.ClassId;
