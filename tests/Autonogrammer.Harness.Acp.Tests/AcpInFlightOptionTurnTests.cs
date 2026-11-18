using Autonogrammer.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Acp.Tests;

/// <summary>
/// A prompt that arrives while a model switch is in flight waits for the switch to finish, then starts. It is never refused, and nothing is sent
/// for it before the switch is answered.
/// </summary>
public sealed class AcpInFlightOptionTurnTests
{
    /// <summary>Submitting a prompt while <c>session/set_config_option</c> is unanswered starts the prompt only after the switch is answered.</summary>
    [Fact]
    public async Task APromptStartsOnceAnInFlightModelSwitchFinishes()
    {
        var ct = TestContext.Current.CancellationToken;
        var setSent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var promptSent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var peer = new ScriptedOmpPeer
        {
            ConfigOptions = AcpRegressionFrames.ModelOptions("m/one"),
            OnSetConfigOption = _ =>
            {
                setSent.TrySetResult();
                return [];
            },
        };
        var transport = new ScriptedAcpTransport(frame =>
        {
            if (string.Equals(AcpFixtures.MethodOf(frame), "session/prompt", StringComparison.Ordinal))
            {
                promptSent.TrySetResult();
            }

            return peer.Respond(frame);
        });
        await using var transportScope = transport.ConfigureAwait(true);
        var session = new AcpAgentSession(transport, new AcpClientOptions { WorkingDirectory = AcpFixtures.CapturedCwd, AuthMethodId = "agent" });
        await using var sessionScope = session.ConfigureAwait(true);
        await session.CreateAsync(ct).ConfigureAwait(true);
        var switching = session.SetModelAsync("m/two", ct);
        await setSent.Task.WaitAsync(ct).ConfigureAwait(true);

        var stream = session.SubmitPromptAsync("go", new PromptOptions(), ct).GetAsyncEnumerator(ct);
        await using var streamScope = stream.ConfigureAwait(true);
        var firstEvent = stream.MoveNextAsync();

        // The prompt waits for the switch: no session/prompt is written while the switch is unanswered.
        transport.Sent.Where(static frame => string.Equals(AcpFixtures.MethodOf(frame), "session/prompt", StringComparison.Ordinal)).ShouldBeEmpty();

        var setId = AcpFixtures.IdOf(transport.Sent.Single(static frame => string.Equals(AcpFixtures.MethodOf(frame), "session/set_config_option", StringComparison.Ordinal)));
        transport.Deliver(ScriptedOmpPeer.Result(setId, "{\"configOptions\":" + AcpRegressionFrames.ModelOptions("m/two") + "}"));
        (await switching.ConfigureAwait(true)).IsSwitched.ShouldBeTrue();

        await promptSent.Task.WaitAsync(ct).ConfigureAwait(true);
        transport.Deliver(peer.EndPrompt("end_turn"));
        (await firstEvent.ConfigureAwait(true)).ShouldBeTrue();
        stream.Current.ShouldBeOfType<CompletedEvent>().Reason.ShouldBe("end_turn");
    }
}
