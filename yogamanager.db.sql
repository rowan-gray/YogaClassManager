BEGIN TRANSACTION;
CREATE TABLE IF NOT EXISTS "ClassRoll" (
	"ClassId"	INTEGER,
	"ClassScheduleId"	INTEGER NOT NULL,
	"Date"	TEXT NOT NULL,
	PRIMARY KEY("ClassId"),
	FOREIGN KEY("ClassScheduleId") REFERENCES "ClassSchedule"("ClassScheduleId") ON DELETE RESTRICT ON UPDATE CASCADE
);
CREATE TABLE IF NOT EXISTS "ClassSchedule" (
	"ClassScheduleId"	INTEGER,
	"Day"	INTEGER NOT NULL CHECK(0 <= "Day" < 7),
	"Time"	INTEGER NOT NULL CHECK(0 <= "Time" < 1440),
	"IsActive"	INTEGER NOT NULL DEFAULT 'True',
	PRIMARY KEY("ClassScheduleId"),
	UNIQUE("Day","Time")
);
CREATE TABLE IF NOT EXISTS "ClassStudents" (
	"ClassId"	INTEGER,
	"StudentId"	INTEGER,
	"PassId"	INTEGER,
	PRIMARY KEY("ClassId","StudentId"),
	FOREIGN KEY("ClassId") REFERENCES "ClassRoll"("ClassId") ON DELETE CASCADE ON UPDATE CASCADE,
	FOREIGN KEY("PassId") REFERENCES "Pass"("PassId"),
	FOREIGN KEY("StudentId") REFERENCES "Student"("StudentId") ON DELETE CASCADE ON UPDATE CASCADE
);
CREATE TABLE IF NOT EXISTS "DatedPass" (
	"PassId"	INTEGER,
	"ClassCount"	INTEGER,
	"StartDate"	TEXT,
	"EndDate"	TEXT,
	PRIMARY KEY("PassId"),
	FOREIGN KEY("PassId") REFERENCES "Pass"("PassId") ON DELETE CASCADE ON UPDATE CASCADE
);
CREATE TABLE IF NOT EXISTS "EmergencyContact" (
	"EmergencyContactId"	INTEGER,
	"Relationship"	INTEGER NOT NULL CHECK(0 <= "Relationship" < 4),
	PRIMARY KEY("EmergencyContactId"),
	FOREIGN KEY("EmergencyContactId") REFERENCES "Person"("PersonId") ON DELETE CASCADE ON UPDATE CASCADE
);
CREATE TABLE IF NOT EXISTS "Modifications" (
	"table_name"	TEXT NOT NULL,
	"id_name"	INTEGER NOT NULL,
	"id_value"	INTEGER NOT NULL,
	"action"	TEXT NOT NULL,
	"changed_at"	NUMERIC DEFAULT (strftime('%s', 'now') || substr(strftime('%f', 'now'), 4)),
	PRIMARY KEY("table_name","id_name","id_value") ON CONFLICT REPLACE
);
CREATE TABLE IF NOT EXISTS "Pass" (
	"PassId"	INTEGER,
	"StudentId"	INTEGER NOT NULL,
	PRIMARY KEY("PassId"),
	FOREIGN KEY("StudentId") REFERENCES "Student"("StudentId") ON DELETE RESTRICT ON UPDATE CASCADE
);
CREATE TABLE IF NOT EXISTS "PassAlterations" (
	"PassAlterationId"	INTEGER NOT NULL,
	"PassId"	INTEGER NOT NULL,
	"AlerationCount"	INTEGER NOT NULL,
	"AlterationReason"	TEXT,
	PRIMARY KEY("PassAlterationId"),
	FOREIGN KEY("PassId") REFERENCES "Pass"("PassId") ON UPDATE Cascade ON DELETE Cascade
);
CREATE TABLE IF NOT EXISTS "Person" (
	"PersonId"	INTEGER,
	"FirstName"	TEXT NOT NULL,
	"LastName"	TEXT,
	"PhoneNumber"	TEXT,
	"Email"	TEXT CHECK("Email" LIKE '%@%.%'),
	"IsActive"	INTEGER DEFAULT 1 CHECK(0 <= "IsActive" <= 1),
	PRIMARY KEY("PersonId"),
	CHECK("PhoneNumber" IS NOT NULL OR "Email" IS NOT NULL)
);
CREATE TABLE IF NOT EXISTS "Student" (
	"StudentId"	INTEGER,
	PRIMARY KEY("StudentId"),
	FOREIGN KEY("StudentId") REFERENCES "Person"("PersonId") ON DELETE RESTRICT ON UPDATE CASCADE
);
CREATE TABLE IF NOT EXISTS "StudentEmergencyContacts" (
	"StudentId"	INTEGER,
	"EmergencyContactId"	INTEGER,
	"Relationship"	INTEGER NOT NULL DEFAULT 0,
	PRIMARY KEY("StudentId","EmergencyContactId"),
	FOREIGN KEY("EmergencyContactId") REFERENCES "Person"("PersonId") ON DELETE RESTRICT ON UPDATE CASCADE,
	FOREIGN KEY("StudentId") REFERENCES "Student"("StudentId") ON DELETE CASCADE ON UPDATE CASCADE
);
CREATE TABLE IF NOT EXISTS "StudentHealthConcerns" (
	"StudentId"	INTEGER NOT NULL,
	"HealthConcern"	TEXT NOT NULL,
	UNIQUE("StudentId","HealthConcern"),
	FOREIGN KEY("StudentId") REFERENCES "Student"("StudentId") ON DELETE CASCADE ON UPDATE CASCADE
);
CREATE TABLE IF NOT EXISTS "Term" (
	"TermId"	INTEGER NOT NULL,
	"TermName"	TEXT NOT NULL,
	"StartDate"	TEXT NOT NULL,
	"EndDate"	TEXT NOT NULL,
	"CatchupStartDate"	TEXT,
	"CatchupEndDate"	TEXT,
	PRIMARY KEY("TermId")
);
CREATE TABLE IF NOT EXISTS "TermClasses" (
	"TermId"	INTEGER,
	"ClassId"	INTEGER,
	"ClassCount"	INTEGER NOT NULL,
	PRIMARY KEY("TermId","ClassId"),
	FOREIGN KEY("ClassId") REFERENCES "ClassSchedule"("ClassScheduleId"),
	FOREIGN KEY("TermId") REFERENCES "Term"("TermId")
);
CREATE TABLE IF NOT EXISTS "TermPass" (
	"PassId"	INTEGER,
	"TermId"	INTEGER NOT NULL,
	"ClassId"	INTEGER NOT NULL DEFAULT 1,
	PRIMARY KEY("PassId"),
	FOREIGN KEY("ClassId") REFERENCES "ClassSchedule"("ClassScheduleId") ON DELETE CASCADE ON UPDATE CASCADE,
	FOREIGN KEY("PassId") REFERENCES "Pass"("PassId") ON DELETE CASCADE ON UPDATE CASCADE,
	FOREIGN KEY("TermId") REFERENCES "Term"("TermId") ON DELETE CASCADE ON UPDATE CASCADE
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
CREATE TRIGGER ClassRoll_ondelete AFTER DELETE ON ClassRoll
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('ClassRoll','ClassId',OLD.ClassId,'DELETE');
END;
CREATE TRIGGER ClassRoll_oninsert AFTER INSERT ON ClassRoll
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('ClassRoll','ClassId',NEW.ClassId,'INSERT');
END;
CREATE TRIGGER ClassRoll_onupdate AFTER UPDATE ON ClassRoll
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('ClassRoll','ClassId',OLD.ClassId,'UPDATE');
END;
CREATE TRIGGER ClassSchedule_ondelete AFTER DELETE ON ClassSchedule
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('ClassSchedule','ClassScheduleId',OLD.ClassScheduleId,'DELETE');
END;
CREATE TRIGGER ClassSchedule_oninsert AFTER INSERT ON ClassSchedule
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('ClassSchedule','ClassScheduleId',NEW.ClassScheduleId,'INSERT');
END;
CREATE TRIGGER ClassSchedule_onupdate AFTER UPDATE ON ClassSchedule
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('ClassSchedule','ClassScheduleId',OLD.ClassScheduleId,'UPDATE');
END;
CREATE TRIGGER ClassStudents_ondelete AFTER DELETE ON ClassStudents
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('ClassStudents','ClassId',OLD.ClassId,'DELETE');
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('ClassStudents','StudentId',OLD.StudentId,'DELETE');
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('ClassStudents','PassId',OLD.PassId,'DELETE');
END;
CREATE TRIGGER ClassStudents_oninsert AFTER INSERT ON ClassStudents
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('ClassStudents','ClassId',NEW.ClassId,'INSERT');
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('ClassStudents','StudentId',NEW.StudentId,'INSERT');
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('ClassStudents','PassId',NEW.PassId,'INSERT');
END;
CREATE TRIGGER ClassStudents_onupdate AFTER UPDATE ON ClassStudents
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('ClassStudents','ClassId',OLD.ClassId,'UPDATE');
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('ClassStudents','StudentId',OLD.StudentId,'UPDATE');
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('ClassStudents','PassId',OLD.PassId,'UPDATE');
END;
CREATE TRIGGER DatedPass_ondelete AFTER DELETE ON DatedPass
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('DatedPass','PassId',OLD.PassId,'DELETE');
END;
CREATE TRIGGER DatedPass_oninsert AFTER INSERT ON DatedPass
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('DatedPass','PassId',NEW.PassId,'INSERT');
END;
CREATE TRIGGER DatedPass_onupdate AFTER UPDATE ON DatedPass
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('DatedPass','PassId',OLD.PassId,'UPDATE');
END;
CREATE TRIGGER EmergencyContact_ondelete AFTER DELETE ON EmergencyContact
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('EmergencyContact','EmergencyContactId',OLD.EmergencyContactId,'DELETE');
END;
CREATE TRIGGER EmergencyContact_oninsert AFTER INSERT ON EmergencyContact
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('EmergencyContact','EmergencyContactId',NEW.EmergencyContactId,'INSERT');
END;
CREATE TRIGGER EmergencyContact_onupdate AFTER UPDATE ON EmergencyContact
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('EmergencyContact','EmergencyContactId',OLD.EmergencyContactId,'UPDATE');
END;
CREATE TRIGGER PassAlterations_ondelete AFTER DELETE ON PassAlterations
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('PassAlterations','PassAlterationId',OLD.PassAlterationId,'DELETE');
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('PassAlterations','PassId',OLD.PassId,'DELETE');
END;
CREATE TRIGGER PassAlterations_oninsert AFTER INSERT ON PassAlterations
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('PassAlterations','PassAlterationId',NEW.PassAlterationId,'INSERT');
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('PassAlterations','PassId',NEW.PassId,'INSERT');
END;
CREATE TRIGGER PassAlterations_onupdate AFTER UPDATE ON PassAlterations
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('PassAlterations','PassAlterationId',OLD.PassAlterationId,'UPDATE');
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('PassAlterations','PassId',OLD.PassId,'UPDATE');
END;
CREATE TRIGGER Pass_ondelete AFTER DELETE ON Pass
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('Pass','PassId',OLD.PassId,'DELETE');
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('Pass','StudentId',OLD.StudentId,'DELETE');
END;
CREATE TRIGGER Pass_oninsert AFTER INSERT ON Pass
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('Pass','PassId',NEW.PassId,'INSERT');
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('Pass','StudentId',NEW.StudentId,'INSERT');
END;
CREATE TRIGGER Pass_onupdate AFTER UPDATE ON Pass
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('Pass','PassId',OLD.PassId,'UPDATE');
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('Pass','StudentId',OLD.StudentId,'UPDATE');
END;
CREATE TRIGGER Person_ondelete AFTER DELETE ON Person
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('Person','PersonId',OLD.PersonId,'DELETE');
END;
CREATE TRIGGER Person_oninsert AFTER INSERT ON Person
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('Person','PersonId',NEW.PersonId,'INSERT');
END;
CREATE TRIGGER Person_onupdate AFTER UPDATE ON Person
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('Person','PersonId',OLD.PersonId,'UPDATE');
END;
CREATE TRIGGER StudentEmergencyContacts_ondelete AFTER DELETE ON StudentEmergencyContacts
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('StudentEmergencyContacts','StudentId',OLD.StudentId,'DELETE');
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('StudentEmergencyContacts','EmergencyContactId',OLD.EmergencyContactId,'DELETE');
END;
CREATE TRIGGER StudentEmergencyContacts_oninsert AFTER INSERT ON StudentEmergencyContacts
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('StudentEmergencyContacts','StudentId',NEW.StudentId,'INSERT');
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('StudentEmergencyContacts','EmergencyContactId',NEW.EmergencyContactId,'INSERT');
END;
CREATE TRIGGER StudentEmergencyContacts_onupdate AFTER UPDATE ON StudentEmergencyContacts
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('StudentEmergencyContacts','StudentId',OLD.StudentId,'UPDATE');
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('StudentEmergencyContacts','EmergencyContactId',OLD.EmergencyContactId,'UPDATE');
END;
CREATE TRIGGER StudentHealthConcerns_ondelete AFTER DELETE ON StudentHealthConcerns
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('StudentHealthConcerns','StudentId',OLD.StudentId,'DELETE');
END;
CREATE TRIGGER StudentHealthConcerns_oninsert AFTER INSERT ON StudentHealthConcerns
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('StudentHealthConcerns','StudentId',NEW.StudentId,'INSERT');
END;
CREATE TRIGGER StudentHealthConcerns_onupdate AFTER UPDATE ON StudentHealthConcerns
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('StudentHealthConcerns','StudentId',OLD.StudentId,'UPDATE');
END;
CREATE TRIGGER Student_ondelete AFTER DELETE ON Student
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('Student','StudentId',OLD.StudentId,'DELETE');
END;
CREATE TRIGGER Student_oninsert AFTER INSERT ON Student
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('Student','StudentId',NEW.StudentId,'INSERT');
END;
CREATE TRIGGER Student_onupdate AFTER UPDATE ON Student
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('Student','StudentId',OLD.StudentId,'UPDATE');
END;
CREATE TRIGGER TermClasses_ondelete AFTER DELETE ON TermClasses
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('TermClasses','TermId',OLD.TermId,'DELETE');
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('TermClasses','ClassId',OLD.ClassId,'DELETE');
END;
CREATE TRIGGER TermClasses_oninsert AFTER INSERT ON TermClasses
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('TermClasses','TermId',NEW.TermId,'INSERT');
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('TermClasses','ClassId',NEW.ClassId,'INSERT');
END;
CREATE TRIGGER TermClasses_onupdate AFTER UPDATE ON TermClasses
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('TermClasses','TermId',OLD.TermId,'UPDATE');
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('TermClasses','ClassId',OLD.ClassId,'UPDATE');
END;
CREATE TRIGGER TermPass_ondelete AFTER DELETE ON TermPass
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('TermPass','PassId',OLD.PassId,'DELETE');
END;
CREATE TRIGGER TermPass_oninsert AFTER INSERT ON TermPass
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('TermPass','PassId',NEW.PassId,'INSERT');
END;
CREATE TRIGGER TermPass_onupdate AFTER UPDATE ON TermPass
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('TermPass','PassId',OLD.PassId,'UPDATE');
END;
CREATE TRIGGER Term_ondelete AFTER DELETE ON Term
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('Term','TermId',OLD.TermId,'DELETE');
END;
CREATE TRIGGER Term_oninsert AFTER INSERT ON Term
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('Term','TermId',NEW.TermId,'INSERT');
END;
CREATE TRIGGER Term_onupdate AFTER UPDATE ON Term
BEGIN
    INSERT INTO modifications (table_name, id_name, id_value, action) VALUES ('Term','TermId',OLD.TermId,'UPDATE');
END;
COMMIT;
