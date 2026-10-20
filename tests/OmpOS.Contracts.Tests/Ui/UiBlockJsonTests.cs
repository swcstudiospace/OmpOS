using System.Text.Json;
using OmpOS.Contracts.Serialization;
using OmpOS.Contracts.Ui;
using Shouldly;
using Xunit;

namespace OmpOS.Contracts.Tests.Ui;

/// <summary>
/// Verifies every <see cref="UiBlock"/> variant round-trips through
/// <see cref="OmpOsJsonContext"/> with its documented kind string,
/// and that unknown or missing kinds degrade to <see cref="UnknownBlock"/>.
/// </summary>
public sealed class UiBlockJsonTests
{
    public static TheoryData<string, UiBlock> AllBlocks => new()
    {
        { "markdown", new MarkdownBlock { Id = "b1", Markdown = "# Hello" } },
        { "code", new CodeBlock { Id = "b2", Language = "csharp", Code = "var x = 1;" } },
        { "tool_call", new ToolCallBlock { Id = "b3", CallId = "call-1", ToolName = "bash", Status = "completed", Arguments = JsonDocument.Parse("""{"cmd":"ls"}""").RootElement.Clone() } },
        { "permission", new PermissionBlock { Id = "b4", Prompt = "Allow?", Options = ["Allow", "Deny"] } },
        { "plan", new PlanBlock { Id = "b5", Steps = ["Step one", "Step two"] } },
        { "diff", new DiffPlaceholderBlock { Id = "b6", Path = "src/a.cs", Reason = "Too large" } },
        { "a2ui-surface", new A2uiSurfaceBlock { Id = "b8", SurfaceId = "s1", Payload = JsonDocument.Parse("""{"catalog":"core"}""").RootElement.Clone() } },
        { "error", new ErrorBlock { Id = "b7", Message = "boom", Retryable = true } },
        { "unknown", new UnknownBlock { Id = "b9", OriginalKind = "unknown", Raw = JsonDocument.Parse("""{"kind":"unknown","id":"b9","note":"kept"}""").RootElement.Clone() } },
    };

    [Theory]
    [MemberData(nameof(AllBlocks))]
    public void EveryBlockKindRoundTripsWithKindFirst(string kind, UiBlock block)
    {
        var json = JsonSerializer.Serialize(block, OmpOsJsonContext.Default.UiBlock);

        json.ShouldStartWith($"{{\"kind\":\"{kind}\",");
        var back = JsonSerializer.Deserialize(json, OmpOsJsonContext.Default.UiBlock);
        back.ShouldBeOfType(block.GetType());
        JsonSerializer.Serialize(back, OmpOsJsonContext.Default.UiBlock).ShouldBe(json);
    }

    [Fact]
    public void MarkdownBlockDeserializesScalarFields()
    {
        const string json = """{"kind":"markdown","id":"b1","markdown":"# Hello"}""";

        var back = JsonSerializer.Deserialize(json, OmpOsJsonContext.Default.UiBlock).ShouldBeOfType<MarkdownBlock>();

        back.Id.ShouldBe("b1");
        back.Markdown.ShouldBe("# Hello");
    }

    [Fact]
    public void UnknownKindDeserializesToUnknownBlockAndReserializesByteIdentical()
    {
        const string json = """{"kind":"a2ui","id":"b8","surface":{"x":1},"extra":true}""";

        var back = JsonSerializer.Deserialize(json, OmpOsJsonContext.Default.UiBlock).ShouldBeOfType<UnknownBlock>();

        back.OriginalKind.ShouldBe("a2ui");
        JsonSerializer.Serialize(back, OmpOsJsonContext.Default.UiBlock).ShouldBe(json);
    }

    [Fact]
    public void MissingKindDeserializesToUnknownBlockWithNullOriginalKind()
    {
        const string json = """{"id":"b9","markdown":"# Hello"}""";

        var back = JsonSerializer.Deserialize(json, OmpOsJsonContext.Default.UiBlock).ShouldBeOfType<UnknownBlock>();

        back.OriginalKind.ShouldBeNull();
        back.Id.ShouldBe("b9");
    }

    [Fact]
    public void KnownKindMissingRequiredMemberThrows()
    {
        const string json = """{"kind":"code","id":"b1","language":"csharp"}""";

        Should.Throw<JsonException>(() => JsonSerializer.Deserialize(json, OmpOsJsonContext.Default.UiBlock));
    }

    [Theory]
    [InlineData("""[]""")]
    [InlineData("""[{"kind":"markdown"}]""")]
    [InlineData("\"not-an-object\"")]
    [InlineData("""7""")]
    [InlineData("""false""")]
    public void NonObjectRootDegradesToUnknown(string rootJson)
    {
        var back = JsonSerializer.Deserialize(rootJson, OmpOsJsonContext.Default.UiBlock);

        var block = back.ShouldBeOfType<UnknownBlock>();
        block.OriginalKind.ShouldBeNull();
        block.Id.ShouldBeEmpty();
        block.Raw.GetRawText().ShouldBe(rootJson);
    }

    [Fact]
    public void NullTopLevelBlockDeserializesToNullWithoutThrowing()
    {
        // STJ default null handling: the converter is not invoked for a null
        // token; the reference deserializes to null. Must not throw.
        JsonSerializer.Deserialize("null", OmpOsJsonContext.Default.UiBlock).ShouldBeNull();
    }
}
