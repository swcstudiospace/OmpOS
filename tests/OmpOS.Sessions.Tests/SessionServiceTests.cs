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
        var request = await Should.ThrowAsync<SessionException>(() => service.RequestToolApprovalAsync("absent", ct)).ConfigureAwait(true);
        var resolve = await Should.ThrowAsync<SessionException>(() => service.ResolveToolApprovalAsync("absent", approved: true, ct)).ConfigureAwait(true);

        prompt.Error.Code.ShouldBe(ErrorCode.SessionNotFound);
        steer.Error.Code.ShouldBe(ErrorCode.SessionNotFound);
        complete.Error.Code.ShouldBe(ErrorCode.SessionNotFound);
        request.Error.Code.ShouldBe(ErrorCode.SessionNotFound);
        resolve.Error.Code.ShouldBe(ErrorCode.SessionNotFound);
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

    /// <summary>An approved tool decision resumes the prompt, which then settles normally.</summary>
    [Fact]
    public async Task ToolApprovalApproveResumesAndCompletes()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = new InMemorySessionRepository();
        var service = NewService(repository);
        var session = await service.CreateAsync("dev", ct).ConfigureAwait(true);
        await service.SubmitPromptAsync(session.Id, "first", ct).ConfigureAwait(true);

        await service.RequestToolApprovalAsync(session.Id, ct).ConfigureAwait(true);

        var waiting = await repository.GetAsync(session.Id, ct).ConfigureAwait(true);
        waiting.ShouldNotBeNull();
        waiting.Status.ShouldBe(SessionStatus.AwaitingToolApproval);

        await service.ResolveToolApprovalAsync(session.Id, approved: true, ct).ConfigureAwait(true);

        var resumed = await repository.GetAsync(session.Id, ct).ConfigureAwait(true);
        resumed.ShouldNotBeNull();
        resumed.Status.ShouldBe(SessionStatus.Running);

        await service.RecordCompletionAsync(session.Id, SessionStatus.Completed, ct).ConfigureAwait(true);

        var stored = await repository.GetAsync(session.Id, ct).ConfigureAwait(true);
        stored.ShouldNotBeNull();
        stored.Status.ShouldBe(SessionStatus.Completed);
        stored.Events.Select(static evt => evt.Seq).ToArray().ShouldBe([1L, 2L, 3L, 4L]);
        stored.Events.Select(static evt => evt.Payload).OfType<UnknownPayload>().Select(static payload => payload.OriginalKind)
            .ShouldBe(["prompt_accepted", "tool_approval_requested", "tool_approval_approved"]);
    }

    /// <summary>A denied tool decision records the denial, leaves the session running, and the turn still settles.</summary>
    [Fact]
    public async Task ToolApprovalDenyReturnsToRunningThenCompletes()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = new InMemorySessionRepository();
        var service = NewService(repository);
        var session = await service.CreateAsync("dev", ct).ConfigureAwait(true);
        await service.SubmitPromptAsync(session.Id, "first", ct).ConfigureAwait(true);
        await service.RequestToolApprovalAsync(session.Id, ct).ConfigureAwait(true);

        await service.ResolveToolApprovalAsync(session.Id, approved: false, ct).ConfigureAwait(true);

        var resumed = await repository.GetAsync(session.Id, ct).ConfigureAwait(true);
        resumed.ShouldNotBeNull();
        resumed.Status.ShouldBe(SessionStatus.Running);
        resumed.Events.Select(static evt => evt.Payload).OfType<UnknownPayload>().Select(static payload => payload.OriginalKind)
            .ShouldBe(["prompt_accepted", "tool_approval_requested", "tool_approval_denied"]);

        await service.RecordCompletionAsync(session.Id, SessionStatus.Completed, ct).ConfigureAwait(true);

        var stored = await repository.GetAsync(session.Id, ct).ConfigureAwait(true);
        stored.ShouldNotBeNull();
        stored.Status.ShouldBe(SessionStatus.Completed);
    }

    /// <summary>Approval transitions outside Running/AwaitingToolApproval are rejected with InvalidTransition.</summary>
    [Fact]
    public async Task ToolApprovalInvalidTransitionsAreRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = new InMemorySessionRepository();
        var service = NewService(repository);
        var session = await service.CreateAsync("dev", ct).ConfigureAwait(true);

        var requestWhileIdle = await Should.ThrowAsync<SessionException>(() =>
            service.RequestToolApprovalAsync(session.Id, ct)).ConfigureAwait(true);
        requestWhileIdle.Error.Code.ShouldBe(ErrorCode.InvalidTransition);

        var resolveWhileIdle = await Should.ThrowAsync<SessionException>(() =>
            service.ResolveToolApprovalAsync(session.Id, approved: true, ct)).ConfigureAwait(true);
        resolveWhileIdle.Error.Code.ShouldBe(ErrorCode.InvalidTransition);

        await service.SubmitPromptAsync(session.Id, "first", ct).ConfigureAwait(true);

        var resolveWhileRunning = await Should.ThrowAsync<SessionException>(() =>
            service.ResolveToolApprovalAsync(session.Id, approved: true, ct)).ConfigureAwait(true);
        resolveWhileRunning.Error.Code.ShouldBe(ErrorCode.InvalidTransition);

        await service.RequestToolApprovalAsync(session.Id, ct).ConfigureAwait(true);

        var secondRequest = await Should.ThrowAsync<SessionException>(() =>
            service.RequestToolApprovalAsync(session.Id, ct)).ConfigureAwait(true);
        secondRequest.Error.Code.ShouldBe(ErrorCode.InvalidTransition);

        await service.ResolveToolApprovalAsync(session.Id, approved: true, ct).ConfigureAwait(true);
        await service.RecordCompletionAsync(session.Id, SessionStatus.Completed, ct).ConfigureAwait(true);

        var requestWhenTerminal = await Should.ThrowAsync<SessionException>(() =>
            service.RequestToolApprovalAsync(session.Id, ct)).ConfigureAwait(true);
        requestWhenTerminal.Error.Code.ShouldBe(ErrorCode.InvalidTransition);
    }

    /// <summary>A prompt submitted while the session awaits tool approval is rejected with PromptInFlight.</summary>
    [Fact]
    public async Task PromptWhileAwaitingApprovalViaApiIsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = new InMemorySessionRepository();
        var service = NewService(repository);
        var session = await service.CreateAsync("dev", ct).ConfigureAwait(true);
        await service.SubmitPromptAsync(session.Id, "first", ct).ConfigureAwait(true);
        await service.RequestToolApprovalAsync(session.Id, ct).ConfigureAwait(true);

        var error = await Should.ThrowAsync<SessionException>(() =>
            service.SubmitPromptAsync(session.Id, "second", ct)).ConfigureAwait(true);

        error.Error.Code.ShouldBe(ErrorCode.PromptInFlight);
        var stored = await repository.GetAsync(session.Id, ct).ConfigureAwait(true);
        stored.ShouldNotBeNull();
        stored.Status.ShouldBe(SessionStatus.AwaitingToolApproval);
        stored.Messages.Select(static message => message.Content).ShouldBe(["first"]);
    }

    /// <summary>Concurrent prompts on distinct sessions each admit exactly one: stripes serialize without cross-talk.</summary>
    [Fact]
    public async Task ConcurrentPromptsOnDistinctSessionsEachAdmitOne()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = new InMemorySessionRepository();
        var service = NewService(repository);
        var sessions = new List<Session>();
        for (var i = 0; i < 8; i++)
        {
            sessions.Add(await service.CreateAsync("dev", ct).ConfigureAwait(true));
        }

        var outcomes = await Task.WhenAll(sessions.SelectMany(session =>
            Enumerable.Range(0, 4).Select(i => Task.Run(async () =>
            {
                try
                {
                    await service.SubmitPromptAsync(session.Id, $"prompt {i}", ct).ConfigureAwait(false);
                    return (SessionId: session.Id, Admitted: true, Code: (ErrorCode?)null);
                }
                catch (SessionException ex)
                {
                    return (SessionId: session.Id, Admitted: false, Code: (ErrorCode?)ex.Error.Code);
                }
            }, ct)))).ConfigureAwait(true);

        foreach (var session in sessions)
        {
            var perSession = outcomes.Where(outcome => string.Equals(outcome.SessionId, session.Id, StringComparison.Ordinal)).ToArray();
            perSession.Count(static outcome => outcome.Admitted).ShouldBe(1);
            perSession.Where(static outcome => !outcome.Admitted).Select(static outcome => outcome.Code)
                .ShouldAllBe(code => code == ErrorCode.PromptInFlight);
        }
    }

    /// <summary>A completed turn writes the harness stop reason to its single completed event.</summary>
    [Fact]
    public async Task CompletionRecordsHarnessStopReason()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = new InMemorySessionRepository();
        var service = NewService(repository);
        var session = await service.CreateAsync("dev", ct).ConfigureAwait(true);
        await service.SubmitPromptAsync(session.Id, "first", ct).ConfigureAwait(true);

        await service.RecordCompletionAsync(session.Id, SessionStatus.Completed, ct, "end_turn").ConfigureAwait(true);

        var stored = await repository.GetAsync(session.Id, ct).ConfigureAwait(true);
        stored.ShouldNotBeNull();
        stored.Status.ShouldBe(SessionStatus.Completed);
        stored.Events.Select(static evt => evt.Payload).OfType<CompletedPayload>().Select(static payload => payload.Reason).ShouldBe(["end_turn"]);
        stored.Events.Count(static evt => IsTerminal(evt.Payload)).ShouldBe(1);
    }

    /// <summary>A failed turn ends with its error event and carries no completed event.</summary>
    [Fact]
    public async Task FailedTurnEndsWithErrorAndNoCompletedEvent()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = new InMemorySessionRepository();
        var service = NewService(repository);
        var session = await service.CreateAsync("dev", ct).ConfigureAwait(true);
        await service.SubmitPromptAsync(session.Id, "first", ct).ConfigureAwait(true);

        await service.RecordCompletionAsync(session.Id, SessionStatus.Failed, ct, "error").ConfigureAwait(true);

        var stored = await repository.GetAsync(session.Id, ct).ConfigureAwait(true);
        stored.ShouldNotBeNull();
        stored.Status.ShouldBe(SessionStatus.Failed);
        stored.Events[^1].Payload.ShouldBeOfType<ErrorPayload>().Message.ShouldBe("Prompt execution failed.");
        stored.Events.Select(static evt => evt.Payload).OfType<CompletedPayload>().ShouldBeEmpty();
    }

    /// <summary>An error streamed during the turn is its only terminal event, so settling the failed turn adds none.</summary>
    [Fact]
    public async Task StreamedErrorIsTheOnlyTerminalEventWhenTheTurnFails()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = new InMemorySessionRepository();
        var service = NewService(repository);
        var session = await service.CreateAsync("dev", ct).ConfigureAwait(true);
        await service.SubmitPromptAsync(session.Id, "first", ct).ConfigureAwait(true);
        await repository.AppendAsync(session.Id, StreamedEvent(session.Id, new ErrorPayload { Message = "rate limited", Retryable = true }), ct).ConfigureAwait(true);

        await service.RecordCompletionAsync(session.Id, SessionStatus.Failed, ct, "error").ConfigureAwait(true);

        var stored = await repository.GetAsync(session.Id, ct).ConfigureAwait(true);
        stored.ShouldNotBeNull();
        stored.Status.ShouldBe(SessionStatus.Failed);
        stored.Events.Select(static evt => evt.Payload).OfType<ErrorPayload>().Select(static payload => payload.Message).ShouldBe(["rate limited"]);
        stored.Events.Select(static evt => evt.Payload).OfType<CompletedPayload>().ShouldBeEmpty();
    }

    /// <summary>A completion after a streamed error appends no completed event, so the turn never carries both terminal events.</summary>
    [Fact]
    public async Task CompletionAfterStreamedErrorAppendsNoCompletedEvent()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = new InMemorySessionRepository();
        var service = NewService(repository);
        var session = await service.CreateAsync("dev", ct).ConfigureAwait(true);
        await service.SubmitPromptAsync(session.Id, "first", ct).ConfigureAwait(true);
        await repository.AppendAsync(session.Id, StreamedEvent(session.Id, new ErrorPayload { Message = "transient", Retryable = true }), ct).ConfigureAwait(true);

        await service.RecordCompletionAsync(session.Id, SessionStatus.Completed, ct, "end_turn").ConfigureAwait(true);

        var stored = await repository.GetAsync(session.Id, ct).ConfigureAwait(true);
        stored.ShouldNotBeNull();
        stored.Events.Count(static evt => IsTerminal(evt.Payload)).ShouldBe(1);
        stored.Events.Select(static evt => evt.Payload).OfType<CompletedPayload>().ShouldBeEmpty();
    }

    /// <summary>The terminal event is in the log before the settled status is saved, so a subscriber that sees the settled status finds it.</summary>
    [Theory]
    [InlineData(SessionStatus.Completed)]
    [InlineData(SessionStatus.Failed)]
    [InlineData(SessionStatus.Cancelled)]
    public async Task TerminalEventIsLoggedBeforeSettledStatusIsSaved(SessionStatus outcome)
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = new ObservingSessionRepository();
        var service = NewService(repository);
        var session = await service.CreateAsync("dev", ct).ConfigureAwait(true);
        await service.SubmitPromptAsync(session.Id, "first", ct).ConfigureAwait(true);

        await service.RecordCompletionAsync(session.Id, outcome, ct).ConfigureAwait(true);

        repository.SettledSaveTerminalFlags.ToArray().ShouldBe([true]);
    }

    private static SessionService NewService(ISessionRepository repository) =>
        new(repository, new FixedTimeProvider(Now));

    private static bool IsTerminal(StreamEventPayload payload) => payload is CompletedPayload or ErrorPayload;

    private static StreamEvent StreamedEvent(string sessionId, StreamEventPayload payload) => new()
    {
        Seq = 0,
        SessionId = sessionId,
        CorrelationId = $"{sessionId}/turn-1",
        Timestamp = Now,
        Payload = payload,
    };
}

