using System.Data.Common;
using Microsoft.Data.Sqlite;

namespace YogaClassManager.NewDatabase;

public class DatabaseService
{
    private uint numConnections;

    public DatabaseService(string filePath)
    {
        var connectionString = new SqliteConnectionStringBuilder($"Data Source={filePath}")
        {
            Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true
        }.ToString();

        DbConnection = new SqliteConnection(connectionString);
    }

    private SqliteConnection DbConnection { get; }

    public async Task ExecuteDbAction(Func<DbConnection, Task> action)
    {
        await OpenConnection();

        try
        {
            await action(DbConnection);
        }
        finally
        {
            await CloseConnection();
        }
    }

    public async Task<T> ExecuteDbFunction<T>(Func<DbConnection, Task<T>> function)
    {
        await OpenConnection();

        T returnValue;

        try
        {
            returnValue = await function(DbConnection);
        }
        finally
        {
            await CloseConnection();
        }

        return returnValue;
    }

    public async Task ExecuteDbActionInTransaction(Func<DbConnection, DbTransaction, Task> action)
    {
        await OpenConnection();

        var transaction = await DbConnection.BeginTransactionAsync();

        try
        {
            await action(DbConnection, transaction);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
        finally
        {
            await CloseConnection();
        }

        await transaction.CommitAsync();
    }

    public async Task<T> ExecuteDbFunction<T>(Func<DbConnection, DbTransaction, Task<T>> function)
    {
        await OpenConnection();

        var transaction = await DbConnection.BeginTransactionAsync();
        T returnValue;

        try
        {
            returnValue = await function(DbConnection, transaction);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
        finally
        {
            await CloseConnection();
        }

        await transaction.CommitAsync();
        return returnValue;
    }

    private async Task OpenConnection()
    {
        if (numConnections == 0) await DbConnection.OpenAsync();

        numConnections++;
    }

    private async Task CloseConnection()
    {
        numConnections--;

        if (numConnections == 0) await DbConnection.CloseAsync();
    }
}