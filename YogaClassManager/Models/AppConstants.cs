using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YogaClassManager.Resources
{
    public static class AppConstants
    {
        public const SQLite.SQLiteOpenFlags Flags =
        // open the database in read/write mode
        SQLite.SQLiteOpenFlags.ReadWrite |
        // enable multi-threaded database access
        SQLite.SQLiteOpenFlags.SharedCache |
        // database file isn't encrypted.
        SQLite.SQLiteOpenFlags.ProtectionNone;
    }
}
