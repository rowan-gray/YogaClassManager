BEGIN TRANSACTION;
CREATE TABLE "ClassRoll" (
	"ClassId"	INTEGER,
	"ClassScheduleId"	INTEGER NOT NULL,
	"Date"	TEXT NOT NULL,
	FOREIGN KEY("ClassScheduleId") REFERENCES "ClassSchedule"("ClassScheduleId") ON DELETE RESTRICT ON UPDATE CASCADE,
	PRIMARY KEY("ClassId")
);
CREATE TABLE "ClassSchedule" (
	"ClassScheduleId"	INTEGER,
	"Day"	INTEGER NOT NULL CHECK(0 <= Day < 7),
	"Time"	INTEGER NOT NULL CHECK(0 <= Time < 1440),
	"IsActive"	INTEGER NOT NULL DEFAULT 'True',
	PRIMARY KEY("ClassScheduleId"),
	UNIQUE("Day","Time")
);
CREATE TABLE "ClassStudents" (
	"ClassId"	INTEGER,
	"StudentId"	INTEGER,
	"PassId"	INTEGER,
	FOREIGN KEY("ClassId") REFERENCES "ClassRoll"("ClassId") ON DELETE CASCADE ON UPDATE CASCADE,
	FOREIGN KEY("StudentId") REFERENCES "Student"("StudentId") ON DELETE CASCADE ON UPDATE CASCADE,
	FOREIGN KEY("PassId") REFERENCES "Pass"("PassId"),
	PRIMARY KEY("ClassId","StudentId")
);
CREATE TABLE "DatedPass" (
	"PassId"	INTEGER,
	"ClassCount"	INTEGER,
	"StartDate"	TEXT,
	"EndDate"	TEXT,
	FOREIGN KEY("PassId") REFERENCES "Pass"("PassId") ON DELETE CASCADE ON UPDATE CASCADE,
	PRIMARY KEY("PassId")
);
CREATE TABLE "EmergencyContact" (
	"EmergencyContactId"	INTEGER,
	"Relationship"	INTEGER NOT NULL CHECK(0 <= Relationship < 4),
	FOREIGN KEY("EmergencyContactId") REFERENCES "Person"("PersonId") ON DELETE CASCADE ON UPDATE CASCADE,
	PRIMARY KEY("EmergencyContactId")
);
CREATE TABLE "Modifications" (
	"table_name"	TEXT NOT NULL,
	"id_name"	INTEGER NOT NULL,
	"id_value"	INTEGER NOT NULL,
	"action"	TEXT NOT NULL,
	"changed_at"	NUMERIC DEFAULT (strftime('%s','now') || substr(strftime('%f','now'),4)),
	PRIMARY KEY("table_name","id_name","id_value") ON CONFLICT REPLACE
);
CREATE TABLE "Pass" (
	"PassId"	INTEGER,
	"StudentId"	INTEGER NOT NULL,
	FOREIGN KEY("StudentId") REFERENCES "Student"("StudentId") ON DELETE RESTRICT ON UPDATE CASCADE,
	PRIMARY KEY("PassId")
);
CREATE TABLE "PassAlterations" (
	"PassAlterationId"	INTEGER NOT NULL,
	"PassId"	INTEGER NOT NULL,
	"AlerationCount"	INTEGER NOT NULL,
	"AlterationReason"	TEXT,
	PRIMARY KEY("PassAlterationId"),
	FOREIGN KEY("PassId") REFERENCES "Pass"("PassId") ON UPDATE Cascade ON DELETE Cascade
);
CREATE TABLE "Person" (
	"PersonId"	INTEGER,
	"FirstName"	TEXT NOT NULL,
	"LastName"	TEXT,
	"PhoneNumber"	TEXT NOT NULL,
	"Email"	TEXT CHECK("Email" LIKE '%@%.%'),
	"IsActive"	INTEGER DEFAULT 1 CHECK(0 <= "IsActive" <= 1),
	PRIMARY KEY("PersonId")
);
CREATE TABLE "Student" (
	"StudentId"	INTEGER,
	FOREIGN KEY("StudentId") REFERENCES "Person"("PersonId") ON DELETE RESTRICT ON UPDATE CASCADE,
	PRIMARY KEY("StudentId")
);
CREATE TABLE "StudentEmergencyContacts" (
	"StudentId"	INTEGER,
	"EmergencyContactId"	INTEGER,
	"Relationship"	INTEGER NOT NULL DEFAULT 0,
	PRIMARY KEY("StudentId","EmergencyContactId"),
	FOREIGN KEY("EmergencyContactId") REFERENCES "Person"("PersonId") ON DELETE RESTRICT ON UPDATE CASCADE,
	FOREIGN KEY("StudentId") REFERENCES "Student"("StudentId") ON DELETE CASCADE ON UPDATE CASCADE
);
CREATE TABLE "StudentHealthConcerns" (
	"StudentId"	INTEGER NOT NULL,
	"HealthConcern"	TEXT NOT NULL,
	UNIQUE("StudentId","HealthConcern"),
	FOREIGN KEY("StudentId") REFERENCES "Student"("StudentId") ON DELETE CASCADE ON UPDATE CASCADE
);
CREATE TABLE "Term" (
	"TermId"	INTEGER NOT NULL,
	"TermName"	TEXT NOT NULL,
	"StartDate"	TEXT NOT NULL,
	"EndDate"	TEXT NOT NULL,
	"CatchupStartDate"	TEXT,
	"CatchupEndDate"	TEXT,
	PRIMARY KEY("TermId")
);
CREATE TABLE "TermClasses" (
	"TermId"	INTEGER,
	"ClassId"	INTEGER,
	"ClassCount"	INTEGER NOT NULL,
	FOREIGN KEY("ClassId") REFERENCES "ClassSchedule"("ClassScheduleId"),
	FOREIGN KEY("TermId") REFERENCES "Term"("TermId"),
	PRIMARY KEY("TermId","ClassId")
);
CREATE TABLE "TermPass" (
	"PassId"	INTEGER,
	"TermId"	INTEGER NOT NULL,
	"ClassId"	INTEGER NOT NULL DEFAULT 1,
	PRIMARY KEY("PassId"),
	FOREIGN KEY("PassId") REFERENCES "Pass"("PassId") ON DELETE CASCADE ON UPDATE CASCADE,
	FOREIGN KEY("TermId") REFERENCES "Term"("TermId") ON DELETE CASCADE ON UPDATE CASCADE,
	FOREIGN KEY("ClassId") REFERENCES "ClassSchedule"("ClassScheduleId") ON DELETE CASCADE ON UPDATE CASCADE
);

CREATE VIEW PassUses AS
SELECT Pass.PassId AS PassId, Count(ClassStudents.ClassId) AS TimesUsed
FROM Pass
LEFT JOIN ClassStudents
ON Pass.PassId = ClassStudents.PassId
GROUP BY Pass.PassId;

CREATE VIEW PassesTotalClasses AS 
SELECT Pass.PassId AS PassId, COALESCE(DatedPass.ClassCount,0) + SUM(COALESCE(PassAlterations.AlerationCount,0)) + COALESCE(TermClasses.ClassCount,0) AS NumberOfClasses 
FROM Pass LEFT JOIN PassAlterations 
ON Pass.PassId = PassAlterations.PassId 
LEFT JOIN DatedPass 
ON Pass.PassId = DatedPass.PassId 
LEFT JOIN TermPass 
ON Pass.PassId = TermPass.PassId 
LEFT JOIN TermClasses 
ON TermPass.TermId = TermClasses.TermId 
AND TermPass.ClassId = TermClasses.ClassId 
GROUP BY Pass.PassId;

CREATE VIEW TermClassUses AS 
SELECT TC.TermId, TC.ClassId, COUNT(TP.PassId) AS Uses
FROM TermClasses TC
LEFT JOIN TermPass TP ON TP.TermId = TC.TermId 
AND TP.ClassId = TC.ClassId
GROUP BY TC.TermId, TC.ClassId;

DROP TRIGGER IF EXISTS ClassRoll_ondelete;
DROP TRIGGER IF EXISTS  ClassRoll_onupdate;
DROP TRIGGER IF EXISTS  ClassRoll_oninsert;

CREATE TRIGGER IF NOT EXISTS ClassRoll_ondelete AFTER DELETE ON ClassRoll
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('ClassRoll','ClassId',OLD.ClassId,'DELETE');
END;

CREATE TRIGGER IF NOT EXISTS ClassRoll_onupdate AFTER UPDATE ON ClassRoll
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('ClassRoll','ClassId',OLD.ClassId,'UPDATE');
END;
     
CREATE TRIGGER IF NOT EXISTS ClassRoll_oninsert AFTER INSERT ON ClassRoll
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('ClassRoll','ClassId',NEW.ClassId,'INSERT');
END;

DROP TRIGGER IF EXISTS  ClassSchedule_ondelete;
DROP TRIGGER IF EXISTS  ClassSchedule_onupdate;
DROP TRIGGER IF EXISTS  ClassSchedule_oninsert;

CREATE TRIGGER IF NOT EXISTS ClassSchedule_ondelete AFTER DELETE ON ClassSchedule
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('ClassSchedule','ClassScheduleId',OLD.ClassScheduleId,'DELETE');
END;

CREATE TRIGGER IF NOT EXISTS ClassSchedule_onupdate AFTER UPDATE ON ClassSchedule
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('ClassSchedule','ClassScheduleId',OLD.ClassScheduleId,'UPDATE');
END;

CREATE TRIGGER IF NOT EXISTS ClassSchedule_oninsert AFTER INSERT ON ClassSchedule
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('ClassSchedule','ClassScheduleId',NEW.ClassScheduleId,'INSERT');
END;

DROP TRIGGER IF EXISTS  ClassStudents_ondelete;
DROP TRIGGER IF EXISTS  ClassStudents_onupdate;
DROP TRIGGER IF EXISTS  ClassStudents_oninsert;

CREATE TRIGGER IF NOT EXISTS ClassStudents_ondelete AFTER DELETE ON ClassStudents
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('ClassStudents','ClassId',OLD.ClassId,'DELETE');
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('ClassStudents','StudentId',OLD.StudentId,'DELETE');
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('ClassStudents','PassId',OLD.PassId,'DELETE');
END;

CREATE TRIGGER IF NOT EXISTS ClassStudents_onupdate AFTER UPDATE ON ClassStudents
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('ClassStudents','ClassId',OLD.ClassId,'UPDATE');
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('ClassStudents','StudentId',OLD.StudentId,'UPDATE');
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('ClassStudents','PassId',OLD.PassId,'UPDATE');
END;

CREATE TRIGGER IF NOT EXISTS ClassStudents_oninsert AFTER INSERT ON ClassStudents
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('ClassStudents','ClassId',NEW.ClassId,'INSERT');
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('ClassStudents','StudentId',NEW.StudentId,'INSERT');
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('ClassStudents','PassId',NEW.PassId,'INSERT');
END;

DROP TRIGGER IF EXISTS  DatedPass_ondelete;
DROP TRIGGER IF EXISTS  DatedPass_onupdate;
DROP TRIGGER IF EXISTS  DatedPass_oninsert;

CREATE TRIGGER IF NOT EXISTS DatedPass_ondelete AFTER DELETE ON DatedPass
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('DatedPass','PassId',OLD.PassId,'DELETE');
END;

CREATE TRIGGER IF NOT EXISTS DatedPass_onupdate AFTER UPDATE ON DatedPass
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('DatedPass','PassId',OLD.PassId,'UPDATE');
END;

CREATE TRIGGER IF NOT EXISTS DatedPass_oninsert AFTER INSERT ON DatedPass
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('DatedPass','PassId',NEW.PassId,'INSERT');
END;

DROP TRIGGER IF EXISTS  EmergencyContact_ondelete;
DROP TRIGGER IF EXISTS  EmergencyContact_onupdate;
DROP TRIGGER IF EXISTS  EmergencyContact_oninsert;

CREATE TRIGGER IF NOT EXISTS EmergencyContact_ondelete AFTER DELETE ON EmergencyContact
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('EmergencyContact','EmergencyContactId',OLD.EmergencyContactId,'DELETE');
END;

CREATE TRIGGER IF NOT EXISTS EmergencyContact_onupdate AFTER UPDATE ON EmergencyContact
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('EmergencyContact','EmergencyContactId',OLD.EmergencyContactId,'UPDATE');
END;

CREATE TRIGGER IF NOT EXISTS EmergencyContact_oninsert AFTER INSERT ON EmergencyContact
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('EmergencyContact','EmergencyContactId',NEW.EmergencyContactId,'INSERT');
END;

DROP TRIGGER IF EXISTS  Pass_ondelete;
DROP TRIGGER IF EXISTS  Pass_onupdate;
DROP TRIGGER IF EXISTS  Pass_oninsert;

CREATE TRIGGER IF NOT EXISTS Pass_ondelete AFTER DELETE ON Pass
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('Pass','PassId',OLD.PassId,'DELETE');
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('Pass','StudentId',OLD.StudentId,'DELETE');
END;

CREATE TRIGGER IF NOT EXISTS Pass_onupdate AFTER UPDATE ON Pass
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('Pass','PassId',OLD.PassId,'UPDATE');
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('Pass','StudentId',OLD.StudentId,'UPDATE');
END;

CREATE TRIGGER IF NOT EXISTS Pass_oninsert AFTER INSERT ON Pass
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('Pass','PassId',NEW.PassId,'INSERT');
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('Pass','StudentId',NEW.StudentId,'INSERT');
END;

DROP TRIGGER IF EXISTS  PassAlterations_ondelete;
DROP TRIGGER IF EXISTS  PassAlterations_onupdate;
DROP TRIGGER IF EXISTS  PassAlterations_oninsert;

CREATE TRIGGER IF NOT EXISTS PassAlterations_ondelete AFTER DELETE ON PassAlterations
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('PassAlterations','PassAlterationId',OLD.PassAlterationId,'DELETE');
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('PassAlterations','PassId',OLD.PassId,'DELETE');
END;

CREATE TRIGGER IF NOT EXISTS PassAlterations_onupdate AFTER UPDATE ON PassAlterations
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('PassAlterations','PassAlterationId',OLD.PassAlterationId,'UPDATE');
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('PassAlterations','PassId',OLD.PassId,'UPDATE');
END;

CREATE TRIGGER IF NOT EXISTS PassAlterations_oninsert AFTER INSERT ON PassAlterations
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('PassAlterations','PassAlterationId',NEW.PassAlterationId,'INSERT');
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('PassAlterations','PassId',NEW.PassId,'INSERT');
END;

DROP TRIGGER IF EXISTS  Person_ondelete;
DROP TRIGGER IF EXISTS  Person_onupdate;
DROP TRIGGER IF EXISTS  Person_oninsert;

CREATE TRIGGER IF NOT EXISTS Person_ondelete AFTER DELETE ON Person
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('Person','PersonId',OLD.PersonId,'DELETE');
END;

CREATE TRIGGER IF NOT EXISTS Person_onupdate AFTER UPDATE ON Person
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('Person','PersonId',OLD.PersonId,'UPDATE');
END;

CREATE TRIGGER IF NOT EXISTS Person_oninsert AFTER INSERT ON Person
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('Person','PersonId',NEW.PersonId,'INSERT');
END;

DROP TRIGGER IF EXISTS  Student_ondelete;
DROP TRIGGER IF EXISTS  Student_onupdate;
DROP TRIGGER IF EXISTS  Student_oninsert;

CREATE TRIGGER IF NOT EXISTS Student_ondelete AFTER DELETE ON Student
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('Student','StudentId',OLD.StudentId,'DELETE');
END;

CREATE TRIGGER IF NOT EXISTS Student_onupdate AFTER UPDATE ON Student
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('Student','StudentId',OLD.StudentId,'UPDATE');
END;

CREATE TRIGGER IF NOT EXISTS Student_oninsert AFTER INSERT ON Student
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('Student','StudentId',NEW.StudentId,'INSERT');
END;

DROP TRIGGER IF EXISTS  StudentEmergencyContacts_ondelete;
DROP TRIGGER IF EXISTS  StudentEmergencyContacts_onupdate;
DROP TRIGGER IF EXISTS  StudentEmergencyContacts_oninsert;

CREATE TRIGGER IF NOT EXISTS StudentEmergencyContacts_ondelete AFTER DELETE ON StudentEmergencyContacts
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('StudentEmergencyContacts','StudentId',OLD.StudentId,'DELETE');
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('StudentEmergencyContacts','EmergencyContactId',OLD.EmergencyContactId,'DELETE');
END;

CREATE TRIGGER IF NOT EXISTS StudentEmergencyContacts_onupdate AFTER UPDATE ON StudentEmergencyContacts
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('StudentEmergencyContacts','StudentId',OLD.StudentId,'UPDATE');
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('StudentEmergencyContacts','EmergencyContactId',OLD.EmergencyContactId,'UPDATE');
END;

CREATE TRIGGER IF NOT EXISTS StudentEmergencyContacts_oninsert AFTER INSERT ON StudentEmergencyContacts
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('StudentEmergencyContacts','StudentId',NEW.StudentId,'INSERT');
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('StudentEmergencyContacts','EmergencyContactId',NEW.EmergencyContactId,'INSERT');
END;

DROP TRIGGER IF EXISTS  StudentHealthConcerns_ondelete;
DROP TRIGGER IF EXISTS  StudentHealthConcerns_onupdate;
DROP TRIGGER IF EXISTS  StudentHealthConcerns_oninsert;

CREATE TRIGGER IF NOT EXISTS StudentHealthConcerns_ondelete AFTER DELETE ON StudentHealthConcerns
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('StudentHealthConcerns','StudentId',OLD.StudentId,'DELETE');
END;

CREATE TRIGGER IF NOT EXISTS StudentHealthConcerns_onupdate AFTER UPDATE ON StudentHealthConcerns
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('StudentHealthConcerns','StudentId',OLD.StudentId,'UPDATE');
END;

CREATE TRIGGER IF NOT EXISTS StudentHealthConcerns_oninsert AFTER INSERT ON StudentHealthConcerns
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('StudentHealthConcerns','StudentId',NEW.StudentId,'INSERT');
END;

DROP TRIGGER IF EXISTS  Term_ondelete;
DROP TRIGGER IF EXISTS  Term_onupdate;
DROP TRIGGER IF EXISTS  Term_oninsert;

CREATE TRIGGER IF NOT EXISTS Term_ondelete AFTER DELETE ON Term
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('Term','TermId',OLD.TermId,'DELETE');
END;

CREATE TRIGGER IF NOT EXISTS Term_onupdate AFTER UPDATE ON Term
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('Term','TermId',OLD.TermId,'UPDATE');
END;

CREATE TRIGGER IF NOT EXISTS Term_oninsert AFTER INSERT ON Term
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('Term','TermId',NEW.TermId,'INSERT');
END;

DROP TRIGGER IF EXISTS  TermClasses_ondelete;
DROP TRIGGER IF EXISTS  TermClasses_onupdate;
DROP TRIGGER IF EXISTS  TermClasses_oninsert;

CREATE TRIGGER IF NOT EXISTS TermClasses_ondelete AFTER DELETE ON TermClasses
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('TermClasses','TermId',OLD.TermId,'DELETE');
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('TermClasses','ClassId',OLD.ClassId,'DELETE');
END;

CREATE TRIGGER IF NOT EXISTS TermClasses_onupdate AFTER UPDATE ON TermClasses
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('TermClasses','TermId',OLD.TermId,'UPDATE');
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('TermClasses','ClassId',OLD.ClassId,'UPDATE');
END;

CREATE TRIGGER IF NOT EXISTS TermClasses_oninsert AFTER INSERT ON TermClasses
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('TermClasses','TermId',NEW.TermId,'INSERT');
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('TermClasses','ClassId',NEW.ClassId,'INSERT');
END;

DROP TRIGGER IF EXISTS  TermPass_ondelete;
DROP TRIGGER IF EXISTS  TermPass_onupdate;
DROP TRIGGER IF EXISTS  TermPass_oninsert;

CREATE TRIGGER IF NOT EXISTS TermPass_ondelete AFTER DELETE ON TermPass
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('TermPass','PassId',OLD.PassId,'DELETE');
END;

CREATE TRIGGER IF NOT EXISTS TermPass_onupdate AFTER UPDATE ON TermPass
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('TermPass','PassId',OLD.PassId,'UPDATE');
END;

CREATE TRIGGER IF NOT EXISTS TermPass_oninsert AFTER INSERT ON TermPass
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('TermPass','PassId',NEW.PassId,'INSERT');
END;
COMMIT;