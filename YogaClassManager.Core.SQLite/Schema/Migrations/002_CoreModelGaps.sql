-- ============================================================================
-- CasualPass: Core's CasualPass.ClassCount needs its own persisted column;
-- no equivalent table exists in production today.
-- ============================================================================
CREATE TABLE IF NOT EXISTS "CasualPass" (
    "PassId"     INTEGER,
    "ClassCount" INTEGER NOT NULL,
    PRIMARY KEY("PassId"),
    FOREIGN KEY("PassId") REFERENCES "Pass"("PassId") ON DELETE CASCADE ON UPDATE CASCADE
);

CREATE TRIGGER IF NOT EXISTS CasualPass_oninsert AFTER INSERT ON CasualPass
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('CasualPass','PassId',NEW.PassId,'INSERT');
END;
CREATE TRIGGER IF NOT EXISTS CasualPass_onupdate AFTER UPDATE ON CasualPass
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('CasualPass','PassId',OLD.PassId,'UPDATE');
END;
CREATE TRIGGER IF NOT EXISTS CasualPass_ondelete AFTER DELETE ON CasualPass
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('CasualPass','PassId',OLD.PassId,'DELETE');
END;

-- ============================================================================
-- PassesTotalClasses must also fold in CasualPass.ClassCount now that the table
-- exists. 1:1 join (PassId is CasualPass's own PK) - no GROUP BY multiplication
-- risk, unlike the existing PassAlterations 1:many join the GROUP BY already handles.
-- ============================================================================
DROP VIEW IF EXISTS "PassesTotalClasses";
CREATE VIEW "PassesTotalClasses" AS
SELECT Pass.PassId AS PassId,
       COALESCE(DatedPass.ClassCount, 0)
         + COALESCE(CasualPass.ClassCount, 0)
         + SUM(COALESCE(PassAlterations.AlerationCount, 0))
         + COALESCE(TermClasses.ClassCount, 0) AS NumberOfClasses
FROM Pass
LEFT JOIN PassAlterations ON Pass.PassId = PassAlterations.PassId
LEFT JOIN DatedPass       ON Pass.PassId = DatedPass.PassId
LEFT JOIN CasualPass      ON Pass.PassId = CasualPass.PassId
LEFT JOIN TermPass        ON Pass.PassId = TermPass.PassId
LEFT JOIN TermClasses     ON TermPass.TermId = TermClasses.TermId
                         AND TermPass.ClassId = TermClasses.ClassId
GROUP BY Pass.PassId;

-- ============================================================================
-- PassDetails: one row per Pass, unifying Pass + its (at most one) subtype row,
-- with a computed Kind discriminator, plus every subtype-specific column needed
-- to hydrate the right concrete C# type. Pure 1:1/1:0..1 LEFT JOINs throughout
-- (CasualPass/DatedPass/TermPass share Pass's PK; Term/TermClasses/TermClassUses/
-- ClassSchedule are all uniquely keyed per TermPass row) - no GROUP BY needed here.
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
    TermClassUses.Uses       AS TermClassUsesCount,
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

-- ============================================================================
-- PassStatus: PassDetails + PassesTotalClasses + PassUses joined, with
-- ClassesUsed / NumberOfClasses / ClassesRemaining / IsDepleted / IsExpired all
-- computed in SQL so PassFilter.IncludeExpired/IncludeDepleted can filter with a
-- plain WHERE, never in C#. ClassesUsed is a DERIVED value (count of actual
-- ClassStudents/roll usage, from the existing PassUses view) - NOT a stored
-- column; PassAlterations only ever affect NumberOfClasses (total capacity).
-- IsExpired branches mirror each subtype's own C# IsExpired exactly:
--   CasualPass  -> always false
--   DatedPass   -> now > EndDate
--   TermPass    -> now > COALESCE(CatchupEndDate, EndDate)
-- Uses date('now','localtime') to match the in-memory model's
-- DateOnly.FromDateTime(DateTime.Now) (LOCAL date), not UTC.
-- ============================================================================
DROP VIEW IF EXISTS "PassStatus";
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
    pd.TermClassIsActive,
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

-- ============================================================================
-- IdentityLinkage: correlated scalar subqueries (not multi-way LEFT JOINs) to
-- avoid row-multiplication/COUNT-inflation - e.g. a Student with both several
-- Passes and several attendance rows would inflate a naive multi-join COUNT.
-- EmergencyContactLinkCount counts rows where THIS person IS the emergency
-- contact (matches InMemoryIdentityRepository.BuildLinkageSummary's
-- `l.EmergencyContactIdentityId == identityId`), not rows where they HAVE one.
-- ============================================================================
DROP VIEW IF EXISTS "IdentityLinkage";
CREATE VIEW "IdentityLinkage" AS
SELECT
    Person.PersonId AS PersonId,
    CASE WHEN Student.StudentId IS NOT NULL THEN 1 ELSE 0 END AS IsStudent,
    (SELECT COUNT(*) FROM StudentEmergencyContacts sec WHERE sec.EmergencyContactId = Person.PersonId) AS EmergencyContactLinkCount,
    (SELECT COUNT(*) FROM Pass p WHERE p.StudentId = Person.PersonId) AS PassCount,
    (SELECT COUNT(*) FROM ClassStudents cs WHERE cs.StudentId = Person.PersonId) AS AttendanceCount
FROM Person
LEFT JOIN Student ON Student.StudentId = Person.PersonId;

-- ============================================================================
-- StudentLastAttendance: most recent attendance date per student.
-- ============================================================================
DROP VIEW IF EXISTS "StudentLastAttendance";
CREATE VIEW "StudentLastAttendance" AS
SELECT cs.StudentId AS StudentId, MAX(cr.Date) AS LastAttendedDate
FROM ClassStudents cs
JOIN ClassRoll cr ON cr.ClassId = cs.ClassId
GROUP BY cs.StudentId;

-- ============================================================================
-- EmergencyContactDetails: StudentEmergencyContacts + Person, one row per link.
-- ============================================================================
DROP VIEW IF EXISTS "EmergencyContactDetails";
CREATE VIEW "EmergencyContactDetails" AS
SELECT
    sec.StudentId          AS StudentId,
    sec.EmergencyContactId AS PersonId,
    p.FirstName            AS FirstName,
    p.LastName             AS LastName,
    p.PhoneNumber          AS PhoneNumber,
    p.Email                AS Email,
    p.IsActive              AS IsActive,
    sec.Relationship        AS Relationship
FROM StudentEmergencyContacts sec
JOIN Person p ON p.PersonId = sec.EmergencyContactId;

-- ============================================================================
-- Supporting indexes - keep count/existence/join queries index-backed instead
-- of full-scanning, per "reduce data queried" instruction.
-- ============================================================================
CREATE INDEX IF NOT EXISTS "IX_ClassStudents_PassId"    ON "ClassStudents"("PassId");
CREATE INDEX IF NOT EXISTS "IX_ClassStudents_StudentId"  ON "ClassStudents"("StudentId");
CREATE INDEX IF NOT EXISTS "IX_ClassRoll_ClassScheduleId" ON "ClassRoll"("ClassScheduleId");
CREATE INDEX IF NOT EXISTS "IX_ClassRoll_Date"           ON "ClassRoll"("Date");
CREATE INDEX IF NOT EXISTS "IX_Pass_StudentId"           ON "Pass"("StudentId");
CREATE INDEX IF NOT EXISTS "IX_StudentEmergencyContacts_EmergencyContactId" ON "StudentEmergencyContacts"("EmergencyContactId");
CREATE INDEX IF NOT EXISTS "IX_TermPass_TermId_ClassId"  ON "TermPass"("TermId","ClassId");
CREATE INDEX IF NOT EXISTS "IX_TermClasses_ClassId"      ON "TermClasses"("ClassId");

-- ============================================================================
-- Fix a latent defect in the production ClassStudents_{oninsert,onupdate,ondelete}
-- triggers copied verbatim in migration 001: they unconditionally log a PassId
-- change row into Modifications, whose id_value column is NOT NULL - but
-- ClassStudents.PassId is nullable (a walk-in student with no pass attending a
-- class is a normal, fully-supported scenario per the model), so any insert/
-- update/delete of a ClassStudents row with a NULL PassId crashes with a NOT
-- NULL constraint violation. Split the PassId logging into its own trigger,
-- guarded by a WHEN clause, so it's simply skipped when there's no pass to log -
-- the ClassId/StudentId logging (always NOT NULL) is unaffected.
-- ============================================================================
DROP TRIGGER IF EXISTS ClassStudents_oninsert;
DROP TRIGGER IF EXISTS ClassStudents_onupdate;
DROP TRIGGER IF EXISTS ClassStudents_ondelete;

CREATE TRIGGER ClassStudents_oninsert AFTER INSERT ON ClassStudents
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('ClassStudents','ClassId',NEW.ClassId,'INSERT');
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('ClassStudents','StudentId',NEW.StudentId,'INSERT');
END;
CREATE TRIGGER ClassStudents_oninsert_passid AFTER INSERT ON ClassStudents WHEN NEW.PassId IS NOT NULL
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('ClassStudents','PassId',NEW.PassId,'INSERT');
END;
CREATE TRIGGER ClassStudents_onupdate AFTER UPDATE ON ClassStudents
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('ClassStudents','ClassId',OLD.ClassId,'UPDATE');
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('ClassStudents','StudentId',OLD.StudentId,'UPDATE');
END;
CREATE TRIGGER ClassStudents_onupdate_passid AFTER UPDATE ON ClassStudents WHEN OLD.PassId IS NOT NULL
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('ClassStudents','PassId',OLD.PassId,'UPDATE');
END;
CREATE TRIGGER ClassStudents_ondelete AFTER DELETE ON ClassStudents
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('ClassStudents','ClassId',OLD.ClassId,'DELETE');
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('ClassStudents','StudentId',OLD.StudentId,'DELETE');
END;
CREATE TRIGGER ClassStudents_ondelete_passid AFTER DELETE ON ClassStudents WHEN OLD.PassId IS NOT NULL
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('ClassStudents','PassId',OLD.PassId,'DELETE');
END;
