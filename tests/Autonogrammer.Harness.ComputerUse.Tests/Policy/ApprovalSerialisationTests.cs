using Autonogrammer.Harness.ComputerUse.Abstractions;
using Autonogrammer.Harness.ComputerUse.Executor;
using Autonogrammer.Harness.ComputerUse.Policy;

using Shouldly;

using Xunit;

namespace Autonogrammer.Harness.ComputerUse.Tests.Policy;

/// <summary>No injection lands while an approval prompt is open.</summary>
public sealed class ApprovalSerialisationTests
{
    [Fact]
    public async Task NoInjectionWhilePromptIsPending()
    {
        GateFixture fixture = await GateFixture.ReadyAsync(ComputerGrantTier.Full).ConfigureAwait(true);
        HoldingPrompt prompt = new();
        ComputerActionGate gate = new(
            fixture.Options,
            new SignedInCowork(),
            fixture.Backend,
            fixture.Grants,
            await AppCatalogue.LoadAsync(new MemorySettings(), TestContext.Current.CancellationToken).ConfigureAwait(true),
            fixture.Limiter,
            fixture.KillSwitch,
            prompt,
            fixture.Audit,
            fixture.Time);
        gate.OptIn("session");
        gate.NoteFrame("session", "frame-1");
        using ComputerActionExecutor executor = new(fixture.Backend, gate, fixture.KillSwitch, fixture.Time, fixture.Options.TakeoverPx);
        Task<ComputerExecutionResult> first = executor.ExecuteAsync(
            fixture.Request,
            new ComputerHostAction { ActionClass = ComputerActionClass.Click },
            [new PointerButton { Button = 1, Down = true }],
            TestContext.Current.CancellationToken).AsTask();
        for (int i = 0; i < 100 && !prompt.Entered.Task.IsCompleted; i++)
        {
            await Task.Yield();
        }

        prompt.Entered.Task.IsCompleted.ShouldBeTrue();
        Task<ComputerExecutionResult> second = Task.Run(() => executor.ExecuteAsync(
            fixture.Request with { ActionHash = new byte[] { 9, 9, 9, 9 } },
            new ComputerHostAction { ActionClass = ComputerActionClass.Click },
            [new PointerButton { Button = 1, Down = true }],
            TestContext.Current.CancellationToken).AsTask());
        for (int i = 0; i < 100 && executor.Queued < 2; i++)
        {
            await Task.Yield();
        }
        fixture.Backend.InjectCount.ShouldBe(0);
        second.IsCompleted.ShouldBeFalse();
        prompt.Release = true;
        ComputerExecutionResult completed = await first.ConfigureAwait(true);
        completed.Injected.ShouldBeTrue();
        await second.ConfigureAwait(true);
    }

    private sealed class SignedInCowork : IComputerUseAccessFacts
    {
        public bool IsSignedIn => true;

        public bool IsCoworkSession(string sessionId) => true;
    }

    private sealed class HoldingPrompt : IComputerApprovalPrompt
    {
        public bool Release { get; set; }

        public ComputerApprovalChoice Choice { get; set; } = ComputerApprovalChoice.AllowOnce;

        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask<ComputerApprovalChoice> RequestAsync(ComputerApprovalFacts facts, CancellationToken cancellationToken)
        {
            Entered.TrySetResult();
            while (!Release)
            {
                await Task.Yield();
            }

            return Choice;
        }
    }
}
