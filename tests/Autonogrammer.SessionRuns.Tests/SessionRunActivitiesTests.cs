using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Contracts.Streaming;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Sessions;
using Shouldly;
using Xunit;

namespace Autonogrammer.SessionRuns.Tests;

/// <summary>Settlement, event ordering and sandbox lifecycle of <see cref="SessionRunActivities"/>, observed through the store.</summary>
public sealed class SessionRunActivitiesTests
{
    /// <summary>A completed turn streams its events into the log and settles the session Completed, with the terminal event last.</summary>
    [Fact]
    public async Task CompletedTurnSettlesCompletedThroughTheStore()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = new InMemorySessionRepository();
        var sessions = new SessionService(repository, TimeProvider.System);
        var sessionId = await RunFixtures.StartPromptAsync(sessions, "hello", ct).ConfigureAwait(true);
        var observer = new RecordingObserver();
        var harness = new ScriptedHarnessFactory([new TextDeltaEvent("hi"), new CompletedEvent("end_turn")]);
        var activities = RunFixtures.Activities(new RecordingSandboxProvider(), harness, repository, sessions, observer);

        var lastSeq = await activities.RunPromptAsync(RunFixtures.InputFor(sessionId, "hello"), static _ => { }, ct).ConfigureAwait(true);

        lastSeq.ShouldBe(3L);
        var stored = await repository.GetAsync(sessionId, ct).ConfigureAwait(true);
        stored.ShouldNotBeNull();
        stored.Status.ShouldBe(SessionStatus.Completed);
        stored.Events.Select(static evt => evt.Seq).ToArray().ShouldBe([1L, 2L, 3L]);
        stored.Events[2].Payload.ShouldBe(new CompletedPayload { Reason = "end_turn" });
        observer.SessionChanges.ShouldNotBeEmpty();
        observer.SessionChanges.Distinct(StringComparer.Ordinal).ToArray().ShouldBe([sessionId]);
    }

    /// <summary>The engine reports its context use at the end of a turn. The log keeps it under its own kind with the two numbers, not under the name of a CLR type.</summary>
    [Fact]
    public async Task ContextUsageIsStoredUnderItsOwnKind()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = new InMemorySessionRepository();
        var sessions = new SessionService(repository, TimeProvider.System);
        var sessionId = await RunFixtures.StartPromptAsync(sessions, "hello", ct).ConfigureAwait(true);
        var harness = new ScriptedHarnessFactory([new TextDeltaEvent("hi"), new ContextUsageEvent(1234, 200000), new CompletedEvent("end_turn")]);
        var activities = RunFixtures.Activities(new RecordingSandboxProvider(), harness, repository, sessions, new RecordingObserver());

        await activities.RunPromptAsync(RunFixtures.InputFor(sessionId, "hello"), static _ => { }, ct).ConfigureAwait(true);

        var stored = await repository.GetAsync(sessionId, ct).ConfigureAwait(true);
        stored.ShouldNotBeNull();
        var usage = stored.Events[2].Payload.ShouldBeOfType<UnknownPayload>();
        usage.OriginalKind.ShouldBe("context_usage");
        usage.Raw.GetProperty("used").GetString().ShouldBe("1234");
        usage.Raw.GetProperty("size").GetString().ShouldBe("200000");
    }

    /// <summary>Harness events are appended in stream order, each mapped to its log payload, under the run's correlation identifier.</summary>
    [Fact]
    public async Task HarnessEventsAreAppendedInOrderThroughTheMapper()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = new InMemorySessionRepository();
        var sessions = new SessionService(repository, TimeProvider.System);
        var sessionId = await RunFixtures.StartPromptAsync(sessions, "read it", ct).ConfigureAwait(true);
        var input = RunFixtures.InputFor(sessionId, "read it");
        var harness = new ScriptedHarnessFactory(
        [
            new TextDeltaEvent("a"),
            new DiagnosticEvent("slow"),
            new ToolCallEvent("call-1", "read", "{}"),
            new TextDeltaEvent("b"),
            new CompletedEvent("end_turn"),
        ]);
        var activities = RunFixtures.Activities(new RecordingSandboxProvider(), harness, repository, sessions, new RecordingObserver());

        var lastSeq = await activities.RunPromptAsync(input, static _ => { }, ct).ConfigureAwait(true);

        lastSeq.ShouldBe(6L);
        var stored = await repository.GetAsync(sessionId, ct).ConfigureAwait(true);
        stored.ShouldNotBeNull();
        stored.Events.Select(static evt => evt.Seq).ToArray().ShouldBe([1L, 2L, 3L, 4L, 5L, 6L]);
        stored.Events.Select(static evt => evt.CorrelationId).Distinct(StringComparer.Ordinal).ToArray().ShouldBe([input.CorrelationId]);
        stored.Events[1].Payload.ShouldBe(new TextDeltaPayload { Delta = "a" });
        stored.Events[2].Payload.ShouldBe(new DiagnosticPayload { Message = "slow" });
        var toolCall = stored.Events[3].Payload.ShouldBeOfType<UnknownPayload>();
        toolCall.OriginalKind.ShouldBe("tool_call");
        toolCall.Raw.GetProperty("callId").GetString().ShouldBe("call-1");
        toolCall.Raw.GetProperty("name").GetString().ShouldBe("read");
        stored.Events[4].Payload.ShouldBe(new TextDeltaPayload { Delta = "b" });
        stored.Events[5].Payload.ShouldBe(new CompletedPayload { Reason = "end_turn" });
    }

    /// <summary>A turn that streams an error settles Failed, and the error is its terminal event.</summary>
    [Fact]
    public async Task StreamedErrorSettlesFailedWithTheErrorAsTerminalEvent()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = new InMemorySessionRepository();
        var sessions = new SessionService(repository, TimeProvider.System);
        var sessionId = await RunFixtures.StartPromptAsync(sessions, "break", ct).ConfigureAwait(true);
        var harness = new ScriptedHarnessFactory([new TextDeltaEvent("partial"), new ErrorEvent("boom", false)]);
        var activities = RunFixtures.Activities(new RecordingSandboxProvider(), harness, repository, sessions, new RecordingObserver());

        var lastSeq = await activities.RunPromptAsync(RunFixtures.InputFor(sessionId, "break"), static _ => { }, ct).ConfigureAwait(true);

        lastSeq.ShouldBe(3L);
        var stored = await repository.GetAsync(sessionId, ct).ConfigureAwait(true);
        stored.ShouldNotBeNull();
        stored.Status.ShouldBe(SessionStatus.Failed);
        stored.Events[2].Payload.ShouldBe(new ErrorPayload { Message = "boom", Retryable = false });
    }

    /// <summary>A harness failure settles the session Failed, writes the failure as the terminal event, and rethrows for the runner.</summary>
    [Fact]
    public async Task HarnessFailureSettlesFailedAndRethrows()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = new InMemorySessionRepository();
        var sessions = new SessionService(repository, TimeProvider.System);
        var sessionId = await RunFixtures.StartPromptAsync(sessions, "fail", ct).ConfigureAwait(true);
        var harness = new ScriptedHarnessFactory([], new InvalidOperationException("harness unavailable"));
        var activities = RunFixtures.Activities(new RecordingSandboxProvider(), harness, repository, sessions, new RecordingObserver());

        var error = await Should.ThrowAsync<InvalidOperationException>(
            () => activities.RunPromptAsync(RunFixtures.InputFor(sessionId, "fail"), static _ => { }, ct)).ConfigureAwait(true);

        error.Message.ShouldBe("harness unavailable");
        var stored = await repository.GetAsync(sessionId, ct).ConfigureAwait(true);
        stored.ShouldNotBeNull();
        stored.Status.ShouldBe(SessionStatus.Failed);
        stored.Events[^1].Payload.ShouldBe(new ErrorPayload { Message = SessionService.FailedTurnMessage, Retryable = false });
    }

    /// <summary>A cancelled run settles the session Cancelled through the store and rethrows the cancellation.</summary>
    [Fact]
    public async Task CancelledRunSettlesCancelledAndRethrows()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = new InMemorySessionRepository();
        var sessions = new SessionService(repository, TimeProvider.System);
        var sessionId = await RunFixtures.StartPromptAsync(sessions, "stop", ct).ConfigureAwait(true);
        var harness = new ScriptedHarnessFactory([new TextDeltaEvent("never delivered")]);
        var activities = RunFixtures.Activities(new RecordingSandboxProvider(), harness, repository, sessions, new RecordingObserver());
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
        await cancellation.CancelAsync().ConfigureAwait(true);

        await Should.ThrowAsync<OperationCanceledException>(
            () => activities.RunPromptAsync(RunFixtures.InputFor(sessionId, "stop"), static _ => { }, cancellation.Token)).ConfigureAwait(true);

        var stored = await repository.GetAsync(sessionId, ct).ConfigureAwait(true);
        stored.ShouldNotBeNull();
        stored.Status.ShouldBe(SessionStatus.Cancelled);
        stored.Events[^1].Payload.ShouldBe(new CompletedPayload { Reason = "cancelled" });
    }

    /// <summary>The provisioned sandbox is reported to the observer, released by teardown, and a repeated teardown releases nothing.</summary>
    [Fact]
    public async Task ProvisionedSandboxIsReportedAndTornDown()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = new InMemorySessionRepository();
        var sessions = new SessionService(repository, TimeProvider.System);
        var sessionId = await RunFixtures.StartPromptAsync(sessions, "hello", ct).ConfigureAwait(true);
        var sandbox = new RecordingSandboxProvider();
        var observer = new RecordingObserver();
        var harness = new ScriptedHarnessFactory([new CompletedEvent("end_turn")]);
        var activities = RunFixtures.Activities(sandbox, harness, repository, sessions, observer);
        var input = RunFixtures.InputFor(sessionId, "hello");

        var descriptor = await activities.ProvisionSandboxAsync(input, ct).ConfigureAwait(true);
        await activities.RunPromptAsync(input, static _ => { }, ct).ConfigureAwait(true);
        await activities.TeardownSandboxAsync(input, ct).ConfigureAwait(true);
        await activities.TeardownSandboxAsync(input, ct).ConfigureAwait(true);

        descriptor.Id.ShouldBe(RunFixtures.SandboxId);
        sandbox.Provisions.Count.ShouldBe(1);
        var request = sandbox.Provisions[0];
        request.IdempotencyKey.ShouldBe(RunFixtures.IdempotencyKey);
        request.SessionId.ShouldBe(sessionId);
        request.Timeout.ShouldBe(TimeSpan.FromSeconds(30));
        request.Spec.Image.ShouldBe("autonogrammer/in-process");
        observer.ProvisionMilliseconds.Count.ShouldBe(1);
        observer.ProvisionMilliseconds[0].ShouldBeGreaterThanOrEqualTo(0d);
        sandbox.Teardowns.ToArray().ShouldBe([RunFixtures.SandboxId]);
    }

    /// <summary>A teardown that fails once is retried by the next attempt, and the sandbox mapping is removed only after the teardown succeeds.</summary>
    [Fact]
    public async Task TeardownFailedOnce_RetryTearsDownAndMappingRemovedOnlyAfterSuccess()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = new InMemorySessionRepository();
        var sessions = new SessionService(repository, TimeProvider.System);
        var sessionId = await RunFixtures.StartPromptAsync(sessions, "hello", ct).ConfigureAwait(true);
        var sandbox = new RecordingSandboxProvider { TeardownFailuresRemaining = 1 };
        var harness = new ScriptedHarnessFactory([new CompletedEvent("end_turn")]);
        var activities = RunFixtures.Activities(sandbox, harness, repository, sessions, new RecordingObserver());
        var input = RunFixtures.InputFor(sessionId, "hello");
        await activities.ProvisionSandboxAsync(input, ct).ConfigureAwait(true);

        await Assert.ThrowsAsync<InvalidOperationException>(() => activities.TeardownSandboxAsync(input, ct)).ConfigureAwait(true);
        sandbox.Teardowns.ShouldBeEmpty();

        await activities.TeardownSandboxAsync(input, ct).ConfigureAwait(true);
        sandbox.TeardownAttempts.ShouldBe(2);
        sandbox.Teardowns.ToArray().ShouldBe([RunFixtures.SandboxId]);

        await activities.TeardownSandboxAsync(input, ct).ConfigureAwait(true);
        sandbox.TeardownAttempts.ShouldBe(2);
    }
}
