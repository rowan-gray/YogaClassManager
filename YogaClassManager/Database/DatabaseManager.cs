# nullable enable

using SQLite;
using YogaClassManager.Resources;

namespace YogaClassManager.Database;

public class DatabaseManager
{
    private SQLiteAsyncConnection? database;
    private string filePath;
    private int transactionDepth;

    public DatabaseManager(string dbLocation)
    {
        filePath = dbLocation;
        if (ValidateFilePath(dbLocation)) Database = new SQLiteAsyncConnection(filePath, AppConstants.Flags);

        ClassesService = new ClassesService(this);
        PassesService = new PassesService(this);
        PeopleService = new PeopleService(this);
        StudentsService = new StudentsService(this);
        TermsService = new TermService(this);
    }

    public SQLiteAsyncConnection Database
    {
        get
        {
            if (database is not null)
                return database;
            throw new IOException("Invalid file path for database.");
        }
        set => database = value;
    }

    public ClassesService ClassesService { get; }
    public PassesService PassesService { get; }
    public PeopleService PeopleService { get; }
    public StudentsService StudentsService { get; }
    public TermService TermsService { get; }

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
        if (transactionDepth == 0) await Database.ExecuteAsync("BEGIN TRANSACTION");
        transactionDepth++;
    }

    public async Task CommitAsync()
    {
        transactionDepth--;
        if (transactionDepth == 0) await Database.ExecuteAsync("COMMIT");
    }

    internal async Task AbortAsync()
    {
        transactionDepth = 0;
        await Database.ExecuteAsync("ROLLBACK");
    }
}