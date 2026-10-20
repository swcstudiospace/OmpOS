using System.Text.Json;
using OmpOS.Contracts.Serialization;
using OmpOS.Contracts.Streaming;
using OmpOS.Contracts.Ui;
using OmpOS.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace OmpOS.Sessions.Tests;

/// <summary>Replay equality and mapping rules of <see cref="HarnessEventProjection"/> (SESS-03).</summary>
public sealed class ProjectionReplayTests
{
    private const string SessionId = "s-replay";
    private const string CorrelationId = "s-replay/turn-1";
    private static readonly DateTimeOffset Stamp = new(2026, 10, 20, 9, 30, 0, TimeSpan.Zero);

    /// <summary>
    /// A fixed 23-event script: 22 harness events covering every harness kind, followed by one malformed
    /// stream event whose payload carries no kind discriminator.
    /// </summary>
    private static readonly HarnessEvent[] HarnessScript =
    [
        new TextDeltaEvent("Hel"),
        new ThoughtEvent("considering"),
        new ToolCallEvent("call-1", "read", "{\"path\":\"a.txt\"}"),
        new ToolCallUpdateEvent("call-1", "completed", "{}"),
        new PermissionRequestEvent("call-2", "bash", "{\"cmd\":\"ls\",\"options\":[\"allow\",\"deny\"]}"),
        new PlanEvent("1. read\n2. answer"),
        new UiBlockEvent(new MarkdownBlock { Id = "block-1", Markdown = "**hi**" }),
        new TextDeltaEvent("lo"),
        new TextDeltaEvent(" world"),
        new ToolCallEvent("call-3", "write", "{\"path\":\"b.txt\",\"content\":\"x\"}"),
        new ToolCallUpdateEvent("call-3", "failed", "{}"),
        new ThoughtEvent("retrying"),
        new ToolCallUpdateEvent("call-9", "completed", "{}"),
        new TextDeltaEvent("!"),
        new UiBlockEvent(new MarkdownBlock { Id = "block-2", Markdown = "# done" }),
        new ErrorEvent("transient", true),
        new TextDeltaEvent("end"),
        new PlanEvent("- a\n\n- b"),
        new PermissionRequestEvent("call-4", "rm", "not json"),
        new ToolCallUpdateEvent("call-4", "cancelled", "{}"),
        new ToolCallEvent("call-5", "read", "{\"path\":\"c\"}"),
        new CompletedEvent("end_turn"),
    ];

    /// <summary>Folding the script, serializing the stream-event log, and replaying the parsed log reproduces identical UI state.</summary>
    [Fact]
    public void ReplayingSerializedLogReproducesIdenticalUiState()
    {
        var log = BuildLog();
        log.Count.ShouldBe(23);

        var live = log.Aggregate(ProjectionState.Empty, HarnessEventProjection.Fold);
        var json = JsonSerializer.Serialize(log.ToArray(), OmpOsJsonContext.Default.StreamEventArray);
        var replayedLog = JsonSerializer.Deserialize(json, OmpOsJsonContext.Default.StreamEventArray);
        replayedLog.ShouldNotBeNull();
        var replayed = replayedLog.Aggregate(ProjectionState.Empty, HarnessEventProjection.Fold);

        Snapshot(replayed).ShouldBe(Snapshot(live));
        live.Blocks.Count.ShouldBeGreaterThan(8);
        live.CompletionReason.ShouldBe("end_turn");
    }

    /// <summary>Folding harness events directly equals folding the stream events they map to.</summary>
    [Fact]
    public void HarnessFoldEqualsFoldOfMappedStreamEvents()
    {
        var direct = HarnessScript.Aggregate(ProjectionState.Empty, HarnessEventProjection.Fold);

        var viaStream = BuildLog().Take(HarnessScript.Length).Aggregate(ProjectionState.Empty, HarnessEventProjection.Fold);

        Snapshot(viaStream).ShouldBe(Snapshot(direct));
    }

    /// <summary>The fold is deterministic: the same events from the same state give the same result every time.</summary>
    [Fact]
    public void FoldIsDeterministic()
    {
        var log = BuildLog();

        var first = log.Aggregate(ProjectionState.Empty, HarnessEventProjection.Fold);
        var second = log.Aggregate(ProjectionState.Empty, HarnessEventProjection.Fold);

        Snapshot(second).ShouldBe(Snapshot(first));
    }

    /// <summary>Every non-stream harness kind is kept in the log as UnknownPayload with its harness kind name; nothing is dropped.</summary>
    [Fact]
    public void NonStreamKindsFoldIntoUnknownPayloadWithOriginalKind()
    {
        var expected = new Dictionary<Type, string>
        {
            [typeof(ThoughtEvent)] = "thought",
            [typeof(ToolCallEvent)] = "tool_call",
            [typeof(ToolCallUpdateEvent)] = "tool_call_update",
            [typeof(PermissionRequestEvent)] = "permission_request",
            [typeof(PlanEvent)] = "plan",
        };

        foreach (var evt in HarnessScript.Where(evt => expected.ContainsKey(evt.GetType())))
        {
            var mapped = HarnessEventProjection.ToStreamEvents(1, SessionId, CorrelationId, Stamp, evt).Single();

            var unknown = mapped.Payload.ShouldBeOfType<UnknownPayload>();
            unknown.OriginalKind.ShouldBe(expected[evt.GetType()]);
            unknown.Raw.ValueKind.ShouldBe(JsonValueKind.Object);
            unknown.Raw.GetProperty("kind").GetString().ShouldBe(expected[evt.GetType()]);
        }
    }

    /// <summary>Thoughts stay in the log but do not produce UI blocks.</summary>
    [Fact]
    public void ThoughtIsLoggedButNotRendered()
    {
        var before = Snapshot(ProjectionState.Empty);

        var after = HarnessEventProjection.Fold(ProjectionState.Empty, new ThoughtEvent("hmm"));

        Snapshot(after).ShouldBe(before);
        HarnessEventProjection.ToStreamEvents(1, SessionId, CorrelationId, Stamp, new ThoughtEvent("hmm"))
            .Single().Payload.ShouldBeOfType<UnknownPayload>().OriginalKind.ShouldBe("thought");
    }

    /// <summary>Text deltas accumulate in the streaming buffer and flush into a markdown block at the next block boundary.</summary>
    [Fact]
    public void TextDeltasFlushIntoMarkdownBlockAtBoundary()
    {
        var streaming = new[] { new TextDeltaEvent("he"), new TextDeltaEvent("llo") }
            .Aggregate(ProjectionState.Empty, HarnessEventProjection.Fold);

        streaming.StreamingText.ShouldBe("hello");
        streaming.Blocks.ShouldBeEmpty();

        var flushed = HarnessEventProjection.Fold(streaming, new PlanEvent("1. step"));

        flushed.StreamingText.ShouldBeEmpty();
        flushed.Blocks.Select(static block => block.GetType()).ShouldBe([typeof(MarkdownBlock), typeof(PlanBlock)]);
        flushed.Blocks[0].ShouldBeOfType<MarkdownBlock>().Markdown.ShouldBe("hello");
    }

    /// <summary>A tool call produces a card that an update changes in place rather than appending a second card.</summary>
    [Fact]
    public void ToolCallUpdateChangesStatusOfExistingCard()
    {
        var state = new HarnessEvent[]
        {
            new ToolCallEvent("call-1", "read", "{\"path\":\"a\"}"),
            new ToolCallUpdateEvent("call-1", "completed", "{}"),
        }.Aggregate(ProjectionState.Empty, HarnessEventProjection.Fold);

        state.Blocks.Count.ShouldBe(1);
        var card = state.Blocks[0].ShouldBeOfType<ToolCallBlock>();
        card.ToolName.ShouldBe("read");
        card.Status.ShouldBe("completed");
    }

    /// <summary>An update with no matching card is kept as an unknown block rather than dropped.</summary>
    [Fact]
    public void UnmatchedToolCallUpdateIsKeptAsUnknownBlock()
    {
        var state = HarnessEventProjection.Fold(ProjectionState.Empty, new ToolCallUpdateEvent("ghost", "completed", "{}"));

        state.Blocks.Count.ShouldBe(1);
        state.Blocks[0].ShouldBeOfType<UnknownBlock>().OriginalKind.ShouldBe("tool_call_update");
    }

    /// <summary>Permission options are taken verbatim from the payload when it carries an options array.</summary>
    [Fact]
    public void PermissionBlockCarriesVerbatimOptions()
    {
        var state = HarnessEventProjection.Fold(
            ProjectionState.Empty,
            new PermissionRequestEvent("call-2", "bash", "{\"options\":[\"allow\",\"deny\"]}"));

        var block = state.Blocks.Single().ShouldBeOfType<PermissionBlock>();
        block.Prompt.ShouldBe("bash");
        block.Options.ShouldBe(["allow", "deny"]);
    }

    /// <summary>Mapping stamps the injected timestamp and sequence number, never the wall clock.</summary>
    [Fact]
    public void ToStreamEventsUsesInjectedSequenceAndTimestamp()
    {
        var events = HarnessEventProjection.ToStreamEvents(7, SessionId, CorrelationId, Stamp, new TextDeltaEvent("x"));

        events.Count.ShouldBe(1);
        events[0].Seq.ShouldBe(7);
        events[0].SessionId.ShouldBe(SessionId);
        events[0].CorrelationId.ShouldBe(CorrelationId);
        events[0].Timestamp.ShouldBe(Stamp);
        events[0].Payload.ShouldBeOfType<TextDeltaPayload>().Delta.ShouldBe("x");
    }

    /// <summary>Maps the harness script to stream events numbered 1..22 and appends the malformed event as Seq 23.</summary>
    private static List<StreamEvent> BuildLog()
    {
        var log = new List<StreamEvent>();
        for (var i = 0; i < HarnessScript.Length; i++)
        {
            log.AddRange(HarnessEventProjection.ToStreamEvents(i + 1, SessionId, CorrelationId, Stamp, HarnessScript[i]));
        }

        log.Add(new StreamEvent
        {
            Seq = log.Count + 1,
            SessionId = SessionId,
            CorrelationId = CorrelationId,
            Timestamp = Stamp,
            Payload = new UnknownPayload
            {
                OriginalKind = null,
                Raw = JsonDocument.Parse("{\"weird\":true}").RootElement.Clone(),
            },
        });
        return log;
    }

    /// <summary>Serializes the UI state to a comparable value. Collection and JSON equality are structural in the serialized form.</summary>
    private static string Snapshot(ProjectionState state)
    {
        var blocks = JsonSerializer.Serialize(state.Blocks.ToArray(), OmpOsJsonContext.Default.UiBlockArray);
        return $"{blocks}|{state.StreamingText}|{state.CompletionReason ?? "<null>"}";
    }
}
