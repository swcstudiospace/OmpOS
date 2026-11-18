using Autonogrammer.Harness.AgentLoop.Hooks;
using Autonogrammer.Harness.AgentLoop.Tests.Stages;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.AgentLoop.Tests.Hooks;

/// <summary>
/// The handler and the approval prompt of one request share one deadline: a prompt gets only the time the handler left, so the
/// answer comes within the handler timeout that the extension waits for. Time runs on a fake clock.
/// </summary>
public sealed class HookBridgeSharedDeadlineTests
{
    private const string Request = "{\"event\":\"beforeShellExecution\",\"payload\":{\"command\":\"rm -rf build\"}}";
    private static readonly TimeSpan HandlerTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan HandlerWork = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan Safety = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task APromptGetsOnlyTheTimeTheHandlerLeft()
    {
        var clock = new StageFakeTimeProvider();
        var testToken = TestContext.Current.CancellationToken;
        var worked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var asked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var never = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new FakeBridgeHandler(async (_, cancellationToken) =>
        {
            var pause = Task.Delay(HandlerWork, clock, cancellationToken);
            worked.TrySetResult();
            await pause.ConfigureAwait(true);
            return new BridgeDecision { Permission = BridgeDecision.PermissionAsk };
        });
        var prompt = FakeApprovalPrompt.Running(async (_, _, cancellationToken) =>
        {
            asked.TrySetResult();
            try
            {
                await never.Task.WaitAsync(cancellationToken).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                cancelled.TrySetResult();
                throw;
            }

            return true;
        });
        var options = new HookBridgeOptions { HandlerTimeout = HandlerTimeout };
        var rig = await HookBridgeRig.StartAsync(handler, options, prompt, clock).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);
        var pending = rig.PostAsync(Request);

        await worked.Task.WaitAsync(Safety, testToken).ConfigureAwait(true);
        clock.Advance(HandlerWork);
        await asked.Task.WaitAsync(Safety, testToken).ConfigureAwait(true);
        clock.Advance(HandlerTimeout - HandlerWork);

        await cancelled.Task.WaitAsync(Safety, testToken).ConfigureAwait(true);
        var response = await pending.ConfigureAwait(true);
        response.Status.ShouldBe(200);
        response.Json().GetProperty("permission").GetString().ShouldBe("deny");
        prompt.Calls.ShouldBe(1);
    }
}
