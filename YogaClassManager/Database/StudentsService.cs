using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YogaClassManager.Database.Models;
using YogaClassManager.Models;
using YogaClassManager.Models.Passes;
using YogaClassManager.Models.People;

namespace YogaClassManager.Database
{
    public class StudentsService : DatabaseService
    {
        public StudentsService(DatabaseManager dbManager) : base(dbManager)
        {
        }

        public async Task<Student> GetStudentAsync(CancellationToken cancellationToken, int id, bool greedyLoad = false, bool returnInactivePasses = false)
        {
            var result = (await dbManager.Database.QueryAsync<StudentMapping>("SELECT PersonId, FirstName, LastName, PhoneNumber, Email, IsActive FROM Person " +
                "JOIN Student ON Person.PersonId = Student.StudentId " +
                $"WHERE PersonId = {id}")).FirstOrDefault();

            if (result is null)
            {
                throw new ArgumentException($"Student with id {id} does not exist");
            }

            var student = result.ToStudent();

            if (greedyLoad)
            {
                await PopulateStudentsFields(cancellationToken, student, returnInactivePasses);
            }

            return student;
        }

        public async Task<List<Student>> GetStudentsAsync(CancellationToken cancellationToken, bool returnInactiveStudents = false, bool greedyLoad = false, bool returnInactivePasses = false)
        {
            var result = (await dbManager.Database.QueryAsync<StudentMapping>("SELECT PersonId, FirstName, LastName, PhoneNumber, Email, IsActive FROM Person " +
                "JOIN Student ON Person.PersonId = Student.StudentId " +
                $"WHERE {returnInactiveStudents} OR IsActive = {!returnInactiveStudents} " +
                $"ORDER BY Firstname ASC"));

            List<Student> students = result.Select(result => result.ToStudent()).ToList();

            if (greedyLoad)
            {
                foreach (var student in students)
                {
                    await PopulateStudentsFields(cancellationToken, student, returnInactivePasses);
                }
            }

            return students;
        }

        public async Task<List<Student>> SearchStudentsAsync(CancellationToken cancellationToken,
                                                             string searchQuery,
                                                             IEnumerable<Student> excludedStudents = null,
                                                             bool returnInactiveStudents = false,
                                                             bool greedyLoad = false,
                                                             bool returnInactivePasses = false,
                                                             int? limit = null)
        {
            var selectQuery = "SELECT PersonId, FirstName, LastName, PhoneNumber, Email, IsActive FROM Person " +
                    "JOIN Student ON Person.PersonId = Student.StudentId " +
                    $"WHERE (lower(FirstName) LIKE lower('{searchQuery.Replace("'", "''")}%') OR lower(LastName) LIKE lower('{searchQuery.Replace("'", "''")}%') " +
                    $"OR lower(FirstName || ' ' || LastName) LIKE lower('{searchQuery.Replace("'", "''")}%')) " +
                    $"AND ({returnInactiveStudents} OR IsActive = {!returnInactiveStudents})";

            if (excludedStudents is not null)
            {
                foreach (var student in excludedStudents)
                {
                    selectQuery += $" AND PersonId != {student.Id}";
                }
            }

            selectQuery += $" ORDER BY Firstname ASC";

            if (limit is not null)
            {
                selectQuery += $" LIMIT {limit}";
            }

            var result = await dbManager.Database.QueryAsync<StudentMapping>(selectQuery);
            List<Student> students = result.Select(result => result.ToStudent()).ToList();

            if (greedyLoad)
            {
                foreach (var student in students)
                {
                    await PopulateStudentsFields(cancellationToken, student, returnInactivePasses);
                }
            }

            return students;
        }

        public async Task<List<EmergencyContact>> GetStudentsEmergencyContacts(CancellationToken cancellationToken, int id)
        {
            var result = await Database.QueryAsync<EmergencyContactMapping>("SELECT Person.PersonId, FirstName, LastName, PhoneNumber, Email, IsActive, " +
                "StudentEmergencyContacts.StudentId, Relationship FROM StudentEmergencyContacts " +
                "JOIN Person ON Person.PersonId = StudentEmergencyContacts.EmergencyContactId " +
                $"WHERE StudentEmergencyContacts.StudentId = {id}");

            List<EmergencyContact> emergencyContacts = result.Select(result => result.ToEmergencyContact()).ToList();

            return emergencyContacts;
        }

        public async Task<List<Pass>> GetStudentsPasses(CancellationToken cancellationToken, int id, bool retrieveExpiredPasses = false, bool retrieveFulfilledPasses = false)
        {
            var datedPasses = await GetStudentsDatedPasses(cancellationToken, id, retrieveExpiredPasses, retrieveFulfilledPasses);
            var termPasses = await GetStudentsTermPasses(cancellationToken, id, retrieveExpiredPasses, retrieveFulfilledPasses);
            var casualPasses = await GetStudentsCasualPasses(cancellationToken, id, retrieveFulfilledPasses);

            var passes = new List<Pass>();
            passes.AddRange(datedPasses);
            passes.AddRange(termPasses);
            passes.AddRange(casualPasses);

            return passes;
        }

        private async Task<List<CasualPass>> GetStudentsCasualPasses(CancellationToken cancellationToken, int id, bool retrieveFulfilledPasses)
        {
            var casualPasses = new List<CasualPass>();


            var query = "SELECT Pass.PassId, Pass.StudentId, ClassCount, TimesUsed " +
                "FROM Pass " +
                "JOIN CasualPass ON Pass.PassId = CasualPass.PassId " +
                "JOIN PassUses on PassUses.PassId = Pass.PassId " +
                "JOIN PassesTotalClasses on PassesTotalClasses.PassId = Pass.PassId " +
                $"WHERE Pass.StudentId = {id}";

            if (!retrieveFulfilledPasses)
            {
                query += " AND TimesUsed < NumberOfClasses";
            }

            var results = await Database.QueryAsync<CasualPassMapping>(query);

            foreach (var result in results)
            {
                var alterations = await dbManager.PassesService.GetAlterationsFromPassId(cancellationToken, result.PassId);
                var pass = new Pass(result.PassId, result.StudentId, result.TimesUsed, new(alterations));
                var datedPass = new CasualPass(pass, result.ClassCount);

                casualPasses.Add(datedPass);
            }

            return casualPasses;
        }

        public async Task<List<TermPass>> GetStudentsTermPasses(CancellationToken cancellationToken, int id, bool retrieveExpiredPasses, bool retrieveFulfilledPasses)
        {
            var termPasses = new List<TermPass>();

            var query = "SELECT Pass.PassId AS PassId, Pass.StudentId AS StudentId, " +
                    "TermPass.TermId AS TermId, TermPass.ClassId AS ClassId, TimesUsed " +
                    "FROM Pass " +
                    "JOIN TermPass ON Pass.PassId = TermPass.PassId " +
                    "JOIN Term on TermPass.TermId = Term.TermId " +
                    "JOIN PassUses on PassUses.PassId = Pass.PassId " +
                    "JOIN PassesTotalClasses on PassesTotalClasses.PassId = Pass.PassId " +
                    $"WHERE Pass.StudentId = {id}";

            if (!retrieveExpiredPasses)
            {
                query += " AND Term.EndDate >= date('now')";
            }

            if (!retrieveFulfilledPasses)
            {
                query += " AND TimesUsed < NumberOfClasses";
            }

            var results = await Database.QueryAsync<TermPassMapping>(query);

            foreach (var result in results)
            {
                var term = await dbManager.TermsService.GetTermFromIdAsync(cancellationToken, result.TermId);
                var termClassSchedule = term.Classes.FirstOrDefault(termClassSchedule => termClassSchedule.ClassSchedule.Id == result.ClassId);
                var alterations = await dbManager.PassesService.GetAlterationsFromPassId(cancellationToken, result.PassId);

                var pass = new Pass(result.PassId, result.StudentId, result.TimesUsed, new(alterations));

                termPasses.Add(new TermPass(pass, term, termClassSchedule));
            }

            return termPasses;
        }

        public async Task<List<DatedPass>> GetStudentsDatedPasses(CancellationToken cancellationToken, int id, bool retrieveExpiredPasses, bool retrieveFulfilledPasses)
        {
            var datedPasses = new List<DatedPass>();


            var query = "SELECT Pass.PassId, Pass.StudentId, ClassCount, StartDate, EndDate, TimesUsed " +
                "FROM Pass " +
                "JOIN DatedPass ON Pass.PassId = DatedPass.PassId " +
                "JOIN PassUses on PassUses.PassId = Pass.PassId " +
                "JOIN PassesTotalClasses on PassesTotalClasses.PassId = Pass.PassId " +
                $"WHERE Pass.StudentId = {id}";

            if (!retrieveExpiredPasses)
            {
                query += " AND DatedPass.EndDate >= date('now')";
            }

            if (!retrieveFulfilledPasses)
            {
                query += " AND TimesUsed < NumberOfClasses";
            }

            var results = await Database.QueryAsync<DatedPassMapping>(query);

            foreach (var result in results)
            {
                var alterations = await dbManager.PassesService.GetAlterationsFromPassId(cancellationToken, result.PassId);
                var pass = new Pass(result.PassId, result.StudentId, result.TimesUsed, new(alterations));
                var datedPass = new DatedPass(pass, result.ClassCount, StringToDateOnly(result.StartDate), StringToDateOnly(result.EndDate));

                datedPasses.Add(datedPass);
            }

            return datedPasses;
        }
        public async Task<List<string>> GetStudentsHealthConcernsAsync(CancellationToken cancellationToken, int id)
        {
            var healthConcerns = new List<string>();


            var results = await Database.QueryAsync<StudentHealthConcernMapping>("SELECT HealthConcern " +
                    "FROM StudentHealthConcerns " +
                    $"WHERE StudentId = {id}");


            foreach ( var result in results) {
                healthConcerns.Add(result.HealthConcern);    
            }

            return healthConcerns;
        }

        //private async Task<Student> entityToStudentConversion(DbDataReader dataReader, CancellationToken cancellationToken, bool greedyLoad, bool retrieveExpiredPasses = false)
        //{
        //    var person = ReaderHelpers.entityToPersonConversion(dataReader);
        //    List<EmergencyContact> emergencyContacts = null;
        //    List<PassMapping> passes = null;
        //    List<string> healthConcerns = null;
        //    if (greedyLoad)
        //    {
        //        emergencyContacts = await GetStudentsEmergencyContacts(db, cancellationToken, person.Id);
        //        passes = await GetStudentsPasses(db, cancellationToken, person.Id, retrieveExpiredPasses);
        //        healthConcerns = await GetStudentsHealthConcernsAsync(db, cancellationToken, person.Id);
        //    }

        //    return new(person, passes, emergencyContacts, healthConcerns);
        //}

        internal async Task AddEmergencyContactAsync(CancellationToken cancellationToken, EmergencyContact emergencyContact)
        {
            await dbManager.BeginTransactionAsync();

            try
            {
                if (emergencyContact.Id == -1)
                {
                    var newId = await dbManager.PeopleService.AddPersonAsync(cancellationToken, emergencyContact);
                    emergencyContact.Id = newId;
                }

                string query = "INSERT INTO StudentEmergencyContacts (StudentId, EmergencyContactId, Relationship)\n" +
                    $"VALUES ({emergencyContact.StudentId},{emergencyContact.Id},{((int)emergencyContact.Relationship)})";

                await Database.ExecuteAsync(query);

                await dbManager.CommitAsync();
            }
            catch
            {
                await dbManager.AbortAsync();
                throw;
            }
        }
        internal async Task RemoveEmergencyContactAsync(CancellationToken cancellationToken, EmergencyContact emergencyContact)
        {
            string query = "DELETE FROM StudentEmergencyContacts " +
                $"WHERE StudentId={emergencyContact.StudentId} AND EmergencyContactId={emergencyContact.Id}";

            await Database.ExecuteAsync(query);
        }

        internal async Task<int> AddStudentAsync(CancellationToken cancellationToken, Student student)
        {
            await dbManager.BeginTransactionAsync();
            try
            {
                var id = await dbManager.PeopleService.AddPersonAsync(cancellationToken, student);

                string query = "INSERT INTO Student " +
                $"VALUES ({id})";

                await Database.ExecuteAsync(query);

                // adds passes
                foreach (var pass in student.Passes)
                {
                    pass.StudentId = id;

                    await dbManager.PassesService.AddPassAsync(cancellationToken, pass);
                }

                // adds emergencyContacts
                foreach (var emergencyContact in student.EmergencyContacts)
                {
                    emergencyContact.StudentId = id;

                    await dbManager.StudentsService.AddEmergencyContactAsync(cancellationToken, emergencyContact);
                }

                // adds healthConcerns
                foreach (var healthConcern in student.HealthConcerns)
                {
                    student.Id = id;

                    await dbManager.StudentsService.AddHealthConcern(cancellationToken, student, healthConcern);
                }

                await dbManager.CommitAsync();
                return id;
            }
            catch
            {
                await dbManager.AbortAsync();
                throw;
            }
        }

        internal async Task UpdateEmergencyContactAsync(CancellationToken cancellationToken, EmergencyContact emergencyContact)
        {
            string query = "UPDATE StudentEmergencyContacts " +
                $"SET Relationship='{(int)emergencyContact.Relationship}' " +
                $"WHERE StudentId = {emergencyContact.StudentId} AND EmergencyContactId = {emergencyContact.Id}";

            await Database.ExecuteAsync(query);
        }

        internal async Task AddHealthConcern(CancellationToken cancellationToken, Student student, string healthConcern)
        {
            string query = "INSERT INTO StudentHealthConcerns " +
                $"VALUES ({student.Id}, '{healthConcern.Replace("'", "''")}')";

            await Database.ExecuteAsync(query);
        }

        internal async Task RemoveHealthConcern(CancellationToken cancellationToken, Student student, string healthConcern)
        {
            string query = "DELETE FROM StudentHealthConcerns " +
                $"WHERE StudentId={student.Id} AND healthConcern='{healthConcern.Replace("'", "''")}'";

            await Database.ExecuteAsync(query);
        }

        internal async Task<List<Student>> GetUpdatedStudents(CancellationToken cancellationToken, long timestamp, bool returnInactiveStudents = false, bool greedyLoad = false, bool retrieveInactivePasses = false)
        {
            var query = "SELECT StudentId, FirstName, LastName, PhoneNumber, Email, IsActive\n" +
                "FROM Modifications M\n" +
                "INNER JOIN Student S ON M.id_value = S.StudentId\n" +
                "INNER JOIN Person P ON S.StudentId = P.PersonId\n" +
                $"WHERE table_name = 'Person' AND changed_at >= {timestamp}";

            if (!returnInactiveStudents)
            {
                query += " AND IsActive = 1";
            }

            var results = await Database.QueryAsync<StudentMapping>(query);
            List<Student> students = results.Select(result => result.ToStudent()).ToList();

            if (greedyLoad)
            {
                foreach (var student in students)
                {
                    await PopulateStudentsFields(cancellationToken, student, retrieveInactivePasses);
                }
            }

            return students;
        }
        internal async Task<List<Student>> GetUpdatedStudentsMatchingQuery(CancellationToken cancellationToken, long timestamp, string searchQuery, bool returnInactiveStudents = false, bool greedyLoad = false, bool retrieveInactivePasses = false)
        {
            var query = "SELECT PersonId, FirstName, LastName, PhoneNumber, Email, IsActive\n" +
                "FROM Modifications M\n" +
                "INNER JOIN Student S ON M.id_value = S.StudentId\n" +
                "INNER JOIN Person P ON S.StudentId = P.PersonId\n" +
                $"WHERE table_name = 'Person' AND changed_at >= {timestamp} AND " +
                $"(lower(FirstName) LIKE lower('{searchQuery}%') OR " +
                $"lower(LastName) LIKE lower('{searchQuery}%') " +
                $"OR lower(FirstName || ' ' || LastName) LIKE lower('{searchQuery}%'))";

            if (!returnInactiveStudents)
            {
                query += " AND IsActive = 1";
            }

            var results = await Database.QueryAsync<StudentMapping>(query);
            List<Student> students = results.Select(result => result.ToStudent()).ToList();

            if (greedyLoad)
            {
                foreach (var student in students)
                {
                    await PopulateStudentsFields(cancellationToken, student, retrieveInactivePasses);
                }
            }

            return students;
        }

        internal async Task<List<int>> GetDeletedStudentsIds(CancellationToken cancellationToken, long timestamp)
        {
            var query = "SELECT id_value\n" +
                "FROM Modifications M\n" +
                $"WHERE table_name = 'Student' AND changed_at >= {timestamp} AND action = 'DELETE'";

            var deletedStudentsIds = await Database.QueryScalarsAsync<int>(query);

            return deletedStudentsIds;
        }

        internal async Task<bool> HaveFieldsChanged(CancellationToken cancellationToken, Student student, long timestamp)
        {
            return await HavePassesChanged(cancellationToken, student, timestamp)
                || await HaveEmergencyContactsChanged(cancellationToken, student, timestamp)
                || await HaveHealthConcernsChanged(cancellationToken, student, timestamp);
        }

        private async Task<bool> HavePassesChanged(CancellationToken cancellationToken, Student student, long timestamp)
        {
            // checks if a pass has been used since timestamp
            var query = "SELECT null\n" +
                "FROM Modifications M\n" +
                "WHERE table_name IN ('ClassStudents')\n" +
                $"AND changed_at >= {timestamp}\n" +
                $"AND M.id_name = 'StudentId'\n" +
                $"AND M.id_value = {student.Id}";
            var results = await Database.QueryScalarsAsync<string>(query);

            if (results.Count > 0)
            {
                return true;
            }

            // checks if a modification has occured to a pass linked to the student
            var query2 = "SELECT null\n" +
                "FROM Modifications M\n" +
                "INNER JOIN Pass P ON P.PassId = M.id_value\n" +
                "WHERE table_name IN ('Pass','DatedPass','TermPass') AND id_name='PassId'\n" +
                $"AND changed_at >= {timestamp} AND P.StudentId = {student.Id}";

            var results2 = await Database.QueryScalarsAsync<string>(query2);

            if (results2.Count > 0)
            {
                return true;
            }

            // checks if a pass has had any alterations
            var query3 = "SELECT null\n" +
                "FROM Modifications M\n" +
                "INNER JOIN Pass P ON M.id_value = P.PassId\n" +
                "WHERE table_name = 'PassAlterations' AND id_name='PassId'\n" +
                $"AND changed_at >= {timestamp} AND P.StudentId = {student.Id}";

            var results3 = await Database.QueryScalarsAsync<string>(query3);

            if (results3.Count > 0)
            {
                return true;
            }

            // checks if a pass has been deleted or added
            var query4 = "SELECT null\n" +
                "FROM Modifications M\n" +
                "WHERE table_name == 'Pass' AND id_name='StudentId'\n" +
                $"AND changed_at >= {timestamp} AND id_value = {student.Id}";

            var results4 = await Database.QueryScalarsAsync<string>(query4);

            return results4.Count > 0;
        }

        private async Task<bool> HaveEmergencyContactsChanged(CancellationToken cancellationToken, Student student, long timestamp)
        {
            // checks if there has been a change in relationship
            var query = "SELECT null\n" +
                "FROM Modifications M\n" +
                "WHERE table_name = 'StudentEmergencyContacts' AND M.id_name = 'StudentId'" +
                $"AND changed_at >= {timestamp} AND M.id_value = {student.Id}";

            var results = await Database.QueryScalarsAsync<string>(query);

            if (results.Count > 0)
            {
                return true;
            }

            // checks if the underlying emergency contact has changed
            var query2 = "SELECT null\n" +
                "FROM Modifications M\n" +
                "INNER JOIN EmergencyContact EC ON M.id_value = EC.EmergencyContactId\n" +
                "INNER JOIN StudentEmergencyContacts SEC ON EC.EmergencyContactId = SEC.EmergencyContactId\n" +
                "WHERE table_name IN ('EmergencyContact','Person') AND id_name = 'EmergencyContactId'" +
                $"AND changed_at >= {timestamp} AND SEC.StudentId = {student.Id}";

            var results2 = await Database.QueryScalarsAsync<string>(query2);

            return results2.Count > 0;
        }

        private async Task<bool> HaveHealthConcernsChanged(CancellationToken cancellationToken, Student student, long timestamp)
        {
            var query = "SELECT null\n" +
                "FROM Modifications M\n" +
                "WHERE table_name = 'StudentHealthConcerns'" +
                $"AND changed_at >= {timestamp} AND id_value = {student.Id}";

            var results = await Database.QueryScalarsAsync<string>(query);

            return results.Count > 0;
        }

        internal async Task<Student> PopulateStudentsFields(CancellationToken cancellationToken, Student student, bool retrieveExpiredPasses = false, bool retrieveFulfilledPasses = false)
        {
            student.EmergencyContacts = new(await GetStudentsEmergencyContacts(cancellationToken, student.Id));
            student.Passes = new(await GetStudentsPasses(cancellationToken, student.Id, retrieveExpiredPasses, retrieveFulfilledPasses));
            student.HealthConcerns = new(await GetStudentsHealthConcernsAsync(cancellationToken, student.Id));
            return student;
        }
    }
}
