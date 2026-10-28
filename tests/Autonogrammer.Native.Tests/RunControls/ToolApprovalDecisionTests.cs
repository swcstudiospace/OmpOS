using Autonogrammer.Native.Engine;
using Autonogrammer.Presentation.Approvals;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.RunControls;

/// <summary>
/// Tool-approval decisions made from the run controls (ADE-02). Nothing is allowed by default: a request is decided only when the
/// user clicks, and each outcome the bridge reports is shown on the request.
/// </summary>
public sealed class ToolApprovalDecisionTests
{
    /// <summary>A request is never sent to the bridge until the user clicks a decision control.</summary>
    [Fact]
    public async Task NothingIsSentUntilTheUserDecides()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var bridge = new ScriptedApprovalBridge();
            var window = RunControlsHarness.Show(bridge);

            var approval = RunControlsHarness.AddRequest(window, "request-1");

            bridge.Calls.ShouldBeEmpty();
            approval.Status.ShouldBe(ToolApprovalStatus.Pending);
            RunControlsHarness.Named(window, "Approve tool request").IsEnabled.ShouldBeTrue();
            RunControlsHarness.Named(window, "Deny tool request").IsEnabled.ShouldBeTrue();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>When policy refuses the allow, the request shows Refused and is never Allowed.</summary>
    [Fact]
    public async Task ApproveWhenThePolicyRefusesShowsRefusedAndNeverAllowed()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var bridge = new ScriptedApprovalBridge(ToolApprovalOutcome.Refused);
            var window = RunControlsHarness.Show(bridge);
            var approval = RunControlsHarness.AddRequest(window, "request-1");

            RunControlsHarness.Click(RunControlsHarness.Named(window, "Approve tool request"));
            await RunControlsHarness.UntilAsync(() => approval.Status != ToolApprovalStatus.Pending, "the refusal", TestContext.Current.CancellationToken).ConfigureAwait(true);

            approval.Status.ShouldBe(ToolApprovalStatus.Refused);
            approval.Status.ShouldNotBe(ToolApprovalStatus.Allowed);
            RunControlsHarness.RowText(RunControlsHarness.Row(window), "StatusText").ShouldContain("Policy does not grant this tool.");
            bridge.Calls.ShouldHaveSingleItem().Decision.ShouldBe(ToolApprovalDecision.Allow);

            // A refused request stays pending on the server, so the user may still deny it.
            await RunControlsHarness.UntilAsync(() => RunControlsHarness.Named(window, "Deny tool request").IsEnabled, "deny to be enabled again", TestContext.Current.CancellationToken).ConfigureAwait(true);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>When the server accepts an allow, the request shows Allowed.</summary>
    [Fact]
    public async Task ApproveWhenTheBridgeAcceptsShowsAllowed()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var bridge = new ScriptedApprovalBridge(ToolApprovalOutcome.Accepted);
            var window = RunControlsHarness.Show(bridge);
            var approval = RunControlsHarness.AddRequest(window, "request-1");

            RunControlsHarness.Click(RunControlsHarness.Named(window, "Approve tool request"));
            await RunControlsHarness.UntilAsync(() => approval.Status != ToolApprovalStatus.Pending, "the allow", TestContext.Current.CancellationToken).ConfigureAwait(true);

            approval.Status.ShouldBe(ToolApprovalStatus.Allowed);
            bridge.Calls.ShouldHaveSingleItem().ShouldBe(new ApprovalCall("session-1", "request-1", ToolApprovalDecision.Allow));
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A deny the server accepts resolves the request: it shows Denied and offers no further decision.</summary>
    [Fact]
    public async Task DenyWhenTheBridgeAcceptsResolvesTheRequest()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var bridge = new ScriptedApprovalBridge(ToolApprovalOutcome.Accepted);
            var window = RunControlsHarness.Show(bridge);
            var approval = RunControlsHarness.AddRequest(window, "request-1");

            RunControlsHarness.Click(RunControlsHarness.Named(window, "Deny tool request"));
            await RunControlsHarness.UntilAsync(() => approval.Status != ToolApprovalStatus.Pending, "the deny", TestContext.Current.CancellationToken).ConfigureAwait(true);
            await RunControlsHarness.UntilAsync(() => !RunControlsHarness.Named(window, "Approve tool request").IsEnabled, "the request to settle", TestContext.Current.CancellationToken).ConfigureAwait(true);

            approval.Status.ShouldBe(ToolApprovalStatus.Denied);
            bridge.Calls.ShouldHaveSingleItem().Decision.ShouldBe(ToolApprovalDecision.Deny);
            RunControlsHarness.Named(window, "Approve tool request").IsEnabled.ShouldBeFalse();
            RunControlsHarness.Named(window, "Deny tool request").IsEnabled.ShouldBeFalse();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A repeated decision on a request the server already decided shows AlreadyDecided, and is never allowed.</summary>
    [Fact]
    public async Task ARepeatedDecisionShowsAlreadyDecided()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var bridge = new ScriptedApprovalBridge(ToolApprovalOutcome.Failed, ToolApprovalOutcome.AlreadyDecided);
            var window = RunControlsHarness.Show(bridge);
            var approval = RunControlsHarness.AddRequest(window, "request-1");

            RunControlsHarness.Click(RunControlsHarness.Named(window, "Approve tool request"));
            await RunControlsHarness.UntilAsync(() => approval.Status == ToolApprovalStatus.Failed, "the first decision to fail", TestContext.Current.CancellationToken).ConfigureAwait(true);

            // A failed decision was not recorded, so the request still waits and the user may decide it again.
            await RunControlsHarness.UntilAsync(() => RunControlsHarness.Named(window, "Approve tool request").IsEnabled, "the repeat decision to be enabled", TestContext.Current.CancellationToken).ConfigureAwait(true);
            RunControlsHarness.Click(RunControlsHarness.Named(window, "Approve tool request"));
            await RunControlsHarness.UntilAsync(() => approval.Status == ToolApprovalStatus.AlreadyDecided, "the repeated decision", TestContext.Current.CancellationToken).ConfigureAwait(true);

            approval.Status.ShouldNotBe(ToolApprovalStatus.Allowed);
            bridge.Calls.Count.ShouldBe(2);
            RunControlsHarness.RowText(RunControlsHarness.Row(window), "StatusText").ShouldContain("no longer pending");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The default bridge fails visibly with its reason and allows nothing.</summary>
    [Fact]
    public async Task TheDefaultBridgeFailsVisiblyWithItsReasonAndAllowsNothing()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var window = RunControlsHarness.ShowDefaults();
            var approval = RunControlsHarness.AddRequest(window, "request-1");

            RunControlsHarness.Click(RunControlsHarness.Named(window, "Approve tool request"));
            await RunControlsHarness.UntilAsync(() => approval.Status != ToolApprovalStatus.Pending, "the default decision", TestContext.Current.CancellationToken).ConfigureAwait(true);

            approval.Status.ShouldBe(ToolApprovalStatus.Failed);
            RunControlsHarness.RowText(RunControlsHarness.Row(window), "StatusText").ShouldContain("No approval service is connected in this build.");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The default bridge reports Failed for every decision and states why.</summary>
    [Fact]
    public async Task TheUnconnectedBridgeReportsFailedAndItsReason()
    {
        var bridge = new UnconnectedToolApprovalBridge();

        var outcome = await bridge.DecideAsync("session-1", "request-1", ToolApprovalDecision.Allow, TestContext.Current.CancellationToken).ConfigureAwait(true);

        outcome.ShouldBe(ToolApprovalOutcome.Failed);
        bridge.UnavailableMessage.ShouldBe("No approval service is connected in this build.");
    }

    /// <summary>Each pending request shows its own tool and arguments, and the list shows only while a request waits.</summary>
    [Fact]
    public async Task EachPendingRequestShowsItsToolAndArgumentsAndTheListShowsOnlyWhileOneWaits()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var window = RunControlsHarness.Show(new ScriptedApprovalBridge());
            RunControlsHarness.RunControlsList(window).IsVisible.ShouldBeFalse();

            RunControlsHarness.AddRequest(window, "request-1", "bash", "command: ls");
            RunControlsHarness.AddRequest(window, "request-2", "write_file", "path: notes.txt");

            RunControlsHarness.RunControlsList(window).IsVisible.ShouldBeTrue();
            RunControlsHarness.RowText(RunControlsHarness.Row(window, 0), "ToolNameText").ShouldContain("bash");
            RunControlsHarness.RowText(RunControlsHarness.Row(window, 0), "ArgumentsText").ShouldContain("command: ls");
            RunControlsHarness.RowText(RunControlsHarness.Row(window, 1), "ToolNameText").ShouldContain("write_file");
            RunControlsHarness.RowText(RunControlsHarness.Row(window, 1), "ArgumentsText").ShouldContain("path: notes.txt");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}
