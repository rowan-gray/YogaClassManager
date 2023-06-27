using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YogaClassManager.Database.Models;
using YogaClassManager.Models;
using YogaClassManager.Models.Classes;
using YogaClassManager.Models.People;

namespace YogaClassManager.Database
{
    public class ClassesService : DatabaseService
    {
        public ClassesService(DatabaseManager dbManager) : base(dbManager)
        {
        }

        public async Task<List<ClassSchedule>> GetClassesAsync(CancellationToken cancellationToken, List<ClassSchedule> excludedClasses = null, bool returnArchivedClassSchedules = false)
        {
            var query = "SELECT ClassScheduleId, Day, Time, IsActive FROM ClassSchedule WHERE true\n";

            if (excludedClasses is not null && excludedClasses.Count > 0)
            {
                foreach (var excludedClass in excludedClasses)
                {
                    query += $"AND ClassScheduleId <> {excludedClass.Id}\n";
                }
            }

            if (!returnArchivedClassSchedules)
            {
                query += $"AND IsActive = 0";
            }

            var results = await Database.QueryAsync<ClassScheduleMapping>(query);
            var classSchedules = results.Select(mapping => mapping.ToClassSchedule()).ToList();

            return classSchedules;
        }

        internal async Task<List<ClassSchedule>> GetTodaysClassesAsync(CancellationToken cancellationToken, bool returnArchivedClassSchedules = false)
        {
            var query = "SELECT ClassScheduleId, Day, Time, IsActive FROM ClassSchedule\n" +
                $"WHERE Day = {(int)DateTime.Now.DayOfWeek}";

            if (!returnArchivedClassSchedules)
            {
                query += $"\nAND IsActive = 0";
            }

            query += $"\nORDER BY Time ASC";


            var results = await Database.QueryAsync<ClassScheduleMapping>(query);
            var classSchedules = results.Select(mapping => mapping.ToClassSchedule()).ToList();

            return classSchedules;
        }

        internal async Task UpdateClassAsync(CancellationToken cancellationToken, ClassSchedule classSchedule)
        {
            string query = "UPDATE ClassSchedule " +
                $"SET Day={(int)classSchedule.Day}, Time={classSchedule.Time.ToTimeSpan().TotalMinutes}, IsActive={classSchedule.IsArchived} " +
                $"WHERE ClassScheduleId = {classSchedule.Id}";

            await Database.ExecuteAsync(query);
        }

        internal async Task<int> AddClassAsync(CancellationToken cancellationToken, ClassSchedule classSchedule)
        {
            var id = await GetUnusedClassId(cancellationToken);

            string query = "INSERT INTO ClassSchedule " +
            $"VALUES ({id},{(int)classSchedule.Day},{classSchedule.Time.ToTimeSpan().TotalMinutes},{classSchedule.IsArchived})";

            await Database.ExecuteAsync(query);

            return id;
        }

        private async Task<int> GetUnusedClassId(CancellationToken cancellationToken)
        {
            int id;

            do
            {
                id = new Random().Next(0, 1000);
            }
            while (await IsIdInUse(cancellationToken, id));

            return id;
        }

        private async Task<bool> IsIdInUse(CancellationToken cancellationToken, int id)
        {
            string query = "SELECT null FROM ClassSchedule " +
                $"WHERE ClassScheduleId = {id}";

            var results = await Database.QueryScalarsAsync<string>(query);
            return results.Count > 0;
        }

        internal async Task<List<ClassSchedule>> GetUnusedClassesFromTerm(CancellationToken cancellationToken, Term term)
        {
            var query = "SELECT cs.ClassScheduleId, Day, Time, IsActive FROM ClassSchedule cs " +
                    $"WHERE cs.ClassScheduleId NOT IN (SELECT tc.ClassId FROM TermClasses tc WHERE tc.TermId = {term.Id})";

            var results = await Database.QueryAsync<ClassScheduleMapping>(query);
            var classSchedules = results.Select(mapping => mapping.ToClassSchedule()).ToList();
            return classSchedules;
        }

        internal async Task<List<ClassRoll>> GetClassRollsFromClassAsync(CancellationToken cancellationToken, ClassSchedule selectedClass)
        {
            var query = $"SELECT ClassId, ClassScheduleId, Date FROM ClassRoll WHERE ClassScheduleId = {selectedClass.Id}";

            var results = await Database.QueryAsync<ClassRollMapping>(query);

            var classRolls = results.Select(mapping => mapping.ToClassRoll()).ToList();

            foreach (var classRoll in classRolls)
            {
                classRoll.ClassSchedule = await dbManager.ClassesService.GetClassFromIdAsync(cancellationToken, classRoll.ClassSchedule.Id);
                classRoll.StudentEntries = await dbManager.ClassesService.GetClassRollsStudentEntriesFromClassRollId(cancellationToken, classRoll.Id);
            }

            return classRolls;
        }

        internal async Task<ClassRoll> GetLastClassRollAsync(CancellationToken cancellationToken)
        {
            var query = $"SELECT C1.ClassId, C1.ClassScheduleId, C1.Date FROM ClassRoll C1 " +
                $"INNER JOIN ClassSchedule CS ON C1.ClassScheduleId = CS.ClassScheduleID " +
                $"WHERE NOT EXISTS (SELECT C2.Date FROM ClassRoll C2 WHERE C2.Date > C1.Date) " +
                $"ORDER BY CS.TIME DESC LIMIT 1";

            var result = (await Database.QueryAsync<ClassRollMapping>(query)).FirstOrDefault();

            if (result is null)
            {
                throw new ArgumentException("There is no previous class");
            }

            var classRoll = result.ToClassRoll();

            classRoll.ClassSchedule = await dbManager.ClassesService.GetClassFromIdAsync(cancellationToken, classRoll.ClassSchedule.Id);
            classRoll.StudentEntries = await dbManager.ClassesService.GetClassRollsStudentEntriesFromClassRollId(cancellationToken, classRoll.Id);

            return classRoll;
        }

        internal async Task<ClassRoll> GetTodaysClassRollFromClassAsync(CancellationToken cancellationToken, ClassSchedule classSchedule)
        {
            var query = $"SELECT ClassId, ClassScheduleId, Date\n" +
                $"FROM ClassRoll\n" +
                $"WHERE ClassScheduleId = {classSchedule.Id}\n" +
                $"AND Date = '{DateOnly.FromDateTime(DateTime.Now):yyyy-MM-dd}'";

            var result = (await Database.QueryAsync<ClassRollMapping>(query)).FirstOrDefault();

            if (result is null)
            {
                throw new ArgumentException("There is no class roll today");
            }

            var classRoll = result.ToClassRoll();

            classRoll.ClassSchedule = await dbManager.ClassesService.GetClassFromIdAsync(cancellationToken, classRoll.ClassSchedule.Id);
            classRoll.StudentEntries = await dbManager.ClassesService.GetClassRollsStudentEntriesFromClassRollId(cancellationToken, classRoll.Id);

            return classRoll;
        }

        internal async Task<ClassSchedule> GetClassFromIdAsync(CancellationToken cancellationToken, int id)
        {
            var query = "SELECT ClassScheduleId, Day, Time, IsActive FROM ClassSchedule " +
                    $"WHERE ClassScheduleId = {id}";



            var result = (await Database.QueryAsync<ClassScheduleMapping>(query)).FirstOrDefault();

            if (result is null)
            {
                throw new ArgumentException($"There is no class schedule with id {id}");
            }

            var classSchedule = result.ToClassSchedule();

            return classSchedule;
        }

        internal async Task<List<ClassRollEntry>> GetClassRollsStudentEntriesFromClassRollId(CancellationToken cancellationToken, int classId)
        {
            var classRollEntries = new List<ClassRollEntry>();

            var query = "SELECT PersonId, FirstName, LastName, PhoneNumber, Email, IsActive, PassId FROM Person " +
                    "JOIN Student ON Person.PersonId = Student.StudentId " +
                    "JOIN ClassStudents ON ClassStudents.StudentId = Student.StudentId " +
                    $"WHERE ClassStudents.ClassId = {classId}";

            var results = await Database.QueryAsync<ClassRollEntryMapping>(query);

            foreach (var result in results)
            {
                var person = result.ToPerson();
                var student = new Student(person, new(), new(), new());

                // TODO get pass directly
                await dbManager.StudentsService.PopulateStudentsFields(cancellationToken, student);

                var pass = student.Passes.FirstOrDefault(pass => pass.Id == result.PassId);

                if (pass is null)
                {
                    pass = await dbManager.PassesService.GetPassFromId(cancellationToken, result.PassId);
                    student.Passes.Add(pass);
                }

                classRollEntries.Add(new(student, pass));
            }

            return classRollEntries;
        }

        internal async Task<int> AddClassRollAsync(CancellationToken cancellationToken, ClassRoll classRoll)
        {
            await dbManager.BeginTransactionAsync();

            try
            {
                var classId = await GetUnusedClassRollId(cancellationToken);

                var query = "INSERT INTO ClassRoll " +
                    $"VALUES ({classId}, {classRoll.ClassSchedule.Id}, '{classRoll.Date:yyyy-MM-dd}')";

                await Database.ExecuteAsync(query);

                foreach (var studentEntry in classRoll.StudentEntries)
                {
                    var studentEntryQuery = "INSERT INTO ClassStudents " +
                    $"VALUES ({classId}, {studentEntry.Student.Id}, {(studentEntry.Pass is not null ? studentEntry.Pass.Id : "NULL")})";

                    await Database.ExecuteAsync(studentEntryQuery);
                }

                await dbManager.CommitAsync();

                return classId;
            }
            catch
            {
                await dbManager.AbortAsync();
                throw;
            }
        }

        internal async Task UpdateClassRollDetailsAsync(CancellationToken cancellationToken, ClassRoll classRoll)
        {
            await dbManager.BeginTransactionAsync();
            try
            {
                var query = "UPDATE ClassRoll " +
                    $"SET Date='{classRoll.Date:yyyy-MM-dd}' " +
                    $"WHERE ClassId={classRoll.Id}";
                await Database.ExecuteAsync(query);
            }
            catch
            {
                await dbManager.AbortAsync();
                throw;
            }
            await dbManager.CommitAsync();
        }

        internal async Task UpdateClassRollAsync(CancellationToken cancellationToken, ClassRoll classRoll)
        {
            await dbManager.BeginTransactionAsync();
            try
            {
                var query = "UPDATE ClassRoll " +
                    $"SET Date='{classRoll.Date:yyyy-MM-dd}' " +
                    $"WHERE ClassId={classRoll.Id}";

                await Database.ExecuteAsync(query);

                var removeEntriesQuery = "DELETE FROM ClassStudents " +
                    $"WHERE ClassId={classRoll.Id}";

                await Database.ExecuteAsync(removeEntriesQuery);

                foreach (var studentEntry in classRoll.StudentEntries)
                {
                    var studentEntryQuery = "INSERT INTO ClassStudents " +
                    $"VALUES ({classRoll.Id}, {studentEntry.Student.Id}, {studentEntry.Pass.Id})";

                    await Database.ExecuteAsync(studentEntryQuery);
                }

                await dbManager.CommitAsync();
            }
            catch
            {
                await dbManager.AbortAsync();
                throw;
            }
        }
        internal async Task UpdateClassRollEntryAsync(CancellationToken cancellationToken, int classRollId, ClassRollEntry classRollEntry)
        {
            await dbManager.BeginTransactionAsync();
            try
            {
                var studentEntryQuery = "UPDATE ClassStudents\n" +
                $"SET PassId = {classRollEntry.Pass.Id}\n" +
                $"WHERE ClassId = {classRollId} AND StudentId = {classRollEntry.Student.Id}";

                await Database.ExecuteAsync(studentEntryQuery);
            }
            catch
            {
                await dbManager.AbortAsync();
                throw;
            }
            await dbManager.CommitAsync();
        }

        internal async Task AddStudentToClassRollAsync(CancellationToken cancellationToken, int classRollId, ClassRollEntry classRollEntry)
        {
            await dbManager.BeginTransactionAsync();
            try
            {
                var studentEntryQuery = "INSERT INTO ClassStudents " +
                $"VALUES ({classRollId}, {classRollEntry.Student.Id}, {classRollEntry.Pass.Id})";

                await Database.ExecuteAsync(studentEntryQuery);

                await dbManager.CommitAsync();
            }
            catch
            {
                await dbManager.AbortAsync();
                throw;
            }
        }

        internal async Task RemoveStudentFromClassRollAsync(CancellationToken cancellationToken, int classRollId, ClassRollEntry classRollEntry)
        {
            await dbManager.BeginTransactionAsync();
            try
            {
                var removeEntriesQuery = "DELETE FROM ClassStudents " +
                    $"WHERE ClassId={classRollId} AND StudentId={classRollEntry.Student.Id} AND PassId={classRollEntry.Pass.Id}";

                await Database.ExecuteAsync(removeEntriesQuery);

                await dbManager.CommitAsync();
            }
            catch
            {
                await dbManager.AbortAsync();
                throw;
            }
        }

        internal async Task RemoveClassRollAsync(CancellationToken cancellationToken, ClassRoll classRoll)
        {
            await dbManager.BeginTransactionAsync();
            try
            {
                var removeEntriesQuery = "DELETE FROM ClassStudents " +
                    $"WHERE ClassId={classRoll.Id}";

                await Database.ExecuteAsync(removeEntriesQuery);

                var query = "DELETE FROM ClassRoll\n" +
                    $"WHERE ClassId={classRoll.Id}";

                await Database.ExecuteAsync(query);

                await dbManager.CommitAsync();
            }
            catch
            {
                await dbManager.AbortAsync();
                throw;
            }
        }

        private async Task<int> GetUnusedClassRollId(CancellationToken cancellationToken)
        {
            int id;

            do
            {
                id = new Random().Next(0, 1000);
            }
            while (await IsClassRollIdInUse(cancellationToken, id));

            return id;
        }

        private async Task<bool> IsClassRollIdInUse(CancellationToken cancellationToken, int id)
        {
            string query = "SELECT null FROM ClassRoll " +
                $"WHERE ClassId = {id}";

            var results = await Database.QueryScalarsAsync<string>(query);
            return results.Count > 0;
        }

        internal async Task<List<ClassSchedule>> GetUpdatedClassSchedule(CancellationToken cancellationToken, long timestamp, bool returnInactiveClass = false)
        {
            var query = "SELECT ClassScheduleId, Day, Time, IsActive\n" +
                "FROM Modifications M\n" +
                "INNER JOIN ClassSchedule M ON M.id_value = M.ClassScheduleId\n" +
                $"WHERE table_name = 'ClassSchedule' AND changed_at >= {timestamp}";

            if (!returnInactiveClass)
            {
                query += " AND IsActive = 0";
            }

            var results = await Database.QueryAsync<ClassScheduleMapping>(query);
            var classSchedules = results.Select(mapping => mapping.ToClassSchedule()).ToList();

            return classSchedules;
        }

        internal async Task<List<int>> GetDeletedClassScheduleIds(CancellationToken cancellationToken, long timestamp)
        {
            var query = "SELECT id_value\n" +
                "FROM Modifications M\n" +
                $"WHERE table_name = 'ClassSchedule' AND changed_at >= {timestamp} AND action = 'DELETE'";

            var results = await Database.QueryScalarsAsync<int>(query);

            return results;
        }


        internal async Task RemoveClassSchedule(CancellationToken cancellationToken, ClassSchedule classSchedule)
        {
            await dbManager.BeginTransactionAsync();
            try
            {
                var removeTermReferencesQuery = "DELETE FROM TermClasses " +
                    $"WHERE ClassId={classSchedule.Id}";

                await Database.ExecuteAsync(removeTermReferencesQuery);

                var removeEntriesQuery = "DELETE FROM ClassSchedule " +
                    $"WHERE ClassScheduleId={classSchedule.Id}";


                await Database.ExecuteAsync(removeEntriesQuery);

                await dbManager.CommitAsync();
            }
            catch
            {
                await dbManager.AbortAsync();
                throw;
            }
        }
    }
}
