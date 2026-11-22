-- ============================================================================
-- ClassSchedule's "IsActive" column has always stored IsArchived (1 = archived,
-- 0 = active): the legacy MAUI app wrote IsActive={IsArchived} and filtered
-- "hide archived" with IsActive = 0, so every production database holds that
-- encoding. Rename the column to say what it holds. The values are already
-- correct and are kept, apart from normalising any non-integer leftovers (the
-- column's legacy DEFAULT is the string 'True') to 1/0.
--
-- PassDetails/PassStatus reference the column, so they're dropped first and
-- recreated unchanged apart from the alias, now TermClassIsArchived (bodies
-- copied from 005 and 002 respectively).
--
-- The legacy DEFAULT 'True' can't be changed without rebuilding the table
-- (SQLite has no ALTER COLUMN, and ClassRoll/TermClasses reference it); every
-- insert supplies IsArchived explicitly, so the default never applies.
-- ============================================================================
DROP VIEW IF EXISTS "PassStatus";
DROP VIEW IF EXISTS "PassDetails";

ALTER TABLE "ClassSchedule" RENAME COLUMN "IsActive" TO "IsArchived";

UPDATE "ClassSchedule"
SET "IsArchived" = CASE WHEN "IsArchived" IN (1, '1', 'True', 'true', 'TRUE') THEN 1 ELSE 0 END
WHERE typeof("IsArchived") != 'integer' OR "IsArchived" NOT IN (0, 1);

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
    ClassSchedule.IsArchived AS TermClassIsArchived
FROM Pass
LEFT JOIN CasualPass    ON CasualPass.PassId = Pass.PassId
LEFT JOIN DatedPass     ON DatedPass.PassId  = Pass.PassId
LEFT JOIN TermPass      ON TermPass.PassId   = Pass.PassId
LEFT JOIN Term          ON Term.TermId       = TermPass.TermId
LEFT JOIN TermClasses   ON TermClasses.TermId = TermPass.TermId AND TermClasses.ClassId = TermPass.ClassId
LEFT JOIN TermClassUses ON TermClassUses.TermId = TermPass.TermId AND TermClassUses.ClassId = TermPass.ClassId
LEFT JOIN ClassSchedule ON ClassSchedule.ClassScheduleId = TermPass.ClassId;

CREATE VIEW "PassStatus" AS
SELECT
    pd.PassId,
    pd.StudentId,
    pd.Kind,
    pd.CasualClassCount,
    pd.DatedClassCount,
    pd.DatedStartDate,
    pd.DatedEndDate,
    pd.TermId,
    pd.TermClassScheduleId,
    pd.TermName,
    pd.TermStartDate,
    pd.TermEndDate,
    pd.TermCatchupStartDate,
    pd.TermCatchupEndDate,
    pd.TermClassCount,
    pd.TermClassUsesCount,
    pd.TermClassDay,
    pd.TermClassTime,
    pd.TermClassIsArchived,
    ptc.NumberOfClasses                                   AS NumberOfClasses,
    COALESCE(pu.TimesUsed, 0)                              AS ClassesUsed,
    ptc.NumberOfClasses - COALESCE(pu.TimesUsed, 0)        AS ClassesRemaining,
    CASE WHEN ptc.NumberOfClasses - COALESCE(pu.TimesUsed, 0) <= 0 THEN 1 ELSE 0 END AS IsDepleted,
    CASE
        WHEN pd.Kind = 'Casual' THEN 0
        WHEN pd.Kind = 'Dated'  THEN CASE WHEN date('now','localtime') > pd.DatedEndDate THEN 1 ELSE 0 END
        WHEN pd.Kind = 'Term'   THEN CASE WHEN date('now','localtime') > COALESCE(pd.TermCatchupEndDate, pd.TermEndDate) THEN 1 ELSE 0 END
        ELSE 0
    END                                                    AS IsExpired
FROM PassDetails pd
JOIN PassesTotalClasses ptc ON ptc.PassId = pd.PassId
LEFT JOIN PassUses pu ON pu.PassId = pd.PassId;
