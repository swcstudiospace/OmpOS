using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Contracts.Streaming;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Sessions;
using Shouldly;
using Xunit;

namespace Autonogrammer.SessionRuns.Tests;

/// <summary>
/// Routed tool approval of <see cref="SessionRunActivities"/>: a permission request is held in the store until a decision
/// arrives, and the default outcome is deny.
/// </summary>
public sealed class ToolApprovalRunTests
{
    /// <summary>
    /// A request that no decision reaches within the timeout is denied, the session is Running again before the stream resumes,
    /// and the run settles Completed.
    /// </summary>
    [Fact]
    public async Task RequestWithoutDecisionIsDeniedAtTimeoutAndSessionRunsAgain()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = new InMemorySessionRepository();
        var sessions = new SessionService(repository, TimeProvider.System);
        var sessionId = await RunFixtures.StartPromptAsync(sessions, "list files", ct).ConfigureAwait(true);
        var statusAfterRequest = new List<SessionStatus?>();
        var harness = new ScriptedHarnessFactory(
            [
                new PermissionRequestEvent("call-1", "bash", "{\"command\":\"ls\"}", RequestId: "req-1"),
                new CompletedEvent("end_turn"),
            ],
            afterEvent: async evt =>
            {
                // Runs when the stream resumes after the permission request, which is after its approval wait settled.
                if (evt is PermissionRequestEvent)
                {
                    var session = await repository.GetAsync(sessionId, ct).ConfigureAwait(true);
                    statusAfterRequest.Add(session?.Status);
                }
            });
        var activities = RunFixtures.Activities(
            new RecordingSandboxProvider(),
            harness,
            repository,
            sessions,
            new RecordingObserver(),
            TimeSpan.FromMilliseconds(100));

        var lastSeq = await activities.RunPromptAsync(RunFixtures.InputFor(sessionId, "list files"), static _ => { }, ct).ConfigureAwait(true);

        statusAfterRequest.ToArray().ShouldBe([SessionStatus.Running]);
        lastSeq.ShouldBe(4L);
        var stored = await repository.GetAsync(sessionId, ct).ConfigureAwait(true);
        stored.ShouldNotBeNull();
        stored.Status.ShouldBe(SessionStatus.Completed);
        var payloads = stored.Events.Select(static evt => evt.Payload).ToArray();
        payloads[1].ShouldBeOfType<UnknownPayload>().OriginalKind.ShouldBe("tool_approval_requested");
        payloads[2].ShouldBeOfType<UnknownPayload>().OriginalKind.ShouldBe("tool_approval_denied");
        payloads[3].ShouldBe(new CompletedPayload { Reason = "end_turn" });
    }

    /// <summary>
    /// A decision from a second session service over the same store reaches the waiting run. An approval above the request's
    /// ceiling is refused and the request stays pending; the denial that follows settles the wait and the run completes.
    /// </summary>
    [Fact]
    public async Task DecisionFromAnotherServiceOverTheSameStoreSettlesTheWait()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = new InMemorySessionRepository();
        var sessions = new SessionService(repository, TimeProvider.System);
        var sessionId = await RunFixtures.StartPromptAsync(sessions, "edit file", ct).ConfigureAwait(true);
        var harness = new ScriptedHarnessFactory([
            new PermissionRequestEvent("call-9", "write", "{}"),
            new CompletedEvent("end_turn"),
        ]);
        var activities = RunFixtures.Activities(new RecordingSandboxProvider(), harness, repository, sessions, new RecordingObserver());
        var run = activities.RunPromptAsync(RunFixtures.InputFor(sessionId, "edit file"), static _ => { }, ct);

        await RunFixtures.WaitForStatusAsync(repository, sessionId, SessionStatus.AwaitingToolApproval, ct).ConfigureAwait(true);
        var other = new SessionService(repository, TimeProvider.System);

        // The run scopes its request identifier to the run: "<RunId>:<call id>", since this request carries no request identifier of its own.
        await Should.ThrowAsync<UnauthorizedAccessException>(
            () => other.ResolveToolApprovalAsync(sessionId, "run-1:call-9", approved: true, ct)).ConfigureAwait(true);
        var refused = await repository.GetAsync(sessionId, ct).ConfigureAwait(true);
        refused.ShouldNotBeNull();
        refused.Status.ShouldBe(SessionStatus.AwaitingToolApproval);

        await other.ResolveToolApprovalAsync(sessionId, "run-1:call-9", approved: false, ct).ConfigureAwait(true);
        var lastSeq = await run.ConfigureAwait(true);

        lastSeq.ShouldBe(4L);
        var stored = await repository.GetAsync(sessionId, ct).ConfigureAwait(true);
        stored.ShouldNotBeNull();
        stored.Status.ShouldBe(SessionStatus.Completed);
        var payloads = stored.Events.Select(static evt => evt.Payload).ToArray();
        payloads[2].ShouldBeOfType<UnknownPayload>().OriginalKind.ShouldBe("tool_approval_denied");
        payloads[3].ShouldBe(new CompletedPayload { Reason = "end_turn" });
    }
}
