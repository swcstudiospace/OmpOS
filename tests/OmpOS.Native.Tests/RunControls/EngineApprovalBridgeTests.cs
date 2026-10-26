using OmpOS.Harness.Abstractions;
using OmpOS.Native.Engine;
using OmpOS.Presentation.Approvals;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.RunControls;

/// <summary>
/// Decisions the run controls send to an engine's approval bridge. The engine parks each request in its permission resolver and
/// waits; a decision settles the parked request once, and the outcome the bridge reports decides what the request shows.
/// </summary>
public sealed class EngineApprovalBridgeTests
{
    private const string SessionId = "session-1";
    private const string RequestId = "request-1";
    private const string AllowedKind = "bash";

    /// <summary>Under policy DenyAll an approve is refused and never allowed, and the request stays pending for a later deny.</summary>
    [Fact]
    public async Task ApproveUnderDenyAllShowsRefusedAndNeverAllowed()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var resolver = new PendingPermissionResolver();
            var bridge = new EngineToolApprovalBridge(resolver, []);
            var parked = ParkRequestAsync(resolver, TestContext.Current.CancellationToken);
            var window = RunControlsHarness.Show(bridge);
            var approval = RunControlsHarness.AddRequest(window, RequestId);

            RunControlsHarness.Click(RunControlsHarness.Named(window, "Approve tool request"));
            await RunControlsHarness.UntilAsync(() => approval.Status != ToolApprovalStatus.Pending, "the refusal", TestContext.Current.CancellationToken).ConfigureAwait(true);

            approval.Status.ShouldBe(ToolApprovalStatus.Refused);
            approval.Status.ShouldNotBe(ToolApprovalStatus.Allowed);
            parked.IsCompleted.ShouldBeFalse();

            RunControlsHarness.Click(RunControlsHarness.Named(window, "Deny tool request"));
            await RunControlsHarness.UntilAsync(() => approval.Status == ToolApprovalStatus.Denied, "the deny", TestContext.Current.CancellationToken).ConfigureAwait(true);

            (await parked.ConfigureAwait(true)).ShouldBe(PermissionDecision.Deny);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>An allow is refused even when no request is parked, so the refusal does not depend on the request reaching the engine.</summary>
    [Fact]
    public async Task AnAllowIsRefusedUnderDenyAllEvenWithNoParkedRequest()
    {
        var bridge = new EngineToolApprovalBridge(new PendingPermissionResolver(), []);

        var outcome = await bridge.DecideAsync(SessionId, RequestId, ToolApprovalDecision.Allow, TestContext.Current.CancellationToken).ConfigureAwait(true);

        outcome.ShouldBe(ToolApprovalOutcome.Refused);
    }

    /// <summary>A deny settles the parked request as denied, and the bridge reports the decision accepted.</summary>
    [Fact]
    public async Task ADenyResolvesThePendingRequestAsAccepted()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var resolver = new PendingPermissionResolver();
            var bridge = new EngineToolApprovalBridge(resolver, [AllowedKind]);
            var parked = ParkRequestAsync(resolver, TestContext.Current.CancellationToken);
            var window = RunControlsHarness.Show(bridge);
            var approval = RunControlsHarness.AddRequest(window, RequestId);

            RunControlsHarness.Click(RunControlsHarness.Named(window, "Deny tool request"));
            await RunControlsHarness.UntilAsync(() => approval.Status != ToolApprovalStatus.Pending, "the deny", TestContext.Current.CancellationToken).ConfigureAwait(true);
            await RunControlsHarness.UntilAsync(() => !RunControlsHarness.Named(window, "Approve tool request").IsEnabled, "the request to settle", TestContext.Current.CancellationToken).ConfigureAwait(true);

            approval.Status.ShouldBe(ToolApprovalStatus.Denied);
            (await parked.ConfigureAwait(true)).ShouldBe(PermissionDecision.Deny);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Under a policy that allows a tool kind, an approve settles the parked request as allowed.</summary>
    [Fact]
    public async Task AnApproveUnderAnAllowingPolicySettlesTheRequestAsAllowed()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var resolver = new PendingPermissionResolver();
            var bridge = new EngineToolApprovalBridge(resolver, [AllowedKind]);
            var parked = ParkRequestAsync(resolver, TestContext.Current.CancellationToken);
            var window = RunControlsHarness.Show(bridge);
            var approval = RunControlsHarness.AddRequest(window, RequestId);

            RunControlsHarness.Click(RunControlsHarness.Named(window, "Approve tool request"));
            await RunControlsHarness.UntilAsync(() => approval.Status != ToolApprovalStatus.Pending, "the allow", TestContext.Current.CancellationToken).ConfigureAwait(true);

            approval.Status.ShouldBe(ToolApprovalStatus.Allowed);
            (await parked.ConfigureAwait(true)).ShouldBe(PermissionDecision.Allow);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A second decision on a request the engine already settled reports AlreadyDecided, and never allows it.</summary>
    [Fact]
    public async Task ARepeatedDecisionReportsAlreadyDecided()
    {
        var resolver = new PendingPermissionResolver();
        var bridge = new EngineToolApprovalBridge(resolver, [AllowedKind]);
        var parked = ParkRequestAsync(resolver, TestContext.Current.CancellationToken);

        var first = await bridge.DecideAsync(SessionId, RequestId, ToolApprovalDecision.Deny, TestContext.Current.CancellationToken).ConfigureAwait(true);
        var second = await bridge.DecideAsync(SessionId, RequestId, ToolApprovalDecision.Deny, TestContext.Current.CancellationToken).ConfigureAwait(true);
        var allowAfter = await bridge.DecideAsync(SessionId, RequestId, ToolApprovalDecision.Allow, TestContext.Current.CancellationToken).ConfigureAwait(true);

        first.ShouldBe(ToolApprovalOutcome.Accepted);
        second.ShouldBe(ToolApprovalOutcome.AlreadyDecided);
        allowAfter.ShouldBe(ToolApprovalOutcome.AlreadyDecided);
        (await parked.ConfigureAwait(true)).ShouldBe(PermissionDecision.Deny);
    }

    /// <summary>A request the engine has already cancelled is no longer pending, so the user's late deny shows AlreadyDecided.</summary>
    [Fact]
    public async Task ADecisionOnARequestTheEngineCancelledShowsAlreadyDecided()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var resolver = new PendingPermissionResolver();
            var bridge = new EngineToolApprovalBridge(resolver, [AllowedKind]);
            using var cancellation = new CancellationTokenSource();
            var parked = ParkRequestAsync(resolver, cancellation.Token);
            var window = RunControlsHarness.Show(bridge);
            var approval = RunControlsHarness.AddRequest(window, RequestId);

            await cancellation.CancelAsync().ConfigureAwait(true);
            var answered = true;
            try
            {
                await parked.ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                answered = false;
            }

            answered.ShouldBeFalse();

            RunControlsHarness.Click(RunControlsHarness.Named(window, "Deny tool request"));
            await RunControlsHarness.UntilAsync(() => approval.Status != ToolApprovalStatus.Pending, "the late decision", TestContext.Current.CancellationToken).ConfigureAwait(true);

            approval.Status.ShouldBe(ToolApprovalStatus.AlreadyDecided);
            approval.Status.ShouldNotBe(ToolApprovalStatus.Allowed);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A deny for a request that was never parked reports AlreadyDecided, because nothing is pending to settle.</summary>
    [Fact]
    public async Task ADenyForAnUnknownRequestReportsAlreadyDecided()
    {
        var bridge = new EngineToolApprovalBridge(new PendingPermissionResolver(), [AllowedKind]);

        var outcome = await bridge.DecideAsync(SessionId, "never-parked", ToolApprovalDecision.Deny, TestContext.Current.CancellationToken).ConfigureAwait(true);

        outcome.ShouldBe(ToolApprovalOutcome.AlreadyDecided);
    }

    /// <summary>Starts the engine's wait for <see cref="RequestId"/>. The returned task completes with the decision that settles it.</summary>
    private static Task<PermissionDecision> ParkRequestAsync(PendingPermissionResolver resolver, CancellationToken cancellationToken) =>
        resolver.ResolveAsync(new PermissionRequestEvent("call-1", AllowedKind, "{}", RequestId), cancellationToken);
}
