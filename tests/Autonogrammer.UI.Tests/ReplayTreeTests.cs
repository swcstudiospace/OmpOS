using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Autonogrammer.Contracts.Serialization;
using Autonogrammer.Contracts.Streaming;
using Autonogrammer.Contracts.Ui;
using Autonogrammer.GenUi;
using Autonogrammer.Presentation.State;
using Autonogrammer.UI.Components;
using Bunit;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests;

/// <summary>Replay equality of <see cref="UiFolder"/> over the fixed 23-event fixture (UI-04).</summary>
public sealed class ReplayTreeTests : UiTestContext
{
    private const string SessionId = "s-replay";
    private const string CorrelationId = "s-replay/turn-1";
    private static readonly DateTimeOffset Stamp = new(2026, 10, 20, 9, 30, 0, TimeSpan.Zero);

    private static readonly Regex HandlerIds = new(" blazor:[A-Za-z]+=\"[0-9a-f-]+\"", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    /// <summary>Folding the fixed 23-event script twice yields identical trees with the same block sequence.</summary>
    [Fact]
    public void FoldTwiceProducesIdenticalTrees()
    {
        var log = BuildLog();
        log.Count.ShouldBe(23);

        var first = log.Aggregate(UiTree.Empty, UiFolder.Fold);
        var second = log.Aggregate(UiTree.Empty, UiFolder.Fold);

        Snapshot(second).ShouldBe(Snapshot(first));
        first.Blocks.Count.ShouldBeGreaterThan(8);
        first.CompletionReason.ShouldBe("end_turn");
    }

    /// <summary>Both folds render identical markup: the replay is view-stable as well as tree-stable.</summary>
    [Fact]
    public void RefoldRendersIdenticalMarkup()
    {
        var log = BuildLog();

        var first = log.Aggregate(UiTree.Empty, UiFolder.Fold);
        var second = log.Aggregate(UiTree.Empty, UiFolder.Fold);

        RenderToMarkup(first).ShouldBe(RenderToMarkup(second));
        RenderToMarkup(first).Length.ShouldBeGreaterThan(0);
    }

    /// <summary>Malformed payload shapes (array, string, number, null, boolean roots) never throw and never blank the view.</summary>
    [Theory]
    [InlineData("[1,2]")]
    [InlineData("\"just a string\"")]
    [InlineData("42")]
    [InlineData("null")]
    [InlineData("true")]
    [InlineData("{\"weird\":true}")]
    public void MalformedPayloadShapesNeverThrow(string rawJson)
    {
        using var doc = JsonDocument.Parse(rawJson);
        var evt = Stream(23, new UnknownPayload { OriginalKind = null, Raw = doc.RootElement.Clone() });

        var tree = UiFolder.Fold(UiTree.Empty, evt);

        tree.Blocks.Count.ShouldBe(1);
        tree.Blocks[0].ShouldBeOfType<UnknownBlock>();
        RenderToMarkup(tree).Length.ShouldBeGreaterThan(0);
    }

    /// <summary>An unrecognized payload kind with an object root is kept as an unknown block, never thrown.</summary>
    [Fact]
    public void UnrecognizedKindIsKeptAsUnknownBlock()
    {
        using var doc = JsonDocument.Parse("{\"kind\":\"future-kind\",\"data\":1}");
        var evt = Stream(1, new UnknownPayload { OriginalKind = "future-kind", Raw = doc.RootElement.Clone() });

        var tree = UiFolder.Fold(UiTree.Empty, evt);

        tree.Blocks.Single().ShouldBeOfType<UnknownBlock>().OriginalKind.ShouldBe("future-kind");
    }

    private string RenderToMarkup(UiTree tree)
    {
        var cut = Render<SessionView>(parameters => parameters
            .Add(p => p.Tree, tree)
            .Add(p => p.ViewState, SessionViewState.Success));
        // Blazor numbers event-handler wirings per render session and gives every captured element reference (@ref) a fresh GUID; neither carries tree content.
        return HandlerIds.Replace(cut.Markup, string.Empty);
    }

    private static StreamEvent Stream(long seq, StreamEventPayload payload) => new()
    {
        Seq = seq,
        SessionId = SessionId,
        CorrelationId = CorrelationId,
        Timestamp = Stamp,
        Payload = payload,
    };

    private static UnknownPayload Unknown(string kind, params (string Name, string Value)[] fields)
    {
        var obj = new JsonObject { ["kind"] = kind };
        foreach (var (name, value) in fields)
        {
            obj[name] = value;
        }

        using var doc = JsonDocument.Parse(obj.ToJsonString());
        return new UnknownPayload { OriginalKind = kind, Raw = doc.RootElement.Clone() };
    }

    /// <summary>Builds the fixed 23-event script: 22 known shapes plus one malformed payload with no kind.</summary>
    private static List<StreamEvent> BuildLog()
    {
        var payloads = new StreamEventPayload[]
        {
            new TextDeltaPayload { Delta = "Hel" },
            Unknown("thought", ("text", "considering")),
            Unknown("tool_call", ("callId", "call-1"), ("name", "read"), ("payloadJson", "{\"path\":\"a.txt\"}")),
            Unknown("tool_call_update", ("callId", "call-1"), ("status", "completed"), ("payloadJson", "{}")),
            Unknown("permission_request", ("callId", "call-2"), ("name", "bash"), ("payloadJson", "{\"cmd\":\"ls\",\"options\":[\"allow\",\"deny\"]}")),
            Unknown("plan", ("markdown", "1. read\n2. answer")),
            new UiBlockPayload { Block = new MarkdownBlock { Id = "block-1", Markdown = "**hi**" } },
            new TextDeltaPayload { Delta = "lo" },
            new TextDeltaPayload { Delta = " world" },
            Unknown("tool_call", ("callId", "call-3"), ("name", "write"), ("payloadJson", "{\"path\":\"b.txt\",\"content\":\"x\"}")),
            Unknown("tool_call_update", ("callId", "call-3"), ("status", "failed"), ("payloadJson", "{}")),
            Unknown("thought", ("text", "retrying")),
            Unknown("tool_call_update", ("callId", "call-9"), ("status", "completed"), ("payloadJson", "{}")),
            new TextDeltaPayload { Delta = "!" },
            new UiBlockPayload { Block = new MarkdownBlock { Id = "block-2", Markdown = "# done" } },
            new ErrorPayload { Message = "transient", Retryable = true },
            new TextDeltaPayload { Delta = "end" },
            Unknown("plan", ("markdown", "- a\n\n- b")),
            Unknown("permission_request", ("callId", "call-4"), ("name", "rm"), ("payloadJson", "not json")),
            Unknown("tool_call_update", ("callId", "call-4"), ("status", "cancelled"), ("payloadJson", "{}")),
            Unknown("tool_call", ("callId", "call-5"), ("name", "read"), ("payloadJson", "{\"path\":\"c\"}")),
            new CompletedPayload { Reason = "end_turn" },
            new UnknownPayload
            {
                OriginalKind = null,
                Raw = JsonDocument.Parse("{\"weird\":true}").RootElement.Clone(),
            },
        };

        var log = new List<StreamEvent>(payloads.Length);
        for (var i = 0; i < payloads.Length; i++)
        {
            log.Add(Stream(i + 1, payloads[i]));
        }

        return log;
    }

    private static string Snapshot(UiTree tree)
    {
        var blocks = JsonSerializer.Serialize(tree.Blocks.ToArray(), AutonogrammerJsonContext.Default.UiBlockArray);
        return $"{blocks}|{tree.StreamingText}|{tree.CompletionReason ?? "<null>"}";
    }
}
