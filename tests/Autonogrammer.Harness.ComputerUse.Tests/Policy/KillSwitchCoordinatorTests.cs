using Autonogrammer.Harness.ComputerUse.Abstractions;
using Autonogrammer.Harness.ComputerUse.Executor;
using Autonogrammer.Harness.ComputerUse.Fakes;
using Autonogrammer.Harness.ComputerUse.Policy;

using Shouldly;

using Xunit;

namespace Autonogrammer.Harness.ComputerUse.Tests.Policy;

/// <summary>Arming, trip, and the 24 px takeover threshold.</summary>
public sealed class KillSwitchCoordinatorTests
{
    [Fact]
    public async Task ArmingIsRefusedUntilTheRegistrarConfirms()
    {
        FakeComputerUseBackend backend = new();
        CountingAudit audit = new();
        using KillSwitchCoordinator coordinator = new(new ScriptRegistrar(false), backend, audit);
        (await coordinator.TryArmAsync("Ctrl+Alt+Shift+Escape", TestContext.Current.CancellationToken)).ShouldBeFalse();
        coordinator.State.ShouldBe(KillSwitchState.Unregistered);
        GateFixture fixture = await GateFixture.CreateAsync().ConfigureAwait(true);
        fixture.Gate.OptIn("session");
        (await fixture.DenyAsync().ConfigureAwait(true)).Code.ShouldBe(DenialCode.KillSwitchUnavailable);
    }

    [Fact]
    public async Task TripReleasesInputRevokesCancelsTurnAndWritesOneAuditRecord()
    {
        FakeComputerUseBackend backend = new();
        CountingAudit audit = new();
        using KillSwitchCoordinator coordinator = new(new ScriptRegistrar(true), backend, audit);
        (await coordinator.TryArmAsync("Ctrl+Alt+Shift+Escape", TestContext.Current.CancellationToken)).ShouldBeTrue();
        CancellationToken revocation = coordinator.RevocationToken;
        CancellationToken turn = coordinator.TurnToken;
        await coordinator.TripAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        revocation.IsCancellationRequested.ShouldBeTrue();
        turn.IsCancellationRequested.ShouldBeTrue();
        backend.ReleaseCount.ShouldBe(1);
        coordinator.State.ShouldBe(KillSwitchState.Tripped);
        audit.Trips.ShouldBe(1);

        GateFixture fixture = await GateFixture.ReadyAsync(ComputerGrantTier.Full).ConfigureAwait(true);
        await fixture.KillSwitch.TripAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        (await fixture.DenyAsync().ConfigureAwait(true)).Code.ShouldBe(DenialCode.Revoked);
    }

    [Fact]
    public async Task TakeoverThresholdReadsTheOptionAndDefaultsTo24()
    {
        new ComputerUseOptions().TakeoverPx.ShouldBe(24);
        GateFixture fixture = await GateFixture.ReadyAsync(ComputerGrantTier.Full).ConfigureAwait(true);
        await fixture.MarkSeenAsync().ConfigureAwait(true);
        using ComputerActionExecutor executor = new(fixture.Backend, fixture.Gate, fixture.KillSwitch, fixture.Time, fixture.Options.TakeoverPx);
        executor.TakeoverPx.ShouldBe(24);
        await executor.ExecuteAsync(
            fixture.Request,
            new ComputerHostAction { ActionClass = ComputerActionClass.PointerMove },
            [new PointerMove { X = 0, Y = 0 }],
            TestContext.Current.CancellationToken).ConfigureAwait(true);
        fixture.Backend.SetPointer(new PointerState { X = 25, Y = 0, Buttons = 0 });
        ComputerExecutionResult paused = await executor.ExecuteAsync(
            fixture.Request with { ActionHash = new byte[] { 8, 8, 8, 8 } },
            new ComputerHostAction { ActionClass = ComputerActionClass.PointerMove },
            [new PointerMove { X = 25, Y = 0 }],
            TestContext.Current.CancellationToken).ConfigureAwait(true);
        paused.Denial.ShouldBe(DenialCode.UserTookOver);
        fixture.Backend.InjectCount.ShouldBe(1);
    }

    private sealed class ScriptRegistrar(bool confirm) : IGlobalHotkeyRegistrar
    {
        public ValueTask<bool> RegisterAsync(string chord, CancellationToken cancellationToken) => ValueTask.FromResult(confirm);

        public ValueTask UnregisterAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
}
