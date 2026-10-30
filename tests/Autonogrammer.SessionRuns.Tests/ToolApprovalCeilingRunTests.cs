using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Contracts.Streaming;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Sessions;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.SessionRuns.Tests;

/// <summary>
/// The tool approval policy of <see cref="SessionRunActivities"/>: the <see cref="IToolApprovalSettings"/> ceiling is stored with every routed
/// request and bounds what a decision may allow, and its timeout ends the wait. Without settings the ceiling is empty, as before.
/// </summary>
public sealed class ToolApprovalCeilingRunTests
{
    private const string RequestId = "run-1:call-9";

    /// <summary>Without settings the stored ceiling is empty, so an allow is refused and a deny settles the wait.</summary>
    [Fact]
    public async Task WithoutSettingsTheStoredCeilingIsEmptyAndAllowIsRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = new InMemorySessionRepository();
        var sessions = new SessionService(repository, TimeProvider.System);
        var sessionId = await RunFixtures.StartPromptAsync(sessions, "edit file", ct).ConfigureAwait(true);
        var activities = Activities(repository, sessions, settings: null);
        var run = activities.RunPromptAsync(RunFixtures.InputFor(sessionId, "edit file"), static _ => { }, ct);

        await RunFixtures.WaitForStatusAsync(repository, sessionId, SessionStatus.AwaitingToolApproval, ct).ConfigureAwait(true);

        var pending = await repository.GetToolApprovalAsync(sessionId, RequestId, ct).ConfigureAwait(true);
        pending.ShouldNotBeNull().GrantableTools.ShouldBeEmpty();
        await Should.ThrowAsync<UnauthorizedAccessException>(() => sessions.ResolveToolApprovalAsync(sessionId, RequestId, approved: true, ct)).ConfigureAwait(true);
        await sessions.ResolveToolApprovalAsync(sessionId, RequestId, approved: false, ct).ConfigureAwait(true);
        _ = await run.ConfigureAwait(true);
    }

    /// <summary>The settings' ceiling is passed to the request: the stored request lists it, and an allow for the named tool settles the wait.</summary>
    [Fact]
    public async Task SettingsCeilingIsStoredWithTheRequestAndPermitsTheNamedTool()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = new InMemorySessionRepository();
        var sessions = new SessionService(repository, TimeProvider.System);
        var sessionId = await RunFixtures.StartPromptAsync(sessions, "edit file", ct).ConfigureAwait(true);
        var activities = Activities(repository, sessions, new FixedSettings(RunFixtures.DefaultApprovalTimeout, "write"));
        var run = activities.RunPromptAsync(RunFixtures.InputFor(sessionId, "edit file"), static _ => { }, ct);

        await RunFixtures.WaitForStatusAsync(repository, sessionId, SessionStatus.AwaitingToolApproval, ct).ConfigureAwait(true);

        var pending = await repository.GetToolApprovalAsync(sessionId, RequestId, ct).ConfigureAwait(true);
        pending.ShouldNotBeNull().GrantableTools.ShouldBe(["write"]);
        await sessions.ResolveToolApprovalAsync(sessionId, RequestId, approved: true, ct).ConfigureAwait(true);
        _ = await run.ConfigureAwait(true);

        var stored = await repository.GetAsync(sessionId, ct).ConfigureAwait(true);
        stored.ShouldNotBeNull().Status.ShouldBe(SessionStatus.Completed);
        stored.Events.Select(static evt => evt.Payload).OfType<UnknownPayload>().Select(static payload => payload.OriginalKind).ShouldContain("tool_approval_approved", StringComparer.Ordinal);
    }

    /// <summary>A ceiling that names another tool refuses the allow, and the request stays pending.</summary>
    [Fact]
    public async Task SettingsCeilingThatNamesAnotherToolRefusesTheAllow()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = new InMemorySessionRepository();
        var sessions = new SessionService(repository, TimeProvider.System);
        var sessionId = await RunFixtures.StartPromptAsync(sessions, "edit file", ct).ConfigureAwait(true);
        var activities = Activities(repository, sessions, new FixedSettings(RunFixtures.DefaultApprovalTimeout, "bash"));
        var run = activities.RunPromptAsync(RunFixtures.InputFor(sessionId, "edit file"), static _ => { }, ct);

        await RunFixtures.WaitForStatusAsync(repository, sessionId, SessionStatus.AwaitingToolApproval, ct).ConfigureAwait(true);

        await Should.ThrowAsync<UnauthorizedAccessException>(() => sessions.ResolveToolApprovalAsync(sessionId, RequestId, approved: true, ct)).ConfigureAwait(true);
        (await repository.GetToolApprovalAsync(sessionId, RequestId, ct).ConfigureAwait(true)).ShouldNotBeNull().Decision.ShouldBeNull();
        await sessions.ResolveToolApprovalAsync(sessionId, RequestId, approved: false, ct).ConfigureAwait(true);
        _ = await run.ConfigureAwait(true);
    }

    /// <summary>The wildcard ceiling permits any tool the run routes.</summary>
    [Fact]
    public async Task WildcardSettingsPermitAnyTool()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = new InMemorySessionRepository();
        var sessions = new SessionService(repository, TimeProvider.System);
        var sessionId = await RunFixtures.StartPromptAsync(sessions, "edit file", ct).ConfigureAwait(true);
        var activities = Activities(repository, sessions, new FixedSettings(RunFixtures.DefaultApprovalTimeout, SessionService.AnyToolCeiling));
        var run = activities.RunPromptAsync(RunFixtures.InputFor(sessionId, "edit file"), static _ => { }, ct);

        await RunFixtures.WaitForStatusAsync(repository, sessionId, SessionStatus.AwaitingToolApproval, ct).ConfigureAwait(true);

        await sessions.ResolveToolApprovalAsync(sessionId, RequestId, approved: true, ct).ConfigureAwait(true);
        _ = await run.ConfigureAwait(true);
        (await repository.GetToolApprovalAsync(sessionId, RequestId, ct).ConfigureAwait(true)).ShouldNotBeNull().Decision.ShouldBe(PermissionDecision.Allow);
    }

    /// <summary>The settings' decision timeout ends the wait: an undecided request is denied and the run settles Completed.</summary>
    [Fact]
    public async Task SettingsDecisionTimeoutDeniesAnUndecidedRequest()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = new InMemorySessionRepository();
        var sessions = new SessionService(repository, TimeProvider.System);
        var sessionId = await RunFixtures.StartPromptAsync(sessions, "edit file", ct).ConfigureAwait(true);
        var activities = Activities(repository, sessions, new FixedSettings(TimeSpan.FromMilliseconds(100), SessionService.AnyToolCeiling));

        _ = await activities.RunPromptAsync(RunFixtures.InputFor(sessionId, "edit file"), static _ => { }, ct).ConfigureAwait(true);

        (await repository.GetToolApprovalAsync(sessionId, RequestId, ct).ConfigureAwait(true)).ShouldNotBeNull().Decision.ShouldBe(PermissionDecision.Deny);
        (await repository.GetAsync(sessionId, ct).ConfigureAwait(true)).ShouldNotBeNull().Status.ShouldBe(SessionStatus.Completed);
    }

    private static SessionRunActivities Activities(InMemorySessionRepository repository, SessionService sessions, IToolApprovalSettings? settings) =>
        new(
            new RecordingSandboxProvider(),
            new ScriptedHarnessFactory([new PermissionRequestEvent("call-9", "write", "{}"), new CompletedEvent("end_turn")]),
            repository,
            sessions,
            TimeProvider.System,
            new RecordingObserver(),
            NullLogger<SessionRunActivities>.Instance,
            settings);

    /// <summary>Fixed approval settings for a test.</summary>
    private sealed class FixedSettings(TimeSpan decisionTimeout, params string[] grantableTools) : IToolApprovalSettings
    {
        /// <inheritdoc />
        public TimeSpan DecisionTimeout { get; } = decisionTimeout;

        /// <inheritdoc />
        public IReadOnlySet<string> GrantableTools { get; } = new HashSet<string>(grantableTools, StringComparer.Ordinal);
    }
}
