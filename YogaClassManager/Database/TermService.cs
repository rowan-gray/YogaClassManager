using Microsoft.Maui.Controls.Compatibility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YogaClassManager.Database.Models;
using YogaClassManager.Models.Classes;

namespace YogaClassManager.Database
{
    public class TermService : DatabaseService
    {
        public TermService(DatabaseManager dbManager) : base(dbManager)
        {
        }

        public async Task<List<Term>> GetTermsAsync(CancellationToken cancellationToken, bool retrieveCompletedTerms = false, bool greedyLoad = true)
        {
            var query = "SELECT TermId, TermName, StartDate, EndDate, CatchupStartDate, CatchupEndDate " +
                    "FROM Term";

            if (!retrieveCompletedTerms)
            {
                query += "\nWHERE EndDate >= Date('now') OR (CatchupEndDate IS NOT NULL AND CatchupEndDate >= DATE('now'))";
            }

            var results = await Database.QueryAsync<TermMapping>(query);
            var terms = results.Select(mapping => mapping.ToTerm()).ToList();

            if (greedyLoad)
            {
                foreach (var term in terms)
                {
                    await PopulateTermsFields(cancellationToken, term);
                }
            }

            return terms;
        }

        public async Task<Term> GetTermFromIdAsync(CancellationToken cancellationToken, int id, bool greedyLoad = true)
        {
            var query = "SELECT TermId, TermName, StartDate, EndDate, CatchupStartDate, CatchupEndDate " +
                        "FROM Term " +
                        $"WHERE TermId = {id}";

            var result = (await Database.QueryAsync<TermMapping>(query)).FirstOrDefault();

            if (result is null)
            {
                throw new ArgumentException($"No term exists with id {id}");
            }

            var term = result.ToTerm();

            if (greedyLoad)
            {
                await PopulateTermsFields(cancellationToken, term);
            }

            return term;
        }

        internal async Task UpdateTermAsync(CancellationToken cancellationToken, Term term)
        {
            string query = "UPDATE Term " +
                $"SET TermName='{term.Name}', StartDate='{term.StartDate:yyyy-MM-dd}', EndDate='{term.EndDate:yyyy-MM-dd}', " +
                $"CatchupStartDate = {NullableDateOnlyToSQLString(term.CatchupStartDate)}, CatchupEndDate = {NullableDateOnlyToSQLString(term.CatchupEndDate)} " +
                $"WHERE TermId = {term.Id}";

            await Database.ExecuteAsync(query);
        }

        private string NullableDateOnlyToSQLString(DateOnly? dateOnly)
        {
            return dateOnly.HasValue ? $"'{dateOnly.Value:yyyy-MM-dd}'" : "NULL";
        }

        internal async Task<int> AddTermAsync(CancellationToken cancellationToken, Term term)
        {
            var id = await GetUnusedTermId(cancellationToken);

            string query = "INSERT INTO Term " +
            $"VALUES ({id},'{term.Name}','{term.StartDate:yyyy-MM-dd}','{term.EndDate:yyyy-MM-dd}',{NullableDateOnlyToSQLString(term.CatchupStartDate)},{NullableDateOnlyToSQLString(term.CatchupEndDate)})";
            
            await Database.ExecuteAsync(query);

            return id;
        }

        private async Task<int> GetUnusedTermId(CancellationToken cancellationToken)
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
            string query = "SELECT null FROM Term " +
                $"WHERE TermId = {id}";

            var results = await Database.QueryScalarsAsync<string>(query);
            return results.Count > 0;
        }

        internal async Task<List<TermClassSchedule>> GetTermClassesFromId(CancellationToken cancellationToken, int termId)
        {
            var query = "SELECT TC.ClassId AS ClassId, Day, Time, IsActive, ClassCount, Uses " +
                    "FROM TermClasses TC " +
                    "INNER JOIN ClassSchedule CS " +
                    "ON TC.ClassId = CS.ClassScheduleId " +
                    "INNER JOIN TermClassUses TCU " +
                    "ON TCU.ClassId = TC.ClassId AND TCU.TermId = TC.TermId " +
                    $"WHERE TCU.TermId = {termId}";


            var results = await Database.QueryAsync<TermClassScheduleMapping>(query);
            var termClassSchedules = results.Select(mapping => mapping.ToTermClassSchedule()).ToList();

            return termClassSchedules;
        }

        internal async Task AddClassAsync(CancellationToken cancellationToken, int termId, TermClassSchedule termClassSchedule)
        {
            string query = "INSERT OR REPLACE INTO TermClasses " +
                $"VALUES ({termId},{termClassSchedule.ClassSchedule.Id}, {termClassSchedule.ClassCount})";

            await Database.ExecuteAsync(query);

        }

        internal async Task SaveTermsClassesAsync(CancellationToken cancellationToken, int termId, List<TermClassSchedule> termClassSchedules)
        {
            if (termClassSchedules.Count <= 0)
                return;
            await dbManager.BeginTransactionAsync();

            try
            {
                string query = "REPLACE INTO TermClasses VALUES\n";
                foreach (var termClassSchedule in termClassSchedules)
                {
                    query += $"({termId}, {termClassSchedule.ClassSchedule.Id}, {termClassSchedule.ClassCount}),\n";
                }
                query = query.Substring(0, query.Length - 2);

                await Database.ExecuteAsync(query);

                await dbManager.CommitAsync();
            }
            catch
            {
                await dbManager.AbortAsync();
                throw;
            }
        }

        internal async Task RemoveClassAsync(CancellationToken cancellationToken, Term term, ClassSchedule classSchedule)
        {
            string query = "DELETE FROM TermClasses " +
                    $"WHERE TermId={term.Id} AND ClassId={classSchedule.Id}";

            await Database.ExecuteAsync(query);
        }

        internal async Task<List<Term>> GetUpdatedTermsAsync(CancellationToken cancellationToken, long timestamp, bool retrieveCompletedTerms = false, bool greedyLoad = true)
        {
            var query = "SELECT TermId, TermName, StartDate, EndDate, CatchupStartDate, CatchupEndDate\n" +
                "FROM Modifications M\n" +
                "INNER JOIN Term T ON M.id_value = T.TermId\n" +
                $"WHERE table_name = 'Term' AND changed_at >= {timestamp}";

            if (!retrieveCompletedTerms)
            {
                query += "\nAND EndDate >= Date('now') OR (CatchupEndDate IS NOT NULL AND CatchupEndDate >= DATE('now'))";
            }

            var results = await Database.QueryAsync<TermMapping>(query);
            var terms = results.Select(mapping => mapping.ToTerm()).ToList();

            if (greedyLoad)
            {
                foreach (var term in terms)
                {
                    await PopulateTermsFields(cancellationToken, term);
                }
            }

            return terms;
        }

        internal async Task<List<int>> GetDeletedTermsAsync(CancellationToken cancellationToken, long timestamp)
        {
            var query = "SELECT id_value\n" +
                "FROM Modifications M\n" +
                $"WHERE table_name = 'Term' AND changed_at >= {timestamp} AND action = 'DELETE'";

            var results = await Database.QueryScalarsAsync<int>(query);

            return results;
        }
        internal async Task<bool> HasTermChanged(CancellationToken cancellationToken, Term term, long timestamp)
        {
            // checks if a class has been linked or unlinked from the term
            var query = "SELECT null\n" +
                "FROM Modifications M\n" +
                "WHERE table_name IN ('TermClasses')\n" +
                $"AND changed_at >= {timestamp}\n" +
                $"AND M.id_name = 'TermId'\n" +
                $"AND M.id_value = {term.Id}";
            var results = await Database.QueryScalarsAsync<string>(query);

            if (results.Count > 0)
            {
                return true;
            }

            // checks if a linked class has been updated
            var query2 = "SELECT null\n" +
                "FROM Modifications M\n" +
                "INNER JOIN TermClasses TC ON M.id_value = TC.ClassId\n" +
                "WHERE M.table_name IN ('ClassSchedule')\n" +
                $"AND changed_at >= {timestamp}\n" +
                $"AND M.id_name = 'ClassId'\n" +
                $"AND TC.TermId = {term.Id}";
            var results2 = await Database.QueryScalarsAsync<string>(query2);

            return results2.Count > 0;
        }

        internal async Task<Term> PopulateTermsFields(CancellationToken cancellationToken, Term term)
        {
            term.Classes.Clear();
            foreach (var termClassSchedule in await GetTermClassesFromId(cancellationToken, term.Id))
            {
                term.Classes.Add(termClassSchedule);
            }
            return term;
        }

        internal async Task RemoveTerm(CancellationToken cancellationToken, Term term)
        {
            var query = $"DELETE FROM Term\n" +
                $"WHERE TermId = {term.Id}";

            await Database.ExecuteAsync(query);
        }
    }
}
