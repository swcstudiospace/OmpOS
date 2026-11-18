using Autonogrammer.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Acp.Tests;

/// <summary>
/// A prompt whose consumer has gone is not read any more: the agent's later session updates for it are dropped, so they cannot fill its buffer
/// and fault the session. The session stays usable once the agent answers the prompt.
/// </summary>
public sealed class AcpAbandonedTurnTests
{
    /// <summary>More updates than the turn buffer holds, sent after the consumer left, do not fault the session.</summary>
    [Fact]
    public async Task UpdatesOfAnAbandonedPromptAreDroppedAndDoNotFaultTheSession()
    {
        var ct = TestContext.Current.CancellationToken;
        var peer = new ScriptedOmpPeer { ConfigOptions = AcpRegressionFrames.ModelOptions("m/one") };
        var transport = new ScriptedAcpTransport(peer.Respond);
        await using var transportScope = transport.ConfigureAwait(true);
        var session = new AcpAgentSession(transport, new AcpClientOptions { WorkingDirectory = AcpFixtures.CapturedCwd, AuthMethodId = "agent" });
        await using var sessionScope = session.ConfigureAwait(true);
        await session.CreateAsync(ct).ConfigureAwait(true);
        var stream = session.SubmitPromptAsync("go", new PromptOptions(), ct).GetAsyncEnumerator(ct);
        var first = stream.MoveNextAsync();
        transport.Deliver(AcpRegressionFrames.Update("first"));
        (await first.ConfigureAwait(true)).ShouldBeTrue();
        await stream.DisposeAsync().ConfigureAwait(true);

        for (var i = 0; i < AcpRegressionFrames.TurnBufferOverflow; i++)
        {
            transport.Deliver(AcpRegressionFrames.Update("chunk"));
        }

        transport.Deliver(peer.EndPrompt("cancelled"));
        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        session.ModelChanged += (_, _) => changed.TrySetResult();
        transport.Deliver(AcpRegressionFrames.ConfigUpdate("m/two"));
        await changed.Task.WaitAsync(ct).ConfigureAwait(true);

        session.CurrentModelId.ShouldBe("m/two");
        session.State.ShouldBe(HarnessState.Ready);
    }
}
