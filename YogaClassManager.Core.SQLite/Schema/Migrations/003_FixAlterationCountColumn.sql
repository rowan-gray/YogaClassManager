-- ============================================================================
-- PassAlterations' count column is "AlterationCount" in real production
-- databases, but earlier versions of 001/002 used the misspelling
-- "AlerationCount". SchemaMigrator renames that column (if a database has it)
-- before this script runs; this script then rebuilds PassesTotalClasses, which
-- 002 may have created against the misspelled name - broken on any production
-- database, where that column never existed.
-- ============================================================================
DROP VIEW IF EXISTS "PassesTotalClasses";
CREATE VIEW "PassesTotalClasses" AS
SELECT Pass.PassId AS PassId,
       COALESCE(DatedPass.ClassCount, 0)
         + COALESCE(CasualPass.ClassCount, 0)
         + SUM(COALESCE(PassAlterations.AlterationCount, 0))
         + COALESCE(TermClasses.ClassCount, 0) AS NumberOfClasses
FROM Pass
LEFT JOIN PassAlterations ON Pass.PassId = PassAlterations.PassId
LEFT JOIN DatedPass       ON Pass.PassId = DatedPass.PassId
LEFT JOIN CasualPass      ON Pass.PassId = CasualPass.PassId
LEFT JOIN TermPass        ON Pass.PassId = TermPass.PassId
LEFT JOIN TermClasses     ON TermPass.TermId = TermClasses.TermId
                         AND TermPass.ClassId = TermClasses.ClassId
GROUP BY Pass.PassId;
