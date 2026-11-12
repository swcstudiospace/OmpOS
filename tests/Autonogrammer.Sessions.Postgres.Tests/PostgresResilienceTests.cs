using System.Diagnostics;
using Autonogrammer.Contracts.Cloud;
using Autonogrammer.Contracts.Streaming;
using Autonogrammer.Resilience;
using Npgsql;
using Shouldly;
using Xunit;
using static Autonogrammer.Sessions.Postgres.Tests.SessionSamples;

namespace Autonogrammer.Sessions.Postgres.Tests;

/// <summary>
/// Every command of the PostgreSQL stores runs under the deadline of the PostgreSQL <see cref="ResiliencePipeline"/>: a write that blocks on another session's row
/// lock ends at the command timeout as a <see cref="DataPlaneTimeoutException"/> that names the component and never a connection string, and a caller's own
/// cancellation still passes through untouched.
/// </summary>
[Collection(PostgresServerGroup.Name)]
public sealed class PostgresResilienceTests(PostgresServerFixture server)
{
    /// <summary>An append that blocks on a held row lock ends at the one second command timeout as a postgres timeout, is not retried, and leaks no connection string.</summary>
    [Fact]
    public async Task AppendBlockedOnARowLockEndsAtTheDeadlineAsADataPlaneTimeout()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateMigratedAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        await ProvisionAsync(schema.CreateDataSource(), ct, "dev").ConfigureAwait(true);
        try
        {
            var repository = NewRepository(schema, new PostgresResilienceOptions { InteractiveCommandSeconds = 1, BudgetSeconds = 2 });
            await repository.SaveAsync(Sample("s-1"), ct).ConfigureAwait(true);

            using var holder = new NpgsqlConnection(schema.ConnectionString);
            await holder.OpenAsync(ct).ConfigureAwait(true);
            var transaction = await holder.BeginTransactionAsync(ct).ConfigureAwait(true);
            await using (transaction.ConfigureAwait(true))
            {
                using (var hold = holder.CreateCommand())
                {
                    hold.Transaction = transaction;
                    hold.CommandText = "SELECT 1 FROM sessions WHERE id = 's-1' FOR UPDATE";
                    _ = await hold.ExecuteScalarAsync(ct).ConfigureAwait(true);
                }

                var clock = Stopwatch.StartNew();
                var failure = await Should.ThrowAsync<DataPlaneTimeoutException>(
                    () => repository.AppendAsync("s-1", Event("s-1", new TextDeltaPayload { Delta = "x" }), ct)).ConfigureAwait(true);
                clock.Stop();

                failure.Component.ShouldBe("postgres");
                failure.Reason.ShouldBe(DataPlaneTimeoutReason.Timeout);
                failure.InnerException.ShouldBeNull();
                var connection = new NpgsqlConnectionStringBuilder(schema.ConnectionString);
                failure.Message.ShouldNotContain(connection.Host!);
                if (!string.IsNullOrEmpty(connection.Password))
                {
                    failure.Message.ShouldNotContain(connection.Password);
                }

                // The command timeout is one second and the whole call budget two: a deadline cannot end sooner than the timeout nor much later than the budget.
                clock.Elapsed.ShouldBeGreaterThan(TimeSpan.FromMilliseconds(800));
                clock.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(4));
                await transaction.RollbackAsync(ct).ConfigureAwait(true);
            }

            // The timed-out append rolled back: the log is empty and the next append takes the first Seq.
            await repository.AppendAsync("s-1", Event("s-1", new TextDeltaPayload { Delta = "y" }), ct).ConfigureAwait(true);
            (await ListAsync(repository, "s-1", 0, ct).ConfigureAwait(true)).Select(static evt => evt.Seq).ToArray().ShouldBe([1L]);
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>The caller's own cancellation passes through as a cancellation, not as a data-plane timeout.</summary>
    [Fact]
    public async Task CallerCancellationPassesThroughUntouched()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateMigratedAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        await ProvisionAsync(schema.CreateDataSource(), ct, "dev").ConfigureAwait(true);
        try
        {
            var repository = NewRepository(schema, new PostgresResilienceOptions());
            await repository.SaveAsync(Sample("s-1"), ct).ConfigureAwait(true);

            using var holder = new NpgsqlConnection(schema.ConnectionString);
            await holder.OpenAsync(ct).ConfigureAwait(true);
            var transaction = await holder.BeginTransactionAsync(ct).ConfigureAwait(true);
            await using (transaction.ConfigureAwait(true))
            {
                using (var hold = holder.CreateCommand())
                {
                    hold.Transaction = transaction;
                    hold.CommandText = "SELECT 1 FROM sessions WHERE id = 's-1' FOR UPDATE";
                    _ = await hold.ExecuteScalarAsync(ct).ConfigureAwait(true);
                }

                using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cancellation.CancelAfter(TimeSpan.FromMilliseconds(500));

                var failure = await Record.ExceptionAsync(
                    () => repository.AppendAsync("s-1", Event("s-1", new TextDeltaPayload { Delta = "x" }), cancellation.Token)).ConfigureAwait(true);

                failure.ShouldNotBeNull();
                failure.ShouldBeAssignableTo<OperationCanceledException>();
                failure.ShouldNotBeOfType<DataPlaneTimeoutException>();
                await transaction.RollbackAsync(ct).ConfigureAwait(true);
            }
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>The cloud agent store runs through the same pipeline: a deadline on a locked row is a postgres timeout there too.</summary>
    [Fact]
    public async Task CloudAgentUpdateBlockedOnARowLockEndsAtTheDeadlineAsADataPlaneTimeout()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateMigratedAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        await ProvisionAsync(schema.CreateDataSource(), ct, "alice").ConfigureAwait(true);
        try
        {
            var resilience = PostgresResilience.Create(new PostgresResilienceOptions { InteractiveCommandSeconds = 1, BudgetSeconds = 2 });
            var store = new PostgresCloudAgentStore(schema.CreateDataSource(), resilience);
            _ = await store.CreateAsync(
                new CloudAgentRecord
                {
                    Id = "a1",
                    OwnerId = "alice",
                    SessionId = "cloud-a1",
                    Name = "a1",
                    Status = CloudAgentStatus.Provisioning,
                    CreatedAt = Epoch,
                    UpdatedAt = Epoch,
                    Repositories = [new CloudRepository { Address = "https://github.com/acme/widgets" }],
                    Branch = "autonogrammer/cloud/a1",
                    IdleMinutes = 15,
                    LastActivityAt = Epoch,
                },
                new CloudAgentLimits(MaxAgents: 5, MaxActive: 5),
                ct).ConfigureAwait(true);

            using var holder = new NpgsqlConnection(schema.ConnectionString);
            await holder.OpenAsync(ct).ConfigureAwait(true);
            var transaction = await holder.BeginTransactionAsync(ct).ConfigureAwait(true);
            await using (transaction.ConfigureAwait(true))
            {
                using (var hold = holder.CreateCommand())
                {
                    hold.Transaction = transaction;
                    hold.CommandText = "SELECT 1 FROM cloud_agents WHERE id = 'a1' FOR UPDATE";
                    _ = await hold.ExecuteScalarAsync(ct).ConfigureAwait(true);
                }

                var failure = await Should.ThrowAsync<DataPlaneTimeoutException>(
                    () => store.UpdateAsync("a1", static record => record with { ComputeSeconds = record.ComputeSeconds + 1 }, ct)).ConfigureAwait(true);

                failure.Component.ShouldBe("postgres");
                await transaction.RollbackAsync(ct).ConfigureAwait(true);
            }
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    private static PostgresSessionRepository NewRepository(PostgresTestSchema schema, PostgresResilienceOptions options) =>
        new(schema.CreateDataSource(), schema.Connection, PostgresResilience.Create(options));

    /// <summary>Provisions accounts as the provisioning step does: one lifecycle row each, committed before the account's first write.</summary>
    private static async Task ProvisionAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken, params string[] accountIds)
    {
        var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            foreach (var accountId in accountIds)
            {
                using var command = connection.CreateCommand();
                command.CommandText = "INSERT INTO account_lifecycle (account_id) VALUES (@account_id) ON CONFLICT (account_id) DO NOTHING";
                command.Parameters.AddWithValue("account_id", accountId);
                _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
