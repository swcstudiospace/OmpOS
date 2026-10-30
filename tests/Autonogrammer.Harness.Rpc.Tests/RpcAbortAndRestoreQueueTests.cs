using System.Diagnostics.CodeAnalysis;
using Autonogrammer.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Rpc.Tests;

/// <summary>Aborting with queue restore: the typed command on the wire and the typed queue it returns.</summary>
public sealed class RpcAbortAndRestoreQueueTests
{
    private const string ReadyLine =
        """{"type":"ready","protocolVersion":1,"supportedProtocolVersions":[1,2],"maxFrameBytes":1048576,"maxReassembledFrameBytes":67108864}""";

    /// <summary>
    /// <c>AbortAndRestoreQueueAsync</c> writes exactly one <c>abort_and_restore_queue</c> frame (never a plain
    /// <c>abort</c>) and returns the steering and follow-up inputs from the scripted response.
    /// </summary>
    [Fact]
    [SuppressMessage(
        "Usage",
        "CA2000",
        Justification = "The OmpRpcSession takes ownership of the transport and disposes it in DisposeAsync.")]
    public async Task AbortAndRestoreQueueSendsTypedCommandAndReturnsRestoredInputs()
    {
        var ct = TestContext.Current.CancellationToken;
        FakeRpcTransport transport = Engine(
            (command, id) => command switch
            {
                "abort_and_restore_queue" =>
                [
                    RpcTestData.Ok(
                        id,
                        command,
                        """{"steering":[{"text":"keep going"}],"followUp":[{"text":"with pics","images":[{"type":"image","data":"aGk=","mimeType":"image/png"}]}]}"""),
                ],
                _ => null,
            });
        var session = new OmpRpcSession(transport, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
        await session.CreateAsync(ct).ConfigureAwait(true);

        RestoredQueue restored = await session.AbortAndRestoreQueueAsync(ct).ConfigureAwait(true);

        transport.Sent.Select(RpcTestData.TypeOf).ShouldBe(["negotiate_protocol", "get_state", "abort_and_restore_queue"]);

        // The command carries the id the scripted response echoed: without id correlation the call
        // would hang until the command timeout instead of returning the restored queue.
        RpcTestData.IdOf(transport.Sent[2]).ShouldNotBeNullOrWhiteSpace();

        restored.Steering.Count.ShouldBe(1);
        restored.Steering[0].Text.ShouldBe("keep going");
        restored.Steering[0].Images.ShouldBeNull();
        restored.FollowUp.Count.ShouldBe(1);
        restored.FollowUp[0].Text.ShouldBe("with pics");
        restored.FollowUp[0].Images.ShouldNotBeNull();
        restored.FollowUp[0].Images!.Count.ShouldBe(1);
        restored.FollowUp[0].Images![0].Data.ShouldBe("aGk=");
        restored.FollowUp[0].Images![0].MimeType.ShouldBe("image/png");

        session.State.ShouldBe(HarnessState.Ready);
        await session.DisposeAsync().ConfigureAwait(true);
    }

    /// <summary>An empty queue object restores empty steering and follow-up lists rather than failing.</summary>
    [Fact]
    [SuppressMessage(
        "Usage",
        "CA2000",
        Justification = "The OmpRpcSession takes ownership of the transport and disposes it in DisposeAsync.")]
    public async Task AbortAndRestoreQueueWithEmptyDataReturnsEmptyQueues()
    {
        var ct = TestContext.Current.CancellationToken;
        FakeRpcTransport transport = Engine(
            (command, id) => command switch
            {
                "abort_and_restore_queue" => [RpcTestData.Ok(id, command, """{"steering":[],"followUp":[]}""")],
                _ => null,
            });
        var session = new OmpRpcSession(transport, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
        await session.CreateAsync(ct).ConfigureAwait(true);

        RestoredQueue restored = await session.AbortAndRestoreQueueAsync(ct).ConfigureAwait(true);

        restored.Steering.ShouldBeEmpty();
        restored.FollowUp.ShouldBeEmpty();
        await session.DisposeAsync().ConfigureAwait(true);
    }

    /// <summary>
    /// Starts a session against a scripted engine. The engine answers the handshake itself and every other command
    /// with <paramref name="scripted"/>.
    /// </summary>
    private static FakeRpcTransport Engine(Func<string, string, IEnumerable<string>?> scripted)
    {
        var transport = new FakeRpcTransport(line =>
        {
            string command = RpcTestData.TypeOf(line);
            string id = RpcTestData.IdOf(line);
            return Handshake(command, id) ?? scripted(command, id) ?? Array.Empty<string>();
        });
        transport.Emit(ReadyLine);
        return transport;
    }

    private static IEnumerable<string>? Handshake(string command, string id) => command switch
    {
        "negotiate_protocol" => [RpcTestData.Ok(id, command, """{"protocolVersion":2}""")],
        "get_state" => [RpcTestData.Ok(id, command, RpcTestData.StateData)],
        _ => null,
    };
}
