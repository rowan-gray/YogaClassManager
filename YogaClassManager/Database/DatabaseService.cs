using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YogaClassManager.Database
{
    public class DatabaseService
    {
        protected readonly DatabaseManager dbManager;
        protected SQLiteAsyncConnection Database => dbManager.Database;

        public DatabaseService(DatabaseManager dbManager)
        {
            this.dbManager = dbManager;
        }

        protected DateOnly StringToDateOnly(string date)
        {
            return DateOnly.ParseExact(date, "yyyy-MM-dd");
        }
    }
}
