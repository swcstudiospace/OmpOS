using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Contracts.Streaming;
using Autonogrammer.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Sessions.Postgres.Tests;

/// <summary>
/// Failed-completion detail as persisted by <see cref="SessionService"/> over the PostgreSQL store. The service tests in
/// Autonogrammer.Sessions.Tests cover the same rules over the in-memory store.
/// </summary>
[Collection(PostgresServerGroup.Name)]
public sealed class PostgresSessionServiceCompletionTests(PostgresServerFixture server)
{
    /// <summary>A failed completion with detail persists the trimmed detail after the standard failure message.</summary>
    [Fact]
    public async Task FailedCompletionWithDetailPersistsDetailInTerminalError()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            await PostgresSessionSchema.EnsureCreatedAsync(schema.CreateDataSource(), ct).ConfigureAwait(true);
            var repository = schema.CreateRepository();
            var service = new SessionService(repository, TimeProvider.System);
            var session = await service.CreateAsync("dev", ct).ConfigureAwait(true);
            await service.SubmitPromptAsync(session.Id, "first", ct).ConfigureAwait(true);

            await service.RecordCompletionAsync(session.Id, SessionStatus.Failed, ct, failureDetail: "  rate limited  ").ConfigureAwait(true);

            var stored = await repository.GetAsync(session.Id, ct).ConfigureAwait(true);
            stored.ShouldNotBeNull();
            stored.Status.ShouldBe(SessionStatus.Failed);
            stored.Events[^1].Payload.ShouldBeOfType<ErrorPayload>().Message.ShouldBe("Prompt execution failed. Detail: rate limited");
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>A failed completion with no detail, null, empty or whitespace, persists the standard failure message unchanged.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task FailedCompletionWithoutDetailKeepsExistingMessage(string? detail)
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            await PostgresSessionSchema.EnsureCreatedAsync(schema.CreateDataSource(), ct).ConfigureAwait(true);
            var repository = schema.CreateRepository();
            var service = new SessionService(repository, TimeProvider.System);
            var session = await service.CreateAsync("dev", ct).ConfigureAwait(true);
            await service.SubmitPromptAsync(session.Id, "first", ct).ConfigureAwait(true);

            await service.RecordCompletionAsync(session.Id, SessionStatus.Failed, ct, failureDetail: detail).ConfigureAwait(true);

            var stored = await repository.GetAsync(session.Id, ct).ConfigureAwait(true);
            stored.ShouldNotBeNull();
            stored.Events[^1].Payload.ShouldBeOfType<ErrorPayload>().Message.ShouldBe(SessionService.FailedTurnMessage);
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Settling a session that awaits approval over PostgreSQL denies the pending request under the session lock, then
    /// completes with the requested status. The denial and the settlement share one lock, so this path must not wait for itself.
    /// </summary>
    [Theory]
    [InlineData(SessionStatus.Completed)]
    [InlineData(SessionStatus.Failed)]
    public async Task SettlingAwaitingApprovalCompletesWithRequestedStatus(SessionStatus final)
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            await PostgresSessionSchema.EnsureCreatedAsync(schema.CreateDataSource(), ct).ConfigureAwait(true);
            var repository = schema.CreateRepository();
            var service = new SessionService(repository, TimeProvider.System);
            var session = await service.CreateAsync("dev", ct).ConfigureAwait(true);
            await service.SubmitPromptAsync(session.Id, "first", ct).ConfigureAwait(true);
            await service.RequestToolApprovalAsync(session.Id, "request-1", "bash", ct, new HashSet<string>(StringComparer.Ordinal) { "bash" }).ConfigureAwait(true);

            await service.RecordCompletionAsync(session.Id, final, ct).ConfigureAwait(true);

            var stored = await repository.GetAsync(session.Id, ct).ConfigureAwait(true);
            stored.ShouldNotBeNull();
            stored.Status.ShouldBe(final);
            var record = await repository.GetToolApprovalAsync(session.Id, "request-1", ct).ConfigureAwait(true);
            record.ShouldNotBeNull();
            record.Decision.ShouldBe(PermissionDecision.Deny);
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }
}
