# nullable enable

using SQLite;
using YogaClassManager.Helpers;
using YogaClassManager.Resources;

namespace YogaClassManager.Database
{
    public class DatabaseManager
    {
        private string filePath;
        private int transactionDepth = 0;
        private SQLiteAsyncConnection? database;
        public SQLiteAsyncConnection Database
        {
            get
            {
                if (database is not null)
                {
                    return database;
                }
                else
                {
                    throw new IOException("Invalid file path for database.");
                }
            }
            set => database = value;
        }

        public DatabaseManager(string dbLocation)
        {
            this.filePath = dbLocation;
            if (ValidateFilePath(dbLocation))
            {
                Database = new SQLiteAsyncConnection(filePath, AppConstants.Flags);
            }

            ClassesService = new(this);
            PassesService = new(this);
            PeopleService = new(this);
            StudentsService = new(this);
            TermsService = new(this);
        }

        public void SetFilePath(string filePath)
        {
            if (ValidateFilePath(filePath))
            {
                this.filePath = filePath;
                Database = new SQLiteAsyncConnection(filePath, AppConstants.Flags);
            }
            else
            {
                throw new ArgumentException("FilePath is not valid");
            }
        }

        public bool ValidateFilePath(string filePath)
        {
            return File.Exists(filePath) && filePath.Split(".").LastOrDefault() == "db";
        }

        public async Task BeginTransactionAsync()
        {
            if (transactionDepth == 0)
            {
                await Database.ExecuteAsync("BEGIN TRANSACTION");
            }
            transactionDepth++;
        }
        public async Task CommitAsync()
        {
            transactionDepth--;
            if (transactionDepth == 0)
            {
                await Database.ExecuteAsync("COMMIT");
            }
        }

        internal async Task AbortAsync()
        {
            transactionDepth = 0;
            await Database.ExecuteAsync("ROLLBACK");
        }

        public ClassesService ClassesService { get; }
        public PassesService PassesService { get; }
        public PeopleService PeopleService { get; }
        public StudentsService StudentsService { get; }
        public TermService TermsService { get; }
    }
}