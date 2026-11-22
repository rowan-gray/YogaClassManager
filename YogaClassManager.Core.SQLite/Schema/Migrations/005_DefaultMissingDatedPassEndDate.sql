-- ============================================================================
-- Some legacy production DatedPass rows have no EndDate (all of them empty
-- placeholders: 0 classes, no uses or alterations). Core's DatedPass requires
-- an end date, so PassDetails reports a missing one as StartDate + 10 weeks.
-- PassStatus.IsExpired reads DatedEndDate from here, so the "include expired"
-- filter agrees with the hydrated model. The stored rows are left untouched.
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
    COALESCE(DatedPass.EndDate, date(DatedPass.StartDate, '+70 days')) AS DatedEndDate,
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
