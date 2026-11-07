using Autonogrammer.Presentation.Dispatch;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Dispatch;

/// <summary>The relay's actions: each reaches the host, the page reloads to show what the service now says, and a refused action is held as the problem.</summary>
public sealed class DispatchRelayActionsTests
{
    /// <summary>Cancelling a running command reaches the host, and the reloaded row shows the command as cancelled.</summary>
    [Fact]
    public async Task CancellingARunningCommandShowsItAsCancelled()
    {
        var host = new FakeDispatchHost(
            [new DispatchDevice("device-1", "Office Mac", Online: true, PresenceKnown: true)],
            [new DispatchCommand("command-1", "run", DispatchPhase.Running, "Fix the build", null)]);
        var relay = new DispatchRelayModel(host, null);
        await relay.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        await relay.CancelAsync("command-1", TestContext.Current.CancellationToken).ConfigureAwait(true);

        host.Cancelled.ShouldBe(["command-1"]);
        relay.Page.Commands.Single().Phase.ShouldBe(DispatchPhase.Cancelled);
        relay.Page.Problem.ShouldBeNull();
    }

    /// <summary>Only a running command's row offers Cancel once the relay has loaded the commands.</summary>
    [Fact]
    public async Task OnlyARunningRowOffersCancelAfterTheLoad()
    {
        var host = new FakeDispatchHost(
            [],
            [
                new DispatchCommand("command-1", "run", DispatchPhase.Running, "Fix the build", null),
                new DispatchCommand("command-2", "run", DispatchPhase.Completed, "Done", null),
            ]);
        var relay = new DispatchRelayModel(host, null);

        await relay.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        relay.Page.Commands.Select(row => row.CanCancel).ShouldBe([true, false]);
    }

    /// <summary>A refused cancel is held as the page's problem, so the view can announce it.</summary>
    [Fact]
    public async Task ARefusedCancelIsHeldAsTheProblem()
    {
        var relay = new DispatchRelayModel(new NotConfiguredDispatchHost(), null);

        await relay.CancelAsync("command-1", TestContext.Current.CancellationToken).ConfigureAwait(true);

        relay.Page.Problem.ShouldNotBeNullOrWhiteSpace();
    }

    /// <summary>A capability change reaches the host, and the reloaded page shows the new policy.</summary>
    [Fact]
    public async Task ACapabilityChangeIsStoredAndShownOnTheReload()
    {
        var host = new FakeDispatchHost([], null, [new DispatchCapabilityPolicy("code", false)]);
        var relay = new DispatchRelayModel(host, host);
        await relay.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        await relay.SetCapabilityAsync("code", true, TestContext.Current.CancellationToken).ConfigureAwait(true);

        host.CapabilityChanges.ShouldBe([("code", true)]);
        relay.Page.PairThisDevice.ShouldNotBeNull().Capabilities.ShouldContain(new DispatchCapabilityPolicy("code", true));
    }

    /// <summary>Pausing Dispatch reaches the host, and the reloaded page shows this desktop as paused.</summary>
    [Fact]
    public async Task PausingDispatchIsStoredAndShownAsPaused()
    {
        var host = new FakeDispatchHost([]);
        var relay = new DispatchRelayModel(host, host);
        await relay.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        await relay.SetPausedAsync(true, TestContext.Current.CancellationToken).ConfigureAwait(true);

        host.PauseChanges.ShouldBe([true]);
        relay.Page.PairThisDevice.ShouldNotBeNull().Paused.ShouldBeTrue();
    }

    /// <summary>A desktop whose state cannot be read is shown as unavailable, not as unpaired, and offers no switches.</summary>
    [Fact]
    public async Task ADesktopWhoseStateCannotBeReadIsShownAsUnavailable()
    {
        var relay = new DispatchRelayModel(new FakeDispatchHost([]), new NotConfiguredDispatchHost());

        await relay.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        var pair = relay.Page.PairThisDevice.ShouldNotBeNull();
        pair.Paired.ShouldBeFalse();
        string.Equals(pair.StatusText, DispatchCopy.ThisDeviceNotPaired, StringComparison.Ordinal).ShouldBeFalse();
        pair.Capabilities.ShouldBeEmpty();
    }

    /// <summary>A head with no local device sends no pause or capability change.</summary>
    [Fact]
    public async Task AHeadWithoutALocalDeviceSendsNoPauseOrCapabilityChange()
    {
        var host = new FakeDispatchHost([]);
        var relay = new DispatchRelayModel(host, null);
        await relay.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        await relay.SetPausedAsync(true, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await relay.SetCapabilityAsync("code", true, TestContext.Current.CancellationToken).ConfigureAwait(true);

        host.PauseChanges.ShouldBeEmpty();
        host.CapabilityChanges.ShouldBeEmpty();
        relay.Page.PairThisDevice.ShouldBeNull();
    }
}
