using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Rpc.Tests;

/// <summary>Command correlation is order-independent: responses complete by id, and unknown ids are ignored.</summary>
public sealed class RpcCommandCorrelationTests
{
    private const string ReadyLine =
        """{"type":"ready","protocolVersion":1,"supportedProtocolVersions":[1,2],"maxFrameBytes":1048576,"maxReassembledFrameBytes":67108864}""";

    /// <summary>
    /// Two concurrent commands answered second-first (with an unknown-id response interleaved) each complete
    /// with their own response.
    /// </summary>
    [Fact]
    [SuppressMessage(
        "Usage",
        "CA2000",
        Justification = "The OmpRpcSession takes ownership of the transport and disposes it in DisposeAsync.")]
    public async Task TwoConcurrentCommandsCompleteOutOfOrderWithOwnResponses()
    {
        var ct = TestContext.Current.CancellationToken;
        string? heldId = null;
        var transport = new FakeRpcTransport(line =>
        {
            string command = RpcTestData.TypeOf(line);
            string id = RpcTestData.IdOf(line);
            if (Handshake(command, id) is { } handshake)
            {
                return handshake;
            }

            if (!string.Equals(command, "echo-probe", StringComparison.Ordinal))
            {
                return [];
            }

            if (heldId is null)
            {
                heldId = id;
                return [];
            }

            return
            [
                RpcTestData.Ok("unknown-id", "echo-probe", "null"),
                RpcTestData.Ok(id, "echo-probe", $$"""{"echo":"{{id}}"}"""),
                RpcTestData.Ok(heldId, "echo-probe", $$"""{"echo":"{{heldId}}"}"""),
            ];
        });
        transport.Emit(ReadyLine);
        var session = new OmpRpcSession(transport, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
        await session.CreateAsync(ct).ConfigureAwait(true);

        string? idA = null;
        string? idB = null;
        Task<JsonElement?> first = session.ExecuteAsync(
            id => { idA = id; return new UnknownCommandProbe("echo-probe") { Id = id }; }, ct);
        Task<JsonElement?> second = session.ExecuteAsync(
            id => { idB = id; return new UnknownCommandProbe("echo-probe") { Id = id }; }, ct);

        JsonElement? responseA = await first.ConfigureAwait(true);
        JsonElement? responseB = await second.ConfigureAwait(true);

        responseA!.Value.GetProperty("echo").GetString().ShouldBe(idA);
        responseB!.Value.GetProperty("echo").GetString().ShouldBe(idB);
        await session.DisposeAsync().ConfigureAwait(true);
    }

    private static IEnumerable<string>? Handshake(string command, string id) => command switch
    {
        "negotiate_protocol" => [RpcTestData.Ok(id, command, """{"protocolVersion":2}""")],
        "get_state" => [RpcTestData.Ok(id, command, RpcTestData.StateData)],
        _ => null,
    };
}
