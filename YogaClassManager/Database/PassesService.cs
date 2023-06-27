using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YogaClassManager.Database.Models;
using YogaClassManager.Models;
using YogaClassManager.Models.Passes;

namespace YogaClassManager.Database
{
    public class PassesService : DatabaseService
    {
        public PassesService(DatabaseManager dbManager) : base(dbManager)
        {
        }

        internal async Task AddPassAsync(CancellationToken cancellationToken, Pass pass)
        {
            await dbManager.BeginTransactionAsync();
            try
            {
                var id = await GetUnusedPassId(cancellationToken);

                string query = "INSERT INTO Pass " +
                $"VALUES ({id},{pass.StudentId})";

                await Database.ExecuteAsync(query);

                if (pass.GetType() == typeof(DatedPass))
                {
                    var datedPass = (DatedPass)pass;
                    query = "INSERT INTO DatedPass " +
                    $"VALUES ({id},{datedPass.ClassCount},{NullableDateOnlyToSQLString(datedPass.StartDate)},{NullableDateOnlyToSQLString(datedPass.EndDate)})";
                    await Database.ExecuteAsync(query);
                }
                else if (pass.GetType() == typeof(TermPass))
                {
                    var termPass = (TermPass)pass;
                    query = "INSERT INTO TermPass " +
                    $"VALUES ({id},{termPass.Term.Id},{termPass.TermClassSchedule.ClassSchedule.Id})";
                    await Database.ExecuteAsync(query);
                }
                else if (pass.GetType() == typeof(CasualPass))
                {
                    var casualPass = (CasualPass)pass;
                    query = "INSERT INTO CasualPass " +
                    $"VALUES ({id},{casualPass.ClassCount})";
                    await Database.ExecuteAsync(query);
                }

                foreach (var alteration in pass.Alterations)
                {
                    alteration.PassId = id;
                    await AddPassAlterationAsync(cancellationToken, alteration);
                }

                await dbManager.CommitAsync();
            }
            catch
            {
                await dbManager.AbortAsync();
                throw;
            }
}

        private string NullableDateOnlyToSQLString(DateOnly? dateOnly)
        {
            return dateOnly.HasValue ? $"'{dateOnly.Value.ToString("yyyy-MM-dd")}'" : "NULL";
        }

        public async Task<int> GetUnusedPassId(CancellationToken cancellationToken)
        {
            int id;

            do
            {
                id = new Random().Next(0, 100000);
            }
            while (await IsIdInUse(cancellationToken, id));

            return id;
        }

        internal async Task<bool> IsIdInUse(CancellationToken cancellationToken, int id)
        {


            string query = "SELECT null FROM Pass " +
                $"WHERE PassId = {id}";

            var results = await Database.QueryScalarsAsync<string>(query);
            return results.Count > 0;
        }

        internal async Task SavePassAsync(CancellationToken cancellationToken, Pass pass)
        {
            await dbManager.BeginTransactionAsync();

            try
            {
                string query = "DELETE FROM PassAlterations " +
                $"WHERE PassId = {pass.Id}";

                foreach (var alteration in pass.Alterations)
                {
                    if (alteration.Id >= 0)
                    {
                        query += $" AND PassAlterationId <> {alteration.Id}";
                    }
                }

                await Database.ExecuteAsync(query);

                foreach (var alteration in pass.Alterations)
                {
                    if (alteration.Id < 0)
                    {
                        await AddPassAlterationAsync(cancellationToken, alteration);
                    }
                }

                if (pass.GetType() == typeof(TermPass))
                {
                    string updatePassQuery = "UPDATE TermPass " +
                        $"SET ClassId={((TermPass)pass).TermClassSchedule.ClassSchedule.Id} " +
                        $"WHERE PassId={pass.Id}";

                    await Database.ExecuteAsync(updatePassQuery);
                }
                else if (pass.GetType() == typeof(DatedPass))
                {
                    var datedPass = (DatedPass)pass;

                    string updatePassQuery = "UPDATE DatedPass " +
                        $"SET StartDate={NullableDateOnlyToSQLString(datedPass.StartDate)}, " +
                        $"EndDate={NullableDateOnlyToSQLString(datedPass.EndDate)} " +
                        $"WHERE PassId={datedPass.Id}";

                    await Database.ExecuteAsync(updatePassQuery);
                }

                await dbManager.CommitAsync();
            }
            catch
            {
                await dbManager.AbortAsync();
                throw;
            }
        }

        internal async Task AddPassAlterationAsync(CancellationToken cancellationToken, PassAlteration alteration)
        {
            var id = await GetUnusedPassAlterationId(cancellationToken);

            string query = "INSERT INTO PassAlterations " +
            "VALUES (?,?,?,?)";

            await Database.ExecuteAsync(query, id, alteration.PassId, alteration.Amount, alteration.Reason);
        }

        private async Task<int> GetUnusedPassAlterationId(CancellationToken cancellationToken)
        {
            int id;

            do
            {
                id = new Random().Next(0, 100000);
            }
            while (await IsPassAlterationIdInUse(cancellationToken, id));

            return id;
        }

        private async Task<bool> IsPassAlterationIdInUse(CancellationToken cancellationToken, int id)
        {
            string query = "SELECT null FROM PassAlterations " +
                $"WHERE PassAlterationId = {id}";

            var results = await Database.QueryScalarsAsync<string>(query);
            return results.Count > 0;
        }

        internal async Task<List<PassAlteration>> GetAlterationsFromPassId(CancellationToken cancellationToken, int passId)
        {
            var query = "SELECT PassAlterationId, PassId, AlterationCount, AlterationReason " +
                    "FROM PassAlterations " +
                    $"WHERE PassId = {passId}";

            var results = await Database.QueryAsync<PassAlterationsMapping>(query);

            List<PassAlteration> alterations = results.Select(mapping => mapping.ToPassAlteration()).ToList();

            return alterations;
        }

        internal async Task RemovePassAsync(CancellationToken cancellationToken, Pass pass)
        {
            string query = "DELETE FROM Pass " +
                $"WHERE PassId = {pass.Id};";

            await Database.ExecuteAsync(query);
        }

        internal async Task<Pass> GetPassFromId(CancellationToken cancellationToken, int passId)
        {
            var datedPassQuery = "SELECT Pass.PassId, Pass.StudentId, ClassCount, StartDate, EndDate, TimesUsed " +
                "FROM Pass " +
                "JOIN DatedPass ON Pass.PassId = DatedPass.PassId " +
                "JOIN PassUses on PassUses.PassId = Pass.PassId " +
                "JOIN PassesTotalClasses on PassesTotalClasses.PassId = Pass.PassId " +
                $"WHERE Pass.PassId = {passId}";

            var datedPassResults = await Database.QueryAsync<DatedPassMapping>(datedPassQuery);
            var datedPassResult = datedPassResults.FirstOrDefault();

            if (datedPassResult is not null)
            {
                var datedPassAlterations = await dbManager.PassesService.GetAlterationsFromPassId(cancellationToken, datedPassResult.PassId);
                var datedPass = new Pass(datedPassResult.PassId, datedPassResult.StudentId, datedPassResult.TimesUsed, new(datedPassAlterations));
                return new DatedPass(datedPass, datedPassResult.ClassCount, StringToDateOnly(datedPassResult.StartDate), StringToDateOnly(datedPassResult.EndDate));
            }

            var casualPassQuery = "SELECT Pass.PassId, Pass.StudentId, ClassCount, TimesUsed " +
                "FROM Pass " +
                "JOIN CasualPass ON Pass.PassId = CasualPass.PassId " +
                "JOIN PassUses on PassUses.PassId = Pass.PassId " +
                "JOIN PassesTotalClasses on PassesTotalClasses.PassId = Pass.PassId " +
                $"WHERE Pass.PassId = {passId}";

            var casualPassResults = await Database.QueryAsync<CasualPassMapping>(casualPassQuery);
            var casualPassResult = casualPassResults.FirstOrDefault();

            if (casualPassResult is not null)
            {
                var casualPassAlterations = await dbManager.PassesService.GetAlterationsFromPassId(cancellationToken, casualPassResult.PassId);
                var casualPass = new Pass(casualPassResult.PassId, casualPassResult.StudentId, casualPassResult.TimesUsed, new(casualPassAlterations));
                return new CasualPass(casualPass, casualPassResult.ClassCount);
            }

            var termPassQuery = "SELECT Pass.PassId AS PassId, Pass.StudentId AS StudentId, " +
                    "TermPass.TermId AS TermId, TermPass.ClassId AS ClassId, TimesUsed " +
                    "FROM Pass " +
                    "JOIN TermPass ON Pass.PassId = TermPass.PassId " +
                    "JOIN Term on TermPass.TermId = Term.TermId " +
                    "JOIN PassUses on PassUses.PassId = Pass.PassId " +
                    "JOIN PassesTotalClasses on PassesTotalClasses.PassId = Pass.PassId " +
                    $"WHERE Pass.PassId = {passId}";

            var termPassResults = await Database.QueryAsync<TermPassMapping>(termPassQuery);
            var termPassResult = termPassResults.FirstOrDefault();

            if (termPassResult is null)
            {
                throw new ArgumentException($"There does not exist a pass with id {passId}");
            }

            var term = await dbManager.TermsService.GetTermFromIdAsync(cancellationToken, termPassResult.TermId);
            var termClassSchedule = term.Classes.FirstOrDefault(termClassSchedule => termClassSchedule.ClassSchedule.Id == termPassResult.ClassId);
            var alterations = await dbManager.PassesService.GetAlterationsFromPassId(cancellationToken, termPassResult.PassId);

            var pass = new Pass(termPassResult.PassId, termPassResult.StudentId, termPassResult.TimesUsed, new(alterations));

            return new TermPass(pass, term, termClassSchedule);
        }
    }
}
