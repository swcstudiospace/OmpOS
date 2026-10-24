using System.Text.Json;
using OmpOS.Api.Sse;
using OmpOS.Contracts.Streaming;
using OmpOS.Contracts.Ui;
using Shouldly;
using Xunit;

namespace OmpOS.Api.IntegrationTests;

/// <summary>The SSE <c>event:</c> name each stream payload kind is sent under.</summary>
public sealed class StreamEventKindTests
{
    /// <summary>Every payload type maps to its wire kind name, including diagnostics, which are named <c>diagnostic</c>.</summary>
    [Fact]
    public void Of_NamesEachPayloadKindForTheEventField()
    {
        StreamEventKind.Of(new TextDeltaPayload { Delta = "a" }).ShouldBe("text_delta");
        StreamEventKind.Of(new UiBlockPayload { Block = new MarkdownBlock { Id = "block-1", Markdown = "x" } }).ShouldBe("ui_block");
        StreamEventKind.Of(new ErrorPayload { Message = "boom", Retryable = false }).ShouldBe("error");
        StreamEventKind.Of(new CompletedPayload { Reason = "end_turn" }).ShouldBe("completed");
        StreamEventKind.Of(new UserMessagePayload { Text = "hi" }).ShouldBe("user_message");
        StreamEventKind.Of(new DiagnosticPayload { Message = "slow tool" }).ShouldBe("diagnostic");
        StreamEventKind.Of(new UnknownPayload { OriginalKind = "tool_call", Raw = RawObject() }).ShouldBe("tool_call");
        StreamEventKind.Of(new UnknownPayload { OriginalKind = null, Raw = RawObject() }).ShouldBe("unknown");
    }

    /// <summary>Only completed and error payloads terminate a stream; a diagnostic does not.</summary>
    [Fact]
    public void IsTerminal_OnlyCompletedAndErrorEndTheStream()
    {
        StreamEventKind.IsTerminal(new CompletedPayload { Reason = "end_turn" }).ShouldBeTrue();
        StreamEventKind.IsTerminal(new ErrorPayload { Message = "boom", Retryable = false }).ShouldBeTrue();
        StreamEventKind.IsTerminal(new DiagnosticPayload { Message = "slow tool" }).ShouldBeFalse();
        StreamEventKind.IsTerminal(new UserMessagePayload { Text = "hi" }).ShouldBeFalse();
    }

    private static JsonElement RawObject()
    {
        using var doc = JsonDocument.Parse("{\"kind\":\"x\"}");
        return doc.RootElement.Clone();
    }
}
