using System.Diagnostics.CodeAnalysis;
using Autonogrammer.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Rpc.Tests;

/// <summary>An unexpected transport failure ends the session with a typed error instead of hanging its consumers.</summary>
public sealed class RpcPumpFailureTests
{
    private const string ReadyLine =
        """{"type":"ready","protocolVersion":1,"supportedProtocolVersions":[1,2],"maxFrameBytes":1048576,"maxReassembledFrameBytes":67108864}""";

    /// <summary>
    /// The transport throws <c>InvalidOperationException</c> after the prompt admission: the active prompt stream
    /// ends with <c>DisconnectedHarnessException</c> carrying the failure, the session faults, and dispose stays quiet.
    /// </summary>
    [Fact]
    [SuppressMessage(
        "Usage",
        "CA2000",
        Justification = "The OmpRpcSession takes ownership of the transport and disposes it in DisposeAsync.")]
    public async Task TransportFailureMidStreamEndsPromptWithTypedDisconnectedError()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken, timeout.Token);
        CancellationToken ct = linked.Token;

        FakeRpcTransport inner = Engine(
            (command, id) => command switch
            {
                "prompt" => [RpcTestData.Ok(id, "prompt", """{"agentInvoked":true}""")],
                _ => null,
            });
        var transport = new FailMidStreamTransport(inner, linesBeforeFailure: 4);
        var session = new OmpRpcSession(transport, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
        await session.CreateAsync(ct).ConfigureAwait(true);

        DisconnectedHarnessException failure = await Should.ThrowAsync<DisconnectedHarnessException>(
            () => CollectAsync(session.SubmitPromptAsync("hi", new PromptOptions(), ct))).ConfigureAwait(true);

        failure.InnerException.ShouldBeOfType<InvalidOperationException>();
        session.State.ShouldBe(HarnessState.Faulted);
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

    private static async Task<List<HarnessEvent>> CollectAsync(IAsyncEnumerable<HarnessEvent> events)
    {
        var collected = new List<HarnessEvent>();
        IAsyncEnumerator<HarnessEvent> enumerator = events.GetAsyncEnumerator();
        try
        {
            while (await enumerator.MoveNextAsync().ConfigureAwait(true))
            {
                collected.Add(enumerator.Current);
            }
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(true);
        }

        return collected;
    }

    /// <summary>An <see cref="IRpcTransport"/> that throws mid-stream after delivering a fixed number of lines.</summary>
    private sealed class FailMidStreamTransport(FakeRpcTransport inner, int linesBeforeFailure) : IRpcTransport
    {
        /// <inheritdoc />
        public async IAsyncEnumerable<string> ReadLinesAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            int delivered = 0;
            await foreach (string line in inner.ReadLinesAsync(cancellationToken).ConfigureAwait(false))
            {
                yield return line;
                if (++delivered == linesBeforeFailure)
                {
                    throw new InvalidOperationException("The test transport failed mid-stream.");
                }
            }
        }

        /// <inheritdoc />
        public Task SendLineAsync(string line, CancellationToken cancellationToken) =>
            inner.SendLineAsync(line, cancellationToken);

        /// <inheritdoc />
        public Task CloseInputAsync(CancellationToken cancellationToken) =>
            inner.CloseInputAsync(cancellationToken);

        /// <inheritdoc />
        public Task<int> WaitForExitAsync(CancellationToken cancellationToken) =>
            inner.WaitForExitAsync(cancellationToken);

        /// <inheritdoc />
        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
