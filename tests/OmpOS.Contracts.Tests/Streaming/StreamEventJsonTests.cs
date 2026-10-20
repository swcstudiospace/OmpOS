using System.Text.Json;
using OmpOS.Contracts.Serialization;
using OmpOS.Contracts.Streaming;
using OmpOS.Contracts.Ui;
using Shouldly;
using Xunit;

namespace OmpOS.Contracts.Tests.Streaming;

/// <summary>
/// Verifies <see cref="StreamEvent"/> round-trips Seq, SessionId, CorrelationId,
/// timestamp and every payload variant, and that unknown payload kinds
/// degrade to <see cref="UnknownPayload"/>.
/// </summary>
public sealed class StreamEventJsonTests
{
    public static TheoryData<StreamEventPayload> AllPayloads => new()
    {
        new TextDeltaPayload { Delta = "hello" },
        new UiBlockPayload { Block = new MarkdownBlock { Id = "b1", Markdown = "hi" } },
        new ErrorPayload { Message = "boom", Retryable = false },
        new CompletedPayload { Reason = "done" },
        new UnknownPayload { OriginalKind = "future", Raw = JsonDocument.Parse("""{"kind":"future"}""").RootElement.Clone() },
    };

    [Theory]
    [MemberData(nameof(AllPayloads))]
    public void EveryPayloadKindRoundTrips(StreamEventPayload payload)
    {
        var evt = new StreamEvent
        {
            Seq = 7,
            SessionId = "sess-1",
            CorrelationId = "corr-1",
            Timestamp = new DateTimeOffset(2026, 10, 20, 0, 0, 0, TimeSpan.Zero),
            Payload = payload,
        };

        var json = JsonSerializer.Serialize(evt, OmpOsJsonContext.Default.StreamEvent);
        var back = JsonSerializer.Deserialize(json, OmpOsJsonContext.Default.StreamEvent);

        back.ShouldNotBeNull();
        back.Seq.ShouldBe(7);
        back.SessionId.ShouldBe("sess-1");
        back.CorrelationId.ShouldBe("corr-1");
        back.Timestamp.ShouldBe(evt.Timestamp);
        back.Payload.ShouldBeOfType(payload.GetType());
        JsonSerializer.Serialize(back, OmpOsJsonContext.Default.StreamEvent).ShouldBe(json);
    }

    [Fact]
    public void UnknownPayloadKindDeserializesToUnknownPayload()
    {
        const string json = """{"seq":1,"sessionId":"s","correlationId":"c","timestamp":"2026-10-20T00:00:00Z","payload":{"kind":"future-kind","data":42}}""";

        var back = JsonSerializer.Deserialize(json, OmpOsJsonContext.Default.StreamEvent);

        back.ShouldNotBeNull();
        var payload = back.Payload.ShouldBeOfType<UnknownPayload>();
        payload.OriginalKind.ShouldBe("future-kind");
    }

    [Fact]
    public void MissingPayloadKindDeserializesToUnknownPayloadWithNullOriginalKind()
    {
        const string json = """{"seq":2,"sessionId":"s","correlationId":"c","timestamp":"2026-10-20T00:00:00Z","payload":{"data":42}}""";

        var back = JsonSerializer.Deserialize(json, OmpOsJsonContext.Default.StreamEvent);

        back.ShouldNotBeNull();
        var payload = back.Payload.ShouldBeOfType<UnknownPayload>();
        payload.OriginalKind.ShouldBeNull();
    }

    [Theory]
    [InlineData("""[]""")]
    [InlineData("""[1,2,3]""")]
    [InlineData("\"not-an-object\"")]
    [InlineData("""42""")]
    [InlineData("""true""")]
    public void NonObjectRootDegradesToUnknown(string rootJson)
    {
        var back = JsonSerializer.Deserialize(rootJson, OmpOsJsonContext.Default.StreamEventPayload);

        var payload = back.ShouldBeOfType<UnknownPayload>();
        payload.OriginalKind.ShouldBeNull();
        payload.Raw.GetRawText().ShouldBe(rootJson);
    }

    [Fact]
    public void NullTopLevelPayloadDeserializesToNullWithoutThrowing()
    {
        // STJ default null handling: the converter is not invoked for a null
        // token; the reference deserializes to null. Must not throw.
        JsonSerializer.Deserialize("null", OmpOsJsonContext.Default.StreamEventPayload).ShouldBeNull();
    }
}
