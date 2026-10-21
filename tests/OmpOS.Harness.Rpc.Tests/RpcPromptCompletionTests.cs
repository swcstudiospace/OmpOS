using System.Diagnostics.CodeAnalysis;
using OmpOS.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Rpc.Tests;

/// <summary>Prompt completion correlation: unknown ids fault, exact repeats stay duplicate completions.</summary>
public sealed class RpcPromptCompletionTests
{
    private const string ReadyLine =
        """{"type":"ready","protocolVersion":1,"supportedProtocolVersions":[1,2],"maxFrameBytes":1048576,"maxReassembledFrameBytes":67108864}""";

    /// <summary>
    /// A <c>prompt_result</c> for an id that is neither the active prompt nor the last completed one faults the
    /// session with <c>MalformedFrameHarnessException</c> instead of stalling the consumer.
    /// </summary>
    [Fact]
    [SuppressMessage(
        "Usage",
        "CA2000",
        Justification = "The OmpRpcSession takes ownership of the transport and disposes it in DisposeAsync.")]
    public async Task PromptResultWithUnknownIdFaultsSessionWithMalformedFrame()
    {
        var ct = TestContext.Current.CancellationToken;
        FakeRpcTransport transport = Engine(
            (command, id) => command switch
            {
                "prompt" =>
                [
                    RpcTestData.Ok(id, "prompt", """{"agentInvoked":true}"""),
                    RpcTestData.PromptResult("bogus-id", "completed"),
                ],
                _ => null,
            });
        var session = new OmpRpcSession(transport, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
        await session.CreateAsync(ct).ConfigureAwait(true);

        await Should.ThrowAsync<MalformedFrameHarnessException>(
            () => CollectAsync(session.SubmitPromptAsync("hi", new PromptOptions(), ct))).ConfigureAwait(true);

        session.State.ShouldBe(HarnessState.Faulted);
        await session.DisposeAsync().ConfigureAwait(true);
    }

    /// <summary>
    /// An exact repeat of the last completed prompt id still faults as a duplicate completion, not as malformed.
    /// </summary>
    [Fact]
    [SuppressMessage(
        "Usage",
        "CA2000",
        Justification = "The OmpRpcSession takes ownership of the transport and disposes it in DisposeAsync.")]
    public async Task RepeatedPromptResultForCompletedIdFaultsAsDuplicateCompletion()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken, timeout.Token);
        CancellationToken ct = linked.Token;

        string? promptId = null;
        FakeRpcTransport transport = Engine(
            (command, id) =>
            {
                if (string.Equals(command, "prompt", StringComparison.Ordinal))
                {
                    promptId = id;
                    return (IEnumerable<string>?)
                    [
                        RpcTestData.Ok(id, "prompt", """{"agentInvoked":true}"""),
                        RpcTestData.PromptResult(id, "completed"),
                    ];
                }

                return null;
            });
        var session = new OmpRpcSession(transport, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
        await session.CreateAsync(ct).ConfigureAwait(true);

        List<HarnessEvent> events = await CollectAsync(session.SubmitPromptAsync("hi", new PromptOptions(), ct))
            .ConfigureAwait(true);
        events.ShouldBe([new CompletedEvent("completed")]);

        transport.Emit(RpcTestData.PromptResult(promptId!, "completed"));
        for (int i = 0; i < 100 && session.State != HarnessState.Faulted; i++)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10), ct).ConfigureAwait(true);
        }

        session.State.ShouldBe(HarnessState.Faulted);
        DisconnectedHarnessException failure = await Should.ThrowAsync<DisconnectedHarnessException>(
            () => session.ExecuteAsync(id => new GetStateCommand { Id = id }, ct)).ConfigureAwait(true);
        failure.InnerException.ShouldBeOfType<DuplicateCompletionHarnessException>();
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
}
