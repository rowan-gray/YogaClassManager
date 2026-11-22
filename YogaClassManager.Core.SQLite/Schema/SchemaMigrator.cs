using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

[assembly: InternalsVisibleTo("YogaClassManager.Core.SQLite.Tests")]

namespace YogaClassManager.Core.SQLite.Schema;

/// <summary>
///     Brings a SQLite database up to date with the schema this project expects, by applying every
///     embedded numbered migration script that hasn't been applied yet (tracked via a SchemaVersion
///     table). Safe to call on every connection-open: a fully up-to-date database is a no-op, a fresh
///     empty database gets fully bootstrapped, and the existing production yogamanager.db.sql schema
///     is brought up to date additively (nothing is dropped or altered destructively).
/// </summary>
public static partial class SchemaMigrator
{
    [GeneratedRegex(@"(\d+)_\w+\.sql$")]
    private static partial Regex MigrationResourceName();

    /// <summary>Schema-dependent steps plain SQL can't express (SQLite has no conditional DDL), run
    /// inside a migration's transaction just before its script.</summary>
    private static readonly Dictionary<int, Func<SqliteConnection, SqliteTransaction, CancellationToken, Task>>
        PreMigrationSteps = new()
        {
            [3] = RenameMisspelledAlterationCountAsync
        };

    public static async Task EnsureUpToDateAsync(SqliteConnection connection, CancellationToken cancellationToken = default)
    {
        var currentVersion = await EnsureSchemaVersionTableAndGetCurrentVersionAsync(connection, cancellationToken);

        foreach (var migration in LoadEmbeddedMigrations().OrderBy(m => m.Version))
        {
            if (migration.Version <= currentVersion)
                continue;

            var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
            try
            {
                if (PreMigrationSteps.TryGetValue(migration.Version, out var preStep))
                    await preStep(connection, transaction, cancellationToken);

                await using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = migration.Sql;
                    await command.ExecuteNonQueryAsync(cancellationToken);
                }

                await using (var recordVersion = connection.CreateCommand())
                {
                    recordVersion.Transaction = transaction;
                    recordVersion.CommandText = "INSERT INTO SchemaVersion(Version) VALUES ($version)";
                    recordVersion.Parameters.AddWithValue("$version", migration.Version);
                    await recordVersion.ExecuteNonQueryAsync(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                throw new InvalidOperationException($"Migration {migration.Version} failed to apply.", ex);
            }
            finally
            {
                await transaction.DisposeAsync();
            }
        }
    }

    /// <summary>
    ///     Reports whether <paramref name="dataSource" /> has any migration pending, without applying
    ///     anything - for callers (e.g. an app-level "back up before migrating" policy) that need to
    ///     decide something before <see cref="EnsureUpToDateAsync" /> actually runs.
    ///     <para>
    ///         <b>Gotcha:</b> opening a <see cref="SqliteConnection" /> against a path that doesn't
    ///         exist yet silently creates a zero-byte file as a side effect of connecting. Only call
    ///         this when <c>File.Exists(dataSource)</c> is already true - never speculatively, or
    ///         you've created a junk file just by asking "does this need a backup".
    ///     </para>
    /// </summary>
    public static async Task<bool> HasPendingMigrationsAsync(string dataSource, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(dataSource);
        await connection.OpenAsync(cancellationToken);

        var currentVersion = await GetCurrentVersionWithoutWritingAsync(connection, cancellationToken);
        var latestVersion = LoadEmbeddedMigrations().Max(m => m.Version);

        return currentVersion < latestVersion;
    }

    /// <summary>Reads the tracked version without creating SchemaVersion: HasPendingMigrationsAsync runs
    /// before the pre-migration backup is copied, so it must leave the file byte-for-byte untouched.
    /// A database with no SchemaVersion table is version 0, exactly as EnsureUpToDateAsync treats it.</summary>
    private static async Task<int> GetCurrentVersionWithoutWritingAsync(SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var existsCommand = connection.CreateCommand();
        existsCommand.CommandText = "SELECT EXISTS(SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = 'SchemaVersion')";
        if (Convert.ToInt64(await existsCommand.ExecuteScalarAsync(cancellationToken)) == 0)
            return 0;

        await using var versionCommand = connection.CreateCommand();
        versionCommand.CommandText = "SELECT COALESCE(MAX(Version), 0) FROM SchemaVersion";
        return Convert.ToInt32(await versionCommand.ExecuteScalarAsync(cancellationToken));
    }

    /// <summary>Ensures the SchemaVersion bootstrap table exists (creating it if this is a brand-new
    /// database) and returns the currently tracked version, without applying any migration bodies.
    /// Used by EnsureUpToDateAsync only - HasPendingMigrationsAsync must not write, see
    /// GetCurrentVersionWithoutWritingAsync.</summary>
    private static async Task<int> EnsureSchemaVersionTableAndGetCurrentVersionAsync(SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using (var bootstrap = connection.CreateCommand())
        {
            bootstrap.CommandText =
                """
                CREATE TABLE IF NOT EXISTS "SchemaVersion" (
                    "Version"   INTEGER NOT NULL PRIMARY KEY,
                    "AppliedAt" TEXT NOT NULL DEFAULT (datetime('now'))
                );
                """;
            await bootstrap.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var versionCommand = connection.CreateCommand();
        versionCommand.CommandText = "SELECT COALESCE(MAX(Version), 0) FROM SchemaVersion";
        return Convert.ToInt32(await versionCommand.ExecuteScalarAsync(cancellationToken));
    }

    /// <summary>Databases created from earlier versions of migration 001 have PassAlterations'
    /// count column misspelled as "AlerationCount" - rename it to the production "AlterationCount".
    /// A no-op on production databases, which already use the correct name.</summary>
    private static async Task RenameMisspelledAlterationCountAsync(SqliteConnection connection,
        SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        await using var check = connection.CreateCommand();
        check.Transaction = transaction;
        check.CommandText = "SELECT EXISTS(SELECT 1 FROM pragma_table_info('PassAlterations') WHERE name = 'AlerationCount')";
        if (Convert.ToInt64(await check.ExecuteScalarAsync(cancellationToken)) == 0)
            return;

        await using var rename = connection.CreateCommand();
        rename.Transaction = transaction;
        rename.CommandText = "ALTER TABLE PassAlterations RENAME COLUMN AlerationCount TO AlterationCount";
        await rename.ExecuteNonQueryAsync(cancellationToken);
    }

    internal static IReadOnlyList<(int Version, string Sql)> LoadEmbeddedMigrations()
    {
        var assembly = typeof(SchemaMigrator).Assembly;
        var migrations = new List<(int Version, string Sql)>();

        foreach (var resourceName in assembly.GetManifestResourceNames())
        {
            var match = MigrationResourceName().Match(resourceName);
            if (!match.Success)
                continue;

            using var stream = assembly.GetManifestResourceStream(resourceName)!;
            using var reader = new StreamReader(stream);
            migrations.Add((int.Parse(match.Groups[1].Value), reader.ReadToEnd()));
        }

        return migrations;
    }
}
