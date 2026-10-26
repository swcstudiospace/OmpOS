using System.Text.Json;
using OmpOS.Contracts.Serialization;
using OmpOS.Contracts.Ui;
using Shouldly;
using Xunit;

namespace OmpOS.Contracts.Tests.Ui;

/// <summary>
/// Verifies the tool-result and file-edit blocks and the permission decision added in contract 1.1.0: they travel
/// under their kind, an older reader still degrades them to <see cref="UnknownBlock"/>, and a kind this version does
/// not know still deserializes to <see cref="UnknownBlock"/>.
/// </summary>
public sealed class ToolBlockContractTests
{
    /// <summary>A kind this version has no type for degrades to UnknownBlock with its payload kept, as before the new kinds existed.</summary>
    [Fact]
    public void UnknownBlockKindStillDeserializesToUnknown()
    {
        const string json = """{"kind":"tool_result_v2","id":"b1","callId":"c1","output":"x"}""";

        var back = JsonSerializer.Deserialize(json, OmpOsJsonContext.Default.UiBlock).ShouldBeOfType<UnknownBlock>();

        back.OriginalKind.ShouldBe("tool_result_v2");
        back.Id.ShouldBe("b1");
        JsonSerializer.Serialize(back, OmpOsJsonContext.Default.UiBlock).ShouldBe(json);
    }

    /// <summary>A file edit that created a file has no old text, and the wire carries none.</summary>
    [Fact]
    public void ACreatedFileEditWritesNoOldTextAndReadsBackWithNone()
    {
        var block = new FileEditBlock { Id = "e1", CallId = "c1", Path = "src/new.cs", NewText = "class A;" };

        var json = JsonSerializer.Serialize<UiBlock>(block, OmpOsJsonContext.Default.UiBlock);

        json.ShouldBe("""{"kind":"file_edit","callId":"c1","path":"src/new.cs","newText":"class A;","id":"e1"}""");
        JsonSerializer.Deserialize(json, OmpOsJsonContext.Default.UiBlock).ShouldBeOfType<FileEditBlock>().OldText.ShouldBeNull();
    }

    /// <summary>A tool result missing its output is a malformed known kind, like every other known kind.</summary>
    [Fact]
    public void AToolResultWithoutOutputThrows()
    {
        const string json = """{"kind":"tool_result","id":"r1","callId":"c1","toolName":"bash","status":"completed"}""";

        Should.Throw<JsonException>(() => JsonSerializer.Deserialize(json, OmpOsJsonContext.Default.UiBlock));
    }

    /// <summary>A permission block with no decision writes none, so a block from before the field existed is unchanged on the wire.</summary>
    [Fact]
    public void AnUndecidedPermissionWritesNoDecision()
    {
        var block = new PermissionBlock { Id = "p1", Prompt = "Allow?", Options = ["Allow"] };

        JsonSerializer.Serialize<UiBlock>(block, OmpOsJsonContext.Default.UiBlock)
            .ShouldBe("""{"kind":"permission","prompt":"Allow?","options":["Allow"],"id":"p1"}""");
    }

    /// <summary>A decision round-trips, and a decision value this version does not know is carried as it came.</summary>
    [Theory]
    [InlineData(ApprovalDecisions.Approved)]
    [InlineData(ApprovalDecisions.Denied)]
    [InlineData("expired")]
    public void ADecisionRoundTripsVerbatim(string decision)
    {
        var block = new PermissionBlock { Id = "p1", Prompt = "Allow?", Options = ["Allow"], Decision = decision };

        var json = JsonSerializer.Serialize<UiBlock>(block, OmpOsJsonContext.Default.UiBlock);
        var back = JsonSerializer.Deserialize(json, OmpOsJsonContext.Default.UiBlock).ShouldBeOfType<PermissionBlock>();

        json.ShouldContain($"\"decision\":\"{decision}\"");
        back.Decision.ShouldBe(decision);
    }

    /// <summary>The ACP update shape yields its text entries joined by a newline and its diff entries in order.</summary>
    [Fact]
    public void AnAcpUpdateYieldsItsTextAndItsEdits()
    {
        const string payload = """
            {"sessionUpdate":"tool_call_update","toolCallId":"c1","status":"completed","content":[
              {"type":"content","content":{"type":"text","text":"line one"}},
              {"type":"diff","path":"src/a.cs","oldText":"old","newText":"new"},
              {"type":"terminal","terminalId":"t1"},
              {"type":"content","content":{"type":"text","text":"line two"}},
              {"type":"diff","path":"src/b.cs","newText":"created"}
            ]}
            """;

        var content = ToolUpdateContent.Read(payload);

        content.Output.ShouldBe("line one\nline two");
        content.Edits.ShouldBe([new ToolFileEdit("src/a.cs", "old", "new"), new ToolFileEdit("src/b.cs", null, "created")]);
    }

    /// <summary>With no text entry, a string raw output stands in for the text and a structured one is written as JSON.</summary>
    [Theory]
    [InlineData("""{"rawOutput":"done"}""", "done")]
    [InlineData("""{"rawOutput":{"exitCode":0}}""", """{"exitCode":0}""")]
    [InlineData("""{"rawOutput":null}""", "")]
    public void RawOutputStandsInForMissingText(string payload, string expected)
    {
        ToolUpdateContent.Read(payload).Output.ShouldBe(expected);
    }

    /// <summary>A payload that is not a JSON object carries nothing, and reading it does not throw.</summary>
    [Theory]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    [InlineData("\"text\"")]
    [InlineData("{}")]
    [InlineData("""{"content":"text"}""")]
    [InlineData("""{"content":[{"type":"diff","newText":"no path"},7,{"type":"content","content":{"type":"image"}}]}""")]
    public void APayloadWithNoUsableContentYieldsNone(string payload)
    {
        var content = ToolUpdateContent.Read(payload);

        content.Output.ShouldBeEmpty();
        content.Edits.ShouldBeEmpty();
    }
}
