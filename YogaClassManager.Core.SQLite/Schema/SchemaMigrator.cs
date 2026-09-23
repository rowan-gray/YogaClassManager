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
public static class SchemaMigrator
{
    private static readonly Regex MigrationResourceName = new(@"(\d+)_\w+\.sql$", RegexOptions.Compiled);

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

        var currentVersion = await EnsureSchemaVersionTableAndGetCurrentVersionAsync(connection, cancellationToken);
        var latestVersion = LoadEmbeddedMigrations().Max(m => m.Version);

        return currentVersion < latestVersion;
    }

    /// <summary>Ensures the SchemaVersion bootstrap table exists (creating it if this is a brand-new
    /// database) and returns the currently tracked version, without applying any migration bodies.
    /// Shared by EnsureUpToDateAsync (which then applies whatever's pending) and
    /// HasPendingMigrationsAsync (which only wants to know if anything's pending).</summary>
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

    internal static IReadOnlyList<(int Version, string Sql)> LoadEmbeddedMigrations()
    {
        var assembly = typeof(SchemaMigrator).Assembly;
        var migrations = new List<(int Version, string Sql)>();

        foreach (var resourceName in assembly.GetManifestResourceNames())
        {
            var match = MigrationResourceName.Match(resourceName);
            if (!match.Success)
                continue;

            using var stream = assembly.GetManifestResourceStream(resourceName)!;
            using var reader = new StreamReader(stream);
            migrations.Add((int.Parse(match.Groups[1].Value), reader.ReadToEnd()));
        }

        return migrations;
    }
}
