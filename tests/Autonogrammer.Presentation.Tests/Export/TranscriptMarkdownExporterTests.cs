using System.Text.Json;
using Autonogrammer.Contracts.Ui;
using Autonogrammer.GenUi;
using Autonogrammer.Presentation.Export;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests;

/// <summary>
/// Verifies the Markdown transcript exporter (ADE-20): messages in order, a heading per block, and the exact golden output.
/// </summary>
public sealed class TranscriptMarkdownExporterTests
{
    /// <summary>ADE-20: a transcript of every block type exports to the golden Markdown file, byte for byte.</summary>
    [Fact]
    public void Export_FixtureTranscript_MatchesGoldenFile()
    {
        var exported = TranscriptMarkdownExporter.Export(FixtureTranscript(), "Fixture run");

        exported.ShouldBe(ReadGolden());
    }

    /// <summary>ADE-20: the export has one level-two heading per block, in transcript order, plus the in-progress tail.</summary>
    [Fact]
    public void Export_FixtureTranscript_WritesOneHeadingPerBlockInOrder()
    {
        var exported = TranscriptMarkdownExporter.Export(FixtureTranscript(), "Fixture run");

        var headings = exported.Split('\n').Where(static line => line.StartsWith("## ", StringComparison.Ordinal)).ToArray();

        headings.ShouldBe(
        [
            "## User",
            "## Assistant",
            "## Code: csharp",
            "## Tool call: read",
            "## Permission request",
            "## Plan",
            "## Diff: src/Parser.cs",
            "## Interface: summary",
            "## Error",
            "## Unknown block: future.widget",
            "## Assistant (in progress)",
        ]);
    }

    /// <summary>ADE-20: a tool call's arguments and an approval's options are written as text, not dropped.</summary>
    [Fact]
    public void Export_ToolCallAndPermission_IncludeTheirText()
    {
        var transcript = new UiTree
        {
            Blocks =
            [
                new ToolCallBlock
                {
                    Id = "t1",
                    CallId = "call-9",
                    ToolName = "grep",
                    Status = "running",
                    Arguments = Json("{\"pattern\":\"Parse\"}"),
                },
                new PermissionBlock { Id = "p1", Prompt = "Run grep?", Options = ["Approve", "Deny"] },
            ],
        };

        var exported = TranscriptMarkdownExporter.Export(transcript, "Run");

        exported.ShouldContain("Status: running");
        exported.ShouldContain("Call ID: call-9");
        exported.ShouldContain("{\"pattern\":\"Parse\"}");
        exported.ShouldContain("Run grep?\n\n- Approve\n- Deny");
    }

    /// <summary>ADE-20: an empty transcript exports to its title alone.</summary>
    [Fact]
    public void Export_EmptyTranscript_WritesOnlyTheTitle()
    {
        TranscriptMarkdownExporter.Export(UiTree.Empty, "Empty chat").ShouldBe("# Empty chat\n");
    }

    /// <summary>ADE-20: a code fence is longer than any backtick run in the code, so the code cannot close it.</summary>
    [Fact]
    public void Export_CodeContainingBackticks_UsesALongerFence()
    {
        var transcript = new UiTree
        {
            Blocks = [new CodeBlock { Id = "c1", Language = "markdown", Code = "use ``` to fence" }],
        };

        var exported = TranscriptMarkdownExporter.Export(transcript, "Code");

        exported.ShouldContain("````markdown\nuse ``` to fence\n````\n");
    }

    /// <summary>ADE-20: a line break in the title cannot split the level-one heading.</summary>
    [Fact]
    public void Export_TitleWithLineBreak_KeepsOneHeadingLine()
    {
        var exported = TranscriptMarkdownExporter.Export(UiTree.Empty, "First\r\nSecond");

        exported.ShouldBe("# First Second\n");
    }

    /// <summary>ADE-20: an unknown block keeps its raw JSON so no transcript content is lost.</summary>
    [Fact]
    public void Export_UnknownBlock_KeepsItsRawJson()
    {
        var transcript = new UiTree
        {
            Blocks = [new UnknownBlock { Id = "u1", OriginalKind = "future.widget", Raw = Json("{\"size\":3}") }],
        };

        TranscriptMarkdownExporter.Export(transcript, "Unknown").ShouldContain("```json\n{\"size\":3}\n```");
    }

    /// <summary>ADE-20: a null transcript is rejected.</summary>
    [Fact]
    public void Export_NullTranscript_Throws()
    {
        Should.Throw<ArgumentNullException>(() => TranscriptMarkdownExporter.Export(null!, "Run"));
    }

    /// <summary>ADE-20: a blank title is rejected, because the document needs a heading.</summary>
    [Fact]
    public void Export_BlankTitle_Throws()
    {
        Should.Throw<ArgumentException>(() => TranscriptMarkdownExporter.Export(UiTree.Empty, "  "));
    }

    private static UiTree FixtureTranscript() => new()
    {
        Blocks =
        [
            new UserMessageBlock { Id = "b1", Text = "Summarise the parser changes." },
            new MarkdownBlock
            {
                Id = "b2",
                Markdown = "The parser now **recognises** magic keywords.\n\n- Adds `PromptParser` tests\n- Keeps the old path",
            },
            new CodeBlock { Id = "b3", Language = "csharp", Code = "Console.WriteLine(\"parsed\");" },
            new ToolCallBlock
            {
                Id = "b4",
                CallId = "call-1",
                ToolName = "read",
                Status = "completed",
                Arguments = Json("{\"path\":\"src/Parser.cs\"}"),
            },
            new PermissionBlock
            {
                Id = "b5",
                Prompt = "Allow write to src/Parser.cs?",
                Options = ["Allow once", "Deny"],
            },
            new PlanBlock { Id = "b6", Steps = ["Read the parser", "Write the exporter"] },
            new DiffPlaceholderBlock { Id = "b7", Path = "src/Parser.cs", Reason = "Diff preview is not available yet." },
            new A2uiSurfaceBlock
            {
                Id = "b8",
                SurfaceId = "summary",
                Payload = Json(
                    """
                    {"components":[
                      {"type":"heading","text":"Parser summary"},
                      {"type":"text","text":"Two recognisers were added."},
                      {"type":"list","items":["one","two"]},
                      {"type":"button","label":"Open"},
                      {"type":"chart"}
                    ]}
                    """),
            },
            new ErrorBlock { Id = "b9", Message = "Provider timed out.", Retryable = true },
            new UnknownBlock { Id = "b10", OriginalKind = "future.widget", Raw = Json("{\"kind\":\"future.widget\"}") },
        ],
        StreamingText = "Still writing the summary",
    };

    private static JsonElement Json(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static string ReadGolden()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Autonogrammer.slnx")))
        {
            directory = directory.Parent;
        }

        var root = directory?.FullName ?? throw new InvalidOperationException("Autonogrammer.slnx was not found above " + AppContext.BaseDirectory);
        return File.ReadAllText(Path.Combine(root, "tests", "Autonogrammer.Presentation.Tests", "Export", "Golden", "transcript.golden.md"));
    }
}
