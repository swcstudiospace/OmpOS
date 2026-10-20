using OmpOS.Contracts.Errors;
using OmpOS.Contracts.Sessions;
using OmpOS.Contracts.Streaming;
using Shouldly;
using Xunit;

namespace OmpOS.Sessions.Tests;

/// <summary>Prompt admission, steer/follow-up policy and completion guards of <see cref="SessionService"/> (SESS-02).</summary>
public sealed class SessionServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A new session is idle, owned by the caller, and persisted.</summary>
    [Fact]
    public async Task CreateReturnsIdleSessionAndPersistsIt()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = new InMemorySessionRepository();
        var service = NewService(repository);

        var created = await service.CreateAsync("dev", ct).ConfigureAwait(true);

        created.Status.ShouldBe(SessionStatus.Idle);
        created.OwnerId.ShouldBe("dev");
        created.CreatedAt.ShouldBe(Now);
        var stored = await repository.GetAsync(created.Id, ct).ConfigureAwait(true);
        stored.ShouldNotBeNull();
        stored.Id.ShouldBe(created.Id);
    }

    /// <summary>Submitting a prompt on an idle session runs it, records the user message and appends the acceptance event.</summary>
    [Fact]
    public async Task SubmitPromptStartsRunningAndAppendsAcceptance()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = new InMemorySessionRepository();
        var service = NewService(repository);
        var session = await service.CreateAsync("dev", ct).ConfigureAwait(true);

        await service.SubmitPromptAsync(session.Id, "hello", ct).ConfigureAwait(true);

        var stored = await repository.GetAsync(session.Id, ct).ConfigureAwait(true);
        stored.ShouldNotBeNull();
        stored.Status.ShouldBe(SessionStatus.Running);
        stored.Messages.Select(static message => (message.Role, message.Content)).ShouldBe([("user", "hello")]);
        stored.Events.Select(static evt => evt.Seq).ToArray().ShouldBe([1L]);
        stored.Events[0].Payload.ShouldBeOfType<UnknownPayload>().OriginalKind.ShouldBe("prompt_accepted");
    }

    /// <summary>A second prompt while one is running is rejected with PromptInFlight and is not queued.</summary>
    [Fact]
    public async Task SecondPromptWhileRunningIsRejectedWithPromptInFlight()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = new InMemorySessionRepository();
        var service = NewService(repository);
        var session = await service.CreateAsync("dev", ct).ConfigureAwait(true);
        await service.SubmitPromptAsync(session.Id, "first", ct).ConfigureAwait(true);

        var error = await Should.ThrowAsync<SessionException>(() =>
            service.SubmitPromptAsync(session.Id, "second", ct)).ConfigureAwait(true);

        error.Error.Code.ShouldBe(ErrorCode.PromptInFlight);
        var stored = await repository.GetAsync(session.Id, ct).ConfigureAwait(true);
        stored.ShouldNotBeNull();
        stored.Messages.Select(static message => message.Content).ShouldBe(["first"]);
    }

    /// <summary>A prompt awaiting tool approval also counts as in flight.</summary>
    [Fact]
    public async Task PromptWhileAwaitingToolApprovalIsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = new InMemorySessionRepository();
        var service = NewService(repository);
        var session = await service.CreateAsync("dev", ct).ConfigureAwait(true);
        await repository.SaveAsync(session with { Status = SessionStatus.AwaitingToolApproval }, ct).ConfigureAwait(true);

        var error = await Should.ThrowAsync<SessionException>(() =>
            service.SubmitPromptAsync(session.Id, "again", ct)).ConfigureAwait(true);

        error.Error.Code.ShouldBe(ErrorCode.PromptInFlight);
    }

    /// <summary>Under concurrent submissions exactly one prompt is admitted; every other is rejected with PromptInFlight.</summary>
    [Fact]
    public async Task ConcurrentSubmissionsAdmitExactlyOnePrompt()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = new InMemorySessionRepository();
        var service = NewService(repository);
        var session = await service.CreateAsync("dev", ct).ConfigureAwait(true);

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 10).Select(i => Task.Run(async () =>
        {
            try
            {
                await service.SubmitPromptAsync(session.Id, $"prompt {i}", ct).ConfigureAwait(false);
                return (Admitted: true, Code: (ErrorCode?)null);
            }
            catch (SessionException ex)
            {
                return (Admitted: false, Code: (ErrorCode?)ex.Error.Code);
            }
        }, ct))).ConfigureAwait(true);

        outcomes.Count(static outcome => outcome.Admitted).ShouldBe(1);
        outcomes.Where(static outcome => !outcome.Admitted).Select(static outcome => outcome.Code).ShouldAllBe(code => code == ErrorCode.PromptInFlight);
        var stored = await repository.GetAsync(session.Id, ct).ConfigureAwait(true);
        stored.ShouldNotBeNull();
        stored.Messages.Count(static message => string.Equals(message.Role, "user", StringComparison.Ordinal)).ShouldBe(1);
    }

    /// <summary>After a prompt settles, the session accepts a new prompt.</summary>
    [Theory]
    [InlineData(SessionStatus.Completed)]
    [InlineData(SessionStatus.Failed)]
    [InlineData(SessionStatus.Cancelled)]
    public async Task PromptAllowedAfterSettlement(SessionStatus outcome)
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = new InMemorySessionRepository();
        var service = NewService(repository);
        var session = await service.CreateAsync("dev", ct).ConfigureAwait(true);
        await service.SubmitPromptAsync(session.Id, "first", ct).ConfigureAwait(true);
        await service.RecordCompletionAsync(session.Id, outcome, ct).ConfigureAwait(true);

        await service.SubmitPromptAsync(session.Id, "second", ct).ConfigureAwait(true);

        var stored = await repository.GetAsync(session.Id, ct).ConfigureAwait(true);
        stored.ShouldNotBeNull();
        stored.Status.ShouldBe(SessionStatus.Running);
        stored.Messages.Select(static message => message.Content).ShouldBe(["first", "second"]);
    }

    /// <summary>A steer during a running prompt is accepted and does not change the running status.</summary>
    [Fact]
    public async Task SteerDuringRunningIsAccepted()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = new InMemorySessionRepository();
        var service = NewService(repository);
        var session = await service.CreateAsync("dev", ct).ConfigureAwait(true);
        await service.SubmitPromptAsync(session.Id, "first", ct).ConfigureAwait(true);

        await service.SubmitSteerAsync(session.Id, "use the other file", ct).ConfigureAwait(true);

        var stored = await repository.GetAsync(session.Id, ct).ConfigureAwait(true);
        stored.ShouldNotBeNull();
        stored.Status.ShouldBe(SessionStatus.Running);
        stored.Messages.Select(static message => message.Role).ShouldBe(["user", "steer"]);
        stored.Events.Select(static evt => evt.Payload).OfType<UnknownPayload>().Select(static payload => payload.OriginalKind).ShouldBe(["prompt_accepted", "steer"]);
    }

    /// <summary>A follow-up during a running prompt is accepted; it is exempt from PromptInFlight and is queued.</summary>
    [Fact]
    public async Task FollowUpDuringRunningIsQueuedNotRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = new InMemorySessionRepository();
        var service = NewService(repository);
        var session = await service.CreateAsync("dev", ct).ConfigureAwait(true);
        await service.SubmitPromptAsync(session.Id, "first", ct).ConfigureAwait(true);

        await service.SubmitFollowUpAsync(session.Id, "then summarize", ct).ConfigureAwait(true);

        var stored = await repository.GetAsync(session.Id, ct).ConfigureAwait(true);
        stored.ShouldNotBeNull();
        stored.Status.ShouldBe(SessionStatus.Running);
        stored.Messages.Select(static message => message.Role).ShouldBe(["user", "follow_up"]);
    }

    /// <summary>A steer with no active prompt is rejected with InvalidTransition.</summary>
    [Fact]
    public async Task SteerWhileIdleIsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = NewService(new InMemorySessionRepository());
        var session = await service.CreateAsync("dev", ct).ConfigureAwait(true);

        var error = await Should.ThrowAsync<SessionException>(() =>
            service.SubmitSteerAsync(session.Id, "nudge", ct)).ConfigureAwait(true);

        error.Error.Code.ShouldBe(ErrorCode.InvalidTransition);
    }

    /// <summary>Operations on an unknown session are rejected with SessionNotFound.</summary>
    [Fact]
    public async Task MissingSessionIsRejectedWithSessionNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = NewService(new InMemorySessionRepository());

        var prompt = await Should.ThrowAsync<SessionException>(() => service.SubmitPromptAsync("absent", "hi", ct)).ConfigureAwait(true);
        var steer = await Should.ThrowAsync<SessionException>(() => service.SubmitSteerAsync("absent", "hi", ct)).ConfigureAwait(true);
        var complete = await Should.ThrowAsync<SessionException>(() => service.RecordCompletionAsync("absent", SessionStatus.Completed, ct)).ConfigureAwait(true);

        prompt.Error.Code.ShouldBe(ErrorCode.SessionNotFound);
        steer.Error.Code.ShouldBe(ErrorCode.SessionNotFound);
        complete.Error.Code.ShouldBe(ErrorCode.SessionNotFound);
    }

    /// <summary>Completing a session that is idle is rejected with InvalidTransition and changes nothing.</summary>
    [Fact]
    public async Task CompletionFromIdleIsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = new InMemorySessionRepository();
        var service = NewService(repository);
        var session = await service.CreateAsync("dev", ct).ConfigureAwait(true);

        var error = await Should.ThrowAsync<SessionException>(() =>
            service.RecordCompletionAsync(session.Id, SessionStatus.Completed, ct)).ConfigureAwait(true);

        error.Error.Code.ShouldBe(ErrorCode.InvalidTransition);
        var stored = await repository.GetAsync(session.Id, ct).ConfigureAwait(true);
        stored.ShouldNotBeNull();
        stored.Status.ShouldBe(SessionStatus.Idle);
        stored.Events.ShouldBeEmpty();
    }

    /// <summary>Completing with a non-terminal status is rejected with InvalidTransition.</summary>
    [Fact]
    public async Task CompletionToNonTerminalStatusIsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = NewService(new InMemorySessionRepository());
        var session = await service.CreateAsync("dev", ct).ConfigureAwait(true);
        await service.SubmitPromptAsync(session.Id, "first", ct).ConfigureAwait(true);

        var error = await Should.ThrowAsync<SessionException>(() =>
            service.RecordCompletionAsync(session.Id, SessionStatus.Running, ct)).ConfigureAwait(true);

        error.Error.Code.ShouldBe(ErrorCode.InvalidTransition);
    }

    /// <summary>Settling a running prompt records the terminal status and a completed event with the reason.</summary>
    [Fact]
    public async Task CompletionSettlesRunningPromptAndAppendsCompletedEvent()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = new InMemorySessionRepository();
        var service = NewService(repository);
        var session = await service.CreateAsync("dev", ct).ConfigureAwait(true);
        await service.SubmitPromptAsync(session.Id, "first", ct).ConfigureAwait(true);

        await service.RecordCompletionAsync(session.Id, SessionStatus.Completed, ct).ConfigureAwait(true);

        var stored = await repository.GetAsync(session.Id, ct).ConfigureAwait(true);
        stored.ShouldNotBeNull();
        stored.Status.ShouldBe(SessionStatus.Completed);
        stored.Events.Select(static evt => evt.Seq).ToArray().ShouldBe([1L, 2L]);
        var completed = stored.Events[1].Payload.ShouldBeOfType<CompletedPayload>();
        completed.Reason.ShouldBe("completed");
        stored.Events[1].CorrelationId.ShouldBe(stored.Events[0].CorrelationId);
    }

    private static SessionService NewService(ISessionRepository repository) =>
        new(repository, new FixedTimeProvider(Now));
}
