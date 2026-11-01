using System.Text.Json.Nodes;
using Autonogrammer.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Acp.Tests;

/// <summary>The usage update becomes a context usage event, and mention resources travel as embedded resource blocks of the prompt.</summary>
public sealed class AcpUsageAndResourcesTests
{
    private static byte[] Update(string update) =>
        AcpFixtures.Utf8($"{{\"jsonrpc\":\"2.0\",\"method\":\"session/update\",\"params\":{{\"sessionId\":\"{AcpFixtures.CapturedSessionId}\",\"update\":{update}}}}}");

    private static IEnumerable<byte[]> Script(byte[] frame, JsonObject[] seen)
    {
        if (!string.Equals(AcpFixtures.MethodOf(frame), "session/prompt", StringComparison.Ordinal))
        {
            return AcpFixtures.Reply(frame);
        }

        seen[0] = JsonNode.Parse(frame)!.AsObject();
        return
        [
            Update("{\"sessionUpdate\":\"usage_update\",\"used\":1200,\"size\":200000}"),
            AcpFixtures.Utf8($"{{\"jsonrpc\":\"2.0\",\"id\":{AcpFixtures.IdOf(frame)},\"result\":{{\"stopReason\":\"end_turn\"}}}}"),
        ];
    }

    private static async Task<List<HarnessEvent>> RunAsync(PromptOptions options, JsonObject[] seen, CancellationToken ct)
    {
        var transport = new ScriptedAcpTransport(frame => Script(frame, seen));
        await using var transportScope = transport.ConfigureAwait(true);
        var session = new AcpAgentSession(transport, new AcpClientOptions { WorkingDirectory = AcpFixtures.CapturedCwd, AuthMethodId = "agent" });
        await using var sessionScope = session.ConfigureAwait(true);
        await session.CreateAsync(ct).ConfigureAwait(true);
        var events = new List<HarnessEvent>();
        await foreach (var harnessEvent in session.SubmitPromptAsync("look at this", options, ct).WithCancellation(ct).ConfigureAwait(true))
        {
            events.Add(harnessEvent);
        }

        return events;
    }

    [Fact]
    public async Task AUsageUpdateIsAContextUsageEventBeforeTheCompletion()
    {
        var events = await RunAsync(new PromptOptions(), new JsonObject[1], TestContext.Current.CancellationToken).ConfigureAwait(true);

        events[0].ShouldBe(new ContextUsageEvent(1200, 200000));
        events[1].ShouldBe(new CompletedEvent("end_turn"));
    }

    /// <summary>A usage update that carries a cost keeps the amount and currency; one without a cost stays equal to the two-argument event.</summary>
    [Fact]
    public async Task AUsageUpdateWithCostKeepsTheAmountAndOneWithoutStaysNull()
    {
        var withCost = await RunScriptAsync(Update("{\"sessionUpdate\":\"usage_update\",\"used\":10,\"size\":20,\"cost\":{\"amount\":1.25,\"currency\":\"USD\"}}"), TestContext.Current.CancellationToken).ConfigureAwait(true);
        withCost[0].ShouldBe(new ContextUsageEvent(10, 20, new UsageCost(1.25m, "USD")));

        var without = await RunAsync(new PromptOptions(), new JsonObject[1], TestContext.Current.CancellationToken).ConfigureAwait(true);
        without[0].ShouldBe(new ContextUsageEvent(1200, 200000));
        (without[0] as ContextUsageEvent)!.Cost.ShouldBeNull();
    }

    private static async Task<List<HarnessEvent>> RunScriptAsync(byte[] usageFrame, CancellationToken ct)
    {
        var transport = new ScriptedAcpTransport(frame =>
        {
            if (!string.Equals(AcpFixtures.MethodOf(frame), "session/prompt", StringComparison.Ordinal))
            {
                return AcpFixtures.Reply(frame);
            }

            return
            [
                usageFrame,
                AcpFixtures.Utf8($"{{\"jsonrpc\":\"2.0\",\"id\":{AcpFixtures.IdOf(frame)},\"result\":{{\"stopReason\":\"end_turn\"}}}}"),
            ];
        });
        await using var transportScope = transport.ConfigureAwait(true);
        var session = new AcpAgentSession(transport, new AcpClientOptions { WorkingDirectory = AcpFixtures.CapturedCwd, AuthMethodId = "agent" });
        await using var sessionScope = session.ConfigureAwait(true);
        await session.CreateAsync(ct).ConfigureAwait(true);
        var events = new List<HarnessEvent>();
        await foreach (var harnessEvent in session.SubmitPromptAsync("look at this", new PromptOptions(), ct).WithCancellation(ct).ConfigureAwait(true))
        {
            events.Add(harnessEvent);
        }

        return events;
    }

    [Fact]
    public async Task APromptWithoutResourcesIsOneTextBlock()
    {
        var seen = new JsonObject[1];
        _ = await RunAsync(new PromptOptions(), seen, TestContext.Current.CancellationToken).ConfigureAwait(true);

        var blocks = seen[0]["params"]!["prompt"]!.AsArray();
        blocks.Count.ShouldBe(1);
        blocks[0]!["type"]!.GetValue<string>().ShouldBe("text");
    }

    [Fact]
    public async Task AResourceIsSentEmbeddedWithItsTextAndTheUserTextIsKept()
    {
        var seen = new JsonObject[1];
        var options = new PromptOptions { Resources = [new PromptResource(new Uri("file:///work/a.txt"), "text/plain", "contents of a", "a.txt", 13, false)] };

        _ = await RunAsync(options, seen, TestContext.Current.CancellationToken).ConfigureAwait(true);

        var text = seen[0].ToJsonString();
        text.ShouldContain("\"resource\"", Case.Sensitive);
        text.ShouldContain("contents of a", Case.Sensitive);
        text.ShouldContain("file:///work/a.txt", Case.Sensitive);
        text.ShouldContain("look at this", Case.Sensitive);
    }
}
