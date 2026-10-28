using Autonogrammer.Harness.AgentLoop.Hooks;
using Autonogrammer.Harness.AgentLoop.Tests.Stages;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.AgentLoop.Tests.Hooks;

/// <summary>
/// Verifies the failure rule: a handler that throws, returns nothing or runs past the handler timeout makes the answer a deny
/// for a fail-closed event and a let-through marked <c>failed_open</c> for any other, and neither the exception text nor the
/// request leaks into the answer or the log. Timeouts run on a fake clock, so no test waits for time to pass.
/// </summary>
public sealed class HookBridgeFailureTests
{
    private static readonly TimeSpan HandlerTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan Safety = TimeSpan.FromSeconds(30);

    private static HookBridgeOptions FailClosed(params string[] events) =>
        new() { FailClosedEvents = new HashSet<string>(events, StringComparer.Ordinal), HandlerTimeout = HandlerTimeout };

    private static string Event(string name) => "{\"event\":\"" + name + "\",\"payload\":{\"command\":\"TOPSECRET-COMMAND\"}}";

    [Fact]
    public async Task AThrowingHandlerDeniesAFailClosedEventAndNothingOfTheFailureIsRevealed()
    {
        var logger = new CapturingLogger();
        var handler = FakeBridgeHandler.Throwing(new InvalidOperationException("TOPSECRET-EXCEPTION-TEXT"));
        var rig = await HookBridgeRig.StartAsync(handler, FailClosed("preToolUse"), logger: logger).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);

        var response = await rig.PostAsync(Event("preToolUse")).ConfigureAwait(true);

        response.Status.ShouldBe(200);
        var json = response.Json();
        json.GetProperty("permission").GetString().ShouldBe("deny");
        json.GetProperty("agent_message").GetString().ShouldNotBeNullOrWhiteSpace();
        json.GetProperty("failed_open").GetBoolean().ShouldBeFalse();
        response.Body.ShouldNotContain("TOPSECRET", Case.Sensitive);
        await rig.DisposeAsync().ConfigureAwait(true);
        logger.Messages.ShouldNotBeEmpty();
        logger.Messages.ShouldAllBe(static message => !message.Contains("TOPSECRET", StringComparison.Ordinal));
        logger.Messages.ShouldContain(static message => message.StartsWith("Warning", StringComparison.Ordinal) && message.Contains("InvalidOperationException", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AThrowingHandlerLetsAnEventThatIsNotFailClosedThroughAndMarksItFailedOpen()
    {
        var handler = FakeBridgeHandler.Throwing(new InvalidOperationException("boom"));
        var rig = await HookBridgeRig.StartAsync(handler, FailClosed("preToolUse")).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);

        var response = await rig.PostAsync(Event("postToolUse")).ConfigureAwait(true);

        response.Status.ShouldBe(200);
        response.PropertyNames().ShouldNotContain("permission", StringComparer.Ordinal);
        response.Json().GetProperty("failed_open").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task AnAsynchronousFailureIsHandledLikeASynchronousOne()
    {
        var handler = new FakeBridgeHandler(async (_, _) =>
        {
            await Task.Yield();
            throw new InvalidOperationException("late");
        });
        var rig = await HookBridgeRig.StartAsync(handler, FailClosed("beforeShellExecution")).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);

        var closed = await rig.PostAsync(Event("beforeShellExecution")).ConfigureAwait(true);
        var open = await rig.PostAsync(Event("afterShellExecution")).ConfigureAwait(true);

        closed.Json().GetProperty("permission").GetString().ShouldBe("deny");
        open.Json().GetProperty("failed_open").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task AHandlerThatReturnsNothingIsAFailure()
    {
        var handler = new FakeBridgeHandler((_, _) => ValueTask.FromResult<BridgeDecision>(null!));
        var rig = await HookBridgeRig.StartAsync(handler, FailClosed("preToolUse")).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);

        var closed = await rig.PostAsync(Event("preToolUse")).ConfigureAwait(true);
        var open = await rig.PostAsync(Event("stop")).ConfigureAwait(true);

        closed.Json().GetProperty("permission").GetString().ShouldBe("deny");
        open.Json().GetProperty("failed_open").GetBoolean().ShouldBeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ACooperativeHandlerThatRunsPastTheTimeoutIsCancelledAndAnsweredByTheFailureRule(bool failClosed)
    {
        var clock = new StageFakeTimeProvider();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var never = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new FakeBridgeHandler(async (_, cancellationToken) =>
        {
            entered.TrySetResult();
            try
            {
                await never.Task.WaitAsync(cancellationToken).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                cancelled.TrySetResult();
                throw;
            }

            return new BridgeDecision();
        });
        var options = failClosed ? FailClosed("preToolUse") : FailClosed();
        var rig = await HookBridgeRig.StartAsync(handler, options, clock: clock).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);
        var pending = rig.PostAsync(Event("preToolUse"));

        await entered.Task.WaitAsync(Safety, TestContext.Current.CancellationToken).ConfigureAwait(true);
        clock.Advance(HandlerTimeout);
        var response = await pending.ConfigureAwait(true);

        await cancelled.Task.WaitAsync(Safety, TestContext.Current.CancellationToken).ConfigureAwait(true);
        response.Status.ShouldBe(200);
        var json = response.Json();
        if (failClosed)
        {
            json.GetProperty("permission").GetString().ShouldBe("deny");
            json.GetProperty("agent_message").GetString().ShouldNotBeNullOrWhiteSpace();
            json.GetProperty("failed_open").GetBoolean().ShouldBeFalse();
        }
        else
        {
            response.PropertyNames().ShouldNotContain("permission", StringComparer.Ordinal);
            json.GetProperty("failed_open").GetBoolean().ShouldBeTrue();
        }
    }

    [Fact]
    public async Task AHandlerThatIgnoresItsTokenIsStillAbandonedAtTheTimeout()
    {
        var clock = new StageFakeTimeProvider();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var testToken = TestContext.Current.CancellationToken;
        var handler = new FakeBridgeHandler(async (_, _) =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(Safety, testToken).ConfigureAwait(true);
            return new BridgeDecision { Permission = BridgeDecision.PermissionAllow };
        });
        var rig = await HookBridgeRig.StartAsync(handler, FailClosed("preToolUse"), clock: clock).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);
        var pending = rig.PostAsync(Event("preToolUse"));

        await entered.Task.WaitAsync(Safety, TestContext.Current.CancellationToken).ConfigureAwait(true);
        clock.Advance(HandlerTimeout);
        var response = await pending.ConfigureAwait(true);
        release.SetResult();

        response.Json().GetProperty("permission").GetString().ShouldBe("deny");
        handler.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task ATimeoutThatHasNotElapsedYetDoesNotCutTheHandlerShort()
    {
        var clock = new StageFakeTimeProvider();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new FakeBridgeHandler(async (_, cancellationToken) =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(cancellationToken).ConfigureAwait(true);
            return new BridgeDecision { Permission = BridgeDecision.PermissionAllow, HooksRun = 1 };
        });
        var rig = await HookBridgeRig.StartAsync(handler, FailClosed("preToolUse"), clock: clock).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);
        var pending = rig.PostAsync(Event("preToolUse"));

        await entered.Task.WaitAsync(Safety, TestContext.Current.CancellationToken).ConfigureAwait(true);
        clock.Advance(HandlerTimeout - TimeSpan.FromMilliseconds(1));
        release.SetResult();
        var response = await pending.ConfigureAwait(true);

        response.Json().GetProperty("permission").GetString().ShouldBe("allow");
        response.Json().GetProperty("hooks_run").GetInt32().ShouldBe(1);
    }
}
