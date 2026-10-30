using Autonogrammer.Harness.ComputerUse.Abstractions;
using Autonogrammer.Harness.ComputerUse.Executor;
using Autonogrammer.Harness.ComputerUse.Policy;

using Shouldly;

using Xunit;

namespace Autonogrammer.Harness.ComputerUse.Tests.Policy;

/// <summary>Pointer actions re-resolve after the move. Keyboard uses the foreground window at decision time.</summary>
public sealed class OwnWindowTests
{
    [Fact]
    public async Task PointerAbortsWhenTheWindowChangesOrIsOwnProcess()
    {
        GateFixture fixture = await GateFixture.ReadyAsync(ComputerGrantTier.Full).ConfigureAwait(true);
        await fixture.MarkSeenAsync().ConfigureAwait(true);
        using ComputerActionExecutor executor = new(fixture.Backend, fixture.Gate, fixture.KillSwitch, fixture.Time);
        fixture.Backend.SetWindows(
        [
            new WindowInfo
            {
                Id = "other",
                Pid = 3,
                AppIdentity = "code",
                Title = "other",
                Bounds = new PixelRect { X = 0, Y = 0, Width = 50, Height = 50 },
                Minimized = false,
                Focused = true,
                IsOwnProcess = false,
            },
            GateFixture.Indicator(),
        ]);
        ComputerExecutionResult changed = await executor.ExecuteAsync(
            fixture.Request,
            new ComputerHostAction { ActionClass = ComputerActionClass.PointerMove, Actuating = true },
            [new PointerMove { X = 10, Y = 10 }, new PointerButton { Button = 1, Down = true }],
            TestContext.Current.CancellationToken).ConfigureAwait(true);
        changed.Denial.ShouldBe(DenialCode.OwnWindow);
        fixture.Backend.LastInjected.ShouldBeOfType<PointerMove>();

        fixture.Backend.SetWindows(
        [
            new WindowInfo
            {
                Id = "target",
                Pid = 4,
                AppIdentity = "code",
                Title = "own",
                Bounds = new PixelRect { X = 0, Y = 0, Width = 50, Height = 50 },
                Minimized = false,
                Focused = true,
                IsOwnProcess = true,
            },
            GateFixture.Indicator(),
        ]);
        ComputerExecutionResult own = await executor.ExecuteAsync(
            fixture.Request with { ActionHash = new byte[] { 5, 5, 5, 5 } },
            new ComputerHostAction { ActionClass = ComputerActionClass.PointerMove },
            [new PointerMove { X = 10, Y = 10 }],
            TestContext.Current.CancellationToken).ConfigureAwait(true);
        own.Denial.ShouldBe(DenialCode.OwnWindow);
    }

    [Fact]
    public async Task KeyboardUsesTheForegroundWindowAtDecisionTime()
    {
        GateFixture fixture = await GateFixture.ReadyAsync(ComputerGrantTier.Full).ConfigureAwait(true);
        fixture.Backend.SetWindows(
        [
            new WindowInfo
            {
                Id = "target",
                Pid = 4,
                AppIdentity = "code",
                Title = "own",
                Bounds = new PixelRect { X = 0, Y = 0, Width = 50, Height = 50 },
                Minimized = false,
                Focused = true,
                IsOwnProcess = true,
            },
            GateFixture.Indicator(),
        ]);
        using ComputerActionExecutor executor = new(fixture.Backend, fixture.Gate, fixture.KillSwitch, fixture.Time);
        ComputerExecutionResult result = await executor.ExecuteAsync(
            fixture.Request,
            new ComputerHostAction { ActionClass = ComputerActionClass.Type },
            [new TypeText { Text = "hello" }],
            TestContext.Current.CancellationToken).ConfigureAwait(true);
        result.Denial.ShouldBe(DenialCode.OwnWindow);
        fixture.Backend.InjectCount.ShouldBe(0);
    }
}
