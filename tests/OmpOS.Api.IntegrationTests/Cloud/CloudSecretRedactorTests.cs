using OmpOS.Api.Cloud;
using OmpOS.Contracts.Ui;
using OmpOS.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace OmpOS.Api.IntegrationTests.Cloud;

/// <summary>The redactor and the event filter: a secret is removed as written, as JSON, split across stream chunks, and in every kind of event.</summary>
public sealed class CloudSecretRedactorTests
{
    private const string Secret = "s3cr3t-value-0123456789";

    /// <summary>A secret is replaced wherever it appears, and text without one is untouched.</summary>
    [Fact]
    public void Redact_ReplacesTheSecretAndNothingElse()
    {
        var redactor = new CloudSecretRedactor([Secret]);

        redactor.Redact("use " + Secret + " twice: " + Secret).ShouldBe("use [REDACTED] twice: [REDACTED]");
        redactor.Redact("nothing to hide").ShouldBe("nothing to hide");
        redactor.Redact(null).ShouldBeNull();
        redactor.Redact(string.Empty).ShouldBe(string.Empty);
    }

    /// <summary>A secret that JSON escapes (quotes, backslashes, angle brackets and ampersands) is found in tool payloads, whichever escaper wrote them.</summary>
    [Fact]
    public void Redact_FindsJsonEscapedForms()
    {
        const string tricky = "pa\"ss\\w<o>&rd'1";
        var redactor = new CloudSecretRedactor([tricky]);

        var escaped = WriteJson("login " + tricky, System.Text.Encodings.Web.JavaScriptEncoder.Default);
        var relaxed = WriteJson("login " + tricky, System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping);

        escaped.ShouldNotContain(tricky, Case.Sensitive);
        escaped.ShouldContain("\\u003C", Case.Sensitive);
        relaxed.ShouldContain("<o>&rd'1", Case.Sensitive);
        redactor.Redact(escaped).ShouldBe("{\"command\":\"login [REDACTED]\"}");
        redactor.Redact(relaxed).ShouldBe("{\"command\":\"login [REDACTED]\"}");
    }

    /// <summary>A multi-line secret such as a PEM key is removed whole and line by line.</summary>
    [Fact]
    public void Redact_RemovesMultiLineSecretsLineByLine()
    {
        const string pem = "-----BEGIN KEY-----\nAAAABBBBCCCCDDDD\nEEEEFFFFGGGGHHHH\n-----END KEY-----";
        var redactor = new CloudSecretRedactor([pem]);

        redactor.Redact("whole: " + pem).ShouldBe("whole: [REDACTED]");
        redactor.Redact("one line: EEEEFFFFGGGGHHHH").ShouldBe("one line: [REDACTED]");
    }

    /// <summary>A redactor with no secret returns every text unchanged and holds nothing back.</summary>
    [Fact]
    public void Empty_PassesThrough()
    {
        CloudSecretRedactor.Empty.HasSecrets.ShouldBeFalse();
        CloudSecretRedactor.Empty.Redact("anything").ShouldBe("anything");
        new CloudSecretRedactor(["", "   "]).HasSecrets.ShouldBeFalse();
    }

    /// <summary>The redactor never prints its secrets.</summary>
    [Fact]
    public void ToString_DoesNotRevealTheSecrets()
    {
        new CloudSecretRedactor([Secret]).ToString().ShouldNotContain(Secret, Case.Sensitive);
        new CloudSecretResolution(new Dictionary<string, string>(StringComparer.Ordinal) { ["A"] = Secret }, []).ToString().ShouldNotContain(Secret, Case.Sensitive);
    }

    /// <summary>A secret split across two stream chunks is removed, and the text before and after it arrives in order.</summary>
    [Fact]
    public void Filter_FindsASecretSplitAcrossChunks()
    {
        var filter = new CloudRedactingEventFilter(new CloudSecretRedactor([Secret]));
        var output = new List<HarnessEvent>();
        output.AddRange(filter.Process(new TextDeltaEvent("the key is " + Secret[..8])));
        output.AddRange(filter.Process(new TextDeltaEvent(Secret[8..] + " and that is all")));
        output.AddRange(filter.Process(new CompletedEvent("end_turn")));

        var text = string.Concat(output.OfType<TextDeltaEvent>().Select(static evt => evt.Text));
        text.ShouldBe("the key is [REDACTED] and that is all");
        output[^1].ShouldBeOfType<CompletedEvent>();
    }

    /// <summary>Held text is released before the next event of another kind, so the order of events is kept.</summary>
    [Fact]
    public void Filter_ReleasesHeldTextBeforeAnotherEvent()
    {
        var filter = new CloudSecretRedactor([Secret]);
        var events = new CloudRedactingEventFilter(filter);
        var output = new List<HarnessEvent>();
        output.AddRange(events.Process(new TextDeltaEvent("short")));
        output.AddRange(events.Process(new ToolCallEvent("c1", "bash", "{\"command\":\"echo " + Secret + "\"}")));

        output.Count.ShouldBe(2);
        output[0].ShouldBeOfType<TextDeltaEvent>().Text.ShouldBe("short");
        output[1].ShouldBeOfType<ToolCallEvent>().PayloadJson.ShouldBe("{\"command\":\"echo [REDACTED]\"}");
    }

    /// <summary>Every text field of every event kind is cleaned.</summary>
    [Fact]
    public void Filter_CleansEveryEventKind()
    {
        var filter = new CloudRedactingEventFilter(new CloudSecretRedactor([Secret]));
        var dirty = "x " + Secret + " y";
        const string clean = "x [REDACTED] y";

        filter.Process(new ThoughtEvent(dirty)).Concat(filter.Flush()).OfType<ThoughtEvent>().Single().Text.ShouldBe(clean);
        filter.Process(new ErrorEvent(dirty, Retryable: false)).Single().ShouldBeOfType<ErrorEvent>().Message.ShouldBe(clean);
        filter.Process(new DiagnosticEvent(dirty)).Single().ShouldBeOfType<DiagnosticEvent>().Message.ShouldBe(clean);
        filter.Process(new PlanEvent(dirty)).Single().ShouldBeOfType<PlanEvent>().Markdown.ShouldBe(clean);
        filter.Process(new ToolCallUpdateEvent("c1", "completed", "{\"out\":\"" + Secret + "\"}")).Single().ShouldBeOfType<ToolCallUpdateEvent>().PayloadJson.ShouldBe("{\"out\":\"[REDACTED]\"}");
        filter.Process(new PermissionRequestEvent("c2", "execute", "{\"cmd\":\"" + Secret + "\"}", "r1")).Single().ShouldBeOfType<PermissionRequestEvent>().PayloadJson.ShouldBe("{\"cmd\":\"[REDACTED]\"}");
        var block = filter.Process(new UiBlockEvent(new MarkdownBlock { Id = "b1", Markdown = dirty })).Single().ShouldBeOfType<UiBlockEvent>();
        block.Block.ShouldBeOfType<MarkdownBlock>().Markdown.ShouldBe(clean);
        filter.Process(new CompletedEvent("end_turn")).Single().ShouldBeOfType<CompletedEvent>().Reason.ShouldBe("end_turn");
    }

    /// <summary>Without secrets the filter forwards each chunk at once, unchanged.</summary>
    [Fact]
    public void Filter_WithoutSecrets_ForwardsAtOnce()
    {
        var filter = new CloudRedactingEventFilter(CloudSecretRedactor.Empty);

        filter.Process(new TextDeltaEvent("a")).Single().ShouldBeOfType<TextDeltaEvent>().Text.ShouldBe("a");
        filter.Flush().ShouldBeEmpty();
    }

    private static string WriteJson(string command, System.Text.Encodings.Web.JavaScriptEncoder encoder)
    {
        using var stream = new MemoryStream();
        using (var writer = new System.Text.Json.Utf8JsonWriter(stream, new System.Text.Json.JsonWriterOptions { Encoder = encoder }))
        {
            writer.WriteStartObject();
            writer.WriteString("command", command);
            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }
}
