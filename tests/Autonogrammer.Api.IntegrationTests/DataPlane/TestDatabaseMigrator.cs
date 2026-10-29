using System.Runtime.ExceptionServices;
using Autonogrammer.Sessions.Postgres;
using Autonogrammer.Sessions.Postgres.Migrations;

namespace Autonogrammer.Api.IntegrationTests.DataPlane;

/// <summary>
/// Migrates the throwaway PostgreSQL database that Production-shaped API hosts and the routed-run test use, once per test process. The API never creates schema
/// objects on its own (<c>Sessions:Postgres:AutoMigrate</c> is off in a Production-shaped host), so the database must already be at the head when such a host starts.
/// The runner takes its own advisory lock, so a second test process that migrates the same database waits instead of racing.
/// </summary>
internal static class TestDatabaseMigrator
{
    private static readonly Lock Gate = new();

    private static string? s_migrated;

    /// <summary>Migrates the database named by <paramref name="adminConnectionString"/> to the compiled head unless this process already did.</summary>
    /// <param name="adminConnectionString">The administrative connection string of the throwaway database (the test migration role).</param>
    public static void EnsureMigrated(string adminConnectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(adminConnectionString);
        lock (Gate)
        {
            if (string.Equals(s_migrated, adminConnectionString, StringComparison.Ordinal))
            {
                return;
            }

            MigrateAndWait(adminConnectionString);
            s_migrated = adminConnectionString;
        }
    }

    /// <summary>
    /// Runs the asynchronous runner on the thread pool and waits for its completion handle. The configuration code that calls this is synchronous and xUnit runs it
    /// without a synchronization context, so waiting on the handle cannot deadlock; the failure, if any, is rethrown with its original stack.
    /// </summary>
    private static void MigrateAndWait(string adminConnectionString)
    {
        var migration = Task.Run(() => MigrateAsync(adminConnectionString));
        _ = ((IAsyncResult)migration).AsyncWaitHandle.WaitOne();
        if (migration.Exception is { InnerException: { } failure })
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private static async Task MigrateAsync(string adminConnectionString)
    {
        var dataSource = new PostgresSessionConnection(adminConnectionString).CreateDataSource(PostgresRole.Migrator);
        await using (dataSource.ConfigureAwait(false))
        {
            await new MigrationRunner(dataSource).MigrateAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }
}
