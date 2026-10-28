using System.Text;
using System.Text.Json.Nodes;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.Acp;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.ContractTests;

/// <summary>
/// Replays <c>session/update</c> variants recorded from live <c>omp acp</c> turns (omp 18.12.5, OD-07) through the public
/// ACP session. The harness does not model most of these kinds, so each must reach the mapper's fallback: the turn yields its
/// <see cref="CompletedEvent"/> alone, with no mapped event and no decode diagnostic. <c>usage_update</c> is the exception since
/// the context ring (Phase 63): it is mapped to a <see cref="ContextUsageEvent"/> and nothing else.
/// </summary>
public sealed class OmpLiveVariantContractTests
{
    private const string SessionId = "fixture-session";
    private const string StopReason = "end_turn";
    private const string MethodNotFoundCode = "-32601";

    /// <summary>The live <c>available_commands_update</c> variant is unmodelled and reaches the fallback.</summary>
    [Fact]
    public async Task AvailableCommandsUpdateReachesFallback()
    {
        var ct = TestContext.Current.CancellationToken;
        await AssertUnmodelledVariantAsync("session-update-available-commands-update.json", "available_commands_update", ct).ConfigureAwait(true);
    }

    /// <summary>The live <c>session_info_update</c> variant is unmodelled and reaches the fallback.</summary>
    [Fact]
    public async Task SessionInfoUpdateReachesFallback()
    {
        var ct = TestContext.Current.CancellationToken;
        await AssertUnmodelledVariantAsync("session-update-session-info-update.json", "session_info_update", ct).ConfigureAwait(true);
    }

    /// <summary>The live <c>config_option_update</c> variant is unmodelled and reaches the fallback.</summary>
    [Fact]
    public async Task ConfigOptionUpdateReachesFallback()
    {
        var ct = TestContext.Current.CancellationToken;
        await AssertUnmodelledVariantAsync("session-update-config-option-update.json", "config_option_update", ct).ConfigureAwait(true);
    }

    /// <summary>The live <c>usage_update</c> variant is modelled: it becomes a <see cref="ContextUsageEvent"/> with the used and total token counts, and no fallback or diagnostic.</summary>
    [Fact]
    public async Task UsageUpdateBecomesAContextUsageEvent()
    {
        var ct = TestContext.Current.CancellationToken;
        await AssertVariantAsync("session-update-usage-update.json", "usage_update", [new ContextUsageEvent(28919, 1_000_000), new CompletedEvent(StopReason)], ct).ConfigureAwait(true);
    }

    private static Task AssertUnmodelledVariantAsync(string fixtureName, string expectedKind, CancellationToken ct) =>
        AssertVariantAsync(fixtureName, expectedKind, [new CompletedEvent(StopReason)], ct);

    private static async Task AssertVariantAsync(string fixtureName, string expectedKind, IReadOnlyList<HarnessEvent> expected, CancellationToken ct)
    {
        string update = ReadFixture(fixtureName);
        JsonNode frame = JsonNode.Parse(update)!;
        frame["params"]!["update"]!["sessionUpdate"]!.GetValue<string>().ShouldBe(expectedKind);

        var (client, omp) = LoopbackAcpTransportPair.Create();
        await omp.ConnectAsync(ct).ConfigureAwait(false);
        Task serving = ServeOmpAsync(omp, update, ct);
        var session = new OmpAcpClient(_ => client).CreateSession(new AcpClientOptions());
        var received = new List<HarnessEvent>();
        try
        {
            await session.CreateAsync(ct).ConfigureAwait(false);
            await AsyncStreamHelpers.DrainAsync(session.SubmitPromptAsync("hello", new PromptOptions(), ct), received, ct).ConfigureAwait(false);
        }
        finally
        {
            await session.DisposeAsync().ConfigureAwait(false);
            await omp.CloseAsync(ct).ConfigureAwait(false);
            await serving.ConfigureAwait(false);
        }

        received.ShouldBe(expected);
    }

    private static async Task ServeOmpAsync(LoopbackAcpTransport omp, string update, CancellationToken ct)
    {
        try
        {
            await foreach (ReadOnlyMemory<byte> raw in omp.ReceiveAsync(ct).ConfigureAwait(false))
            {
                JsonNode request = JsonNode.Parse(Encoding.UTF8.GetString(raw.Span))!;
                if (request["id"] is not { } id)
                {
                    continue;
                }

                await AnswerAsync(omp, request["method"]!.GetValue<string>(), id, update, ct).ConfigureAwait(false);
            }
        }
        catch (DisconnectedHarnessException)
        {
            // The client closed the loopback; the scripted omp has nothing left to answer.
        }
    }

    private static async Task AnswerAsync(LoopbackAcpTransport omp, string method, JsonNode id, string update, CancellationToken ct)
    {
        if (string.Equals(method, "session/prompt", StringComparison.Ordinal))
        {
            await EmitAsync(omp, update, ct).ConfigureAwait(false);
            await EmitAsync(omp, Response(id, $"{{\"stopReason\":\"{StopReason}\"}}"), ct).ConfigureAwait(false);
            return;
        }

        string? result = method switch
        {
            "initialize" => "{\"protocolVersion\":1,\"agentCapabilities\":{\"loadSession\":true,\"mcpCapabilities\":{\"http\":true,\"sse\":true},\"promptCapabilities\":{\"embeddedContext\":true,\"image\":true},\"sessionCapabilities\":{\"list\":{},\"fork\":{},\"resume\":{},\"close\":{}}}}",
            "session/new" => $"{{\"sessionId\":\"{SessionId}\"}}",
            "session/close" => "{}",
            _ => null,
        };
        string reply = result is null ? ErrorResponse(id) : Response(id, result);
        await EmitAsync(omp, reply, ct).ConfigureAwait(false);
    }

    private static string Response(JsonNode id, string resultJson) =>
        new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id.DeepClone(),
            ["result"] = JsonNode.Parse(resultJson),
        }.ToJsonString();

    private static string ErrorResponse(JsonNode id) =>
        new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id.DeepClone(),
            ["error"] = new JsonObject { ["code"] = JsonNode.Parse(MethodNotFoundCode), ["message"] = "Method not found" },
        }.ToJsonString();

    private static Task EmitAsync(LoopbackAcpTransport omp, string frame, CancellationToken ct) =>
        omp.SendAsync(Encoding.UTF8.GetBytes(frame), ct);

    private static string ReadFixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name), Encoding.UTF8).Trim();
}
