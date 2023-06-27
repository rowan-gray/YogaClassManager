using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YogaClassManager.Database.Models;
using YogaClassManager.Models.People;

namespace YogaClassManager.Database
{
    public class PeopleService : DatabaseService
    {
        public PeopleService(DatabaseManager dbManager) : base(dbManager)
        {
        }

        internal async Task<List<Person>> GetPeopleAsync(CancellationToken cancellationToken, bool retrieveHiddenPeople = false)
        {
            var query = "SELECT PersonId, FirstName, LastName, " +
                    "PhoneNumber, Email, IsActive FROM Person " +
                    $"WHERE ({retrieveHiddenPeople} OR IsActive = {!retrieveHiddenPeople}) " +
                    $"ORDER BY Firstname ASC";

            var results = await Database.QueryAsync<PersonMapping>(query);

            var people = results.Select(mapping => mapping.ToPerson()).ToList();
            return people;
        }

        internal async Task<int> AddPersonAsync(CancellationToken cancellationToken, Person person)
        {
            await dbManager.BeginTransactionAsync();
            try
            {
                var id = await GetUnusedPersonId(cancellationToken);

                string query = "INSERT INTO Person " +
                $"VALUES (?,?,?,?,?,?)";

                await Database.ExecuteAsync(query, id,person.FirstName,person.LastName,person.PhoneNumber,person.Email,person.IsActive);

                await dbManager.CommitAsync();
                return id;
            }
            catch
            {
                await dbManager.AbortAsync();
                throw;
            }
        }

        internal async Task<int> GetUnusedPersonId(CancellationToken cancellationToken)
        {
            int id;

            do
            {
                id = new Random().Next(0, 10000);
            }
            while (await IsIdInUse(cancellationToken, id));

            return id;
        }

        internal async Task<bool> IsIdInUse(CancellationToken cancellationToken, int id)
        {
            string query = "SELECT null FROM Person " +
                $"WHERE PersonId = {id}";

            var results = await Database.QueryScalarsAsync<string>(query);
            return results.Count > 0;
        }

        internal async Task SavePersonAsync(CancellationToken cancellationToken, Person person)
        {
            string query = "UPDATE Person " +
                $"SET FirstName=?, LastName=?, PhoneNumber=?, Email=?, IsActive=? " +
                $"WHERE PersonId = {person.Id}";

            await Database.ExecuteAsync(query,person.FirstName, person.LastName, person.PhoneNumber, person.Email, person.IsActive);
        }

        internal async Task HidePersonAsync(CancellationToken cancellationToken, Person person)
        {
            string query = "UPDATE Person " +
            $"SET IsActive = FALSE " +
            $"WHERE PersonId = {person.Id}";

            await Database.ExecuteAsync(query);
        }

        internal async Task UnhidePersonAsync(CancellationToken cancellationToken, Person person)
        {
            string query = "UPDATE Person " +
                $"SET IsActive = TRUE " +
                $"WHERE PersonId = {person.Id}";

            await Database.ExecuteAsync(query);
        }

        internal async Task<List<Person>> SearchPeopleAsync(CancellationToken cancellationToken, string searchQuery, bool returnHiddenPeople = false)
        {
            var query = "SELECT PersonId, FirstName, LastName, PhoneNumber, Email, IsActive FROM Person " +
                $"WHERE (lower(FirstName) LIKE lower('{searchQuery}%') OR lower(LastName) LIKE lower('{searchQuery}%') " +
                $"OR lower(FirstName || ' ' || LastName) LIKE lower('{searchQuery}%')) " +
                $"AND ({returnHiddenPeople} OR IsActive = {!returnHiddenPeople}) " +
                $"ORDER BY Firstname ASC";

            var results = await Database.QueryAsync<PersonMapping>(query);

            var people = results.Select(mapping => mapping.ToPerson()).ToList();
            return people;
        }

        internal async Task<List<Person>> GetUpdatedPeople(CancellationToken cancellationToken, long timestamp, bool returnInactiveStudents = false)
        {
            var query = "SELECT PersonId, FirstName, LastName, PhoneNumber, Email, IsActive\n" +
                "FROM Modifications M\n" +
                "INNER JOIN Person P ON M.id_value = P.PersonId\n" +
                $"WHERE table_name = 'Person' AND changed_at >= {timestamp}";

            if (!returnInactiveStudents)
            {
                query += " AND IsActive = 1";
            }


            var results = await Database.QueryAsync<PersonMapping>(query);

            var people = results.Select(mapping => mapping.ToPerson()).ToList();
            return people;
        }

        internal async Task<List<Person>> GetUpdatedPeopleMatchingQuery(CancellationToken cancellationToken, long timestamp, string searchQuery, bool returnInactiveStudents = false)
        {
            var query = "SELECT PersonId, FirstName, LastName, PhoneNumber, Email, IsActive\n" +
                "FROM Modifications M\n" +
                "INNER JOIN Person P ON M.id_value = P.PersonId\n" +
                $"WHERE table_name = 'Person' AND changed_at >= {timestamp} AND " +
                $"(lower(FirstName) LIKE lower('{searchQuery}%') OR " +
                $"lower(LastName) LIKE lower('{searchQuery}%') " +
                $"OR lower(FirstName || ' ' || LastName) LIKE lower('{searchQuery}%'))";

            if (!returnInactiveStudents)
            {
                query += " AND IsActive = 1";
            }

            var results = await Database.QueryAsync<PersonMapping>(query);

            var people = results.Select(mapping => mapping.ToPerson()).ToList();
            return people;
        }

        internal async Task<List<int>> GetDeletedPeopleIds(CancellationToken cancellationToken, long timestamp)
        {
            var query = "SELECT id_value\n" +
                "FROM Modifications M\n" +
                $"WHERE table_name = 'Person' AND changed_at >= {timestamp} AND action = 'DELETE'";


            var results = await Database.QueryScalarsAsync<int>(query);

            return results;
        }
    }
}
