using Autonogrammer.Contracts.Ui;
using Autonogrammer.GenUi;
using Autonogrammer.Presentation.Export;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests;

/// <summary>
/// Verifies the Markdown export of the approval decision, tool results and file edits (contract 1.1.0).
/// </summary>
public sealed class ToolBlockExportTests
{
    /// <summary>An approval's decision is written after its options, and an undecided request writes none.</summary>
    [Fact]
    public void ExportIncludesTheApprovalDecision()
    {
        var transcript = new UiTree
        {
            Blocks =
            [
                new PermissionBlock { Id = "p1", Prompt = "Run grep?", Options = ["Approve", "Deny"], Decision = ApprovalDecisions.Approved },
                new PermissionBlock { Id = "p2", Prompt = "Run rm?", Options = ["Approve", "Deny"], Decision = ApprovalDecisions.Denied },
                new PermissionBlock { Id = "p3", Prompt = "Run ls?", Options = ["Approve", "Deny"] },
            ],
        };

        var exported = TranscriptMarkdownExporter.Export(transcript, "Run");

        exported.ShouldContain("Run grep?\n\n- Approve\n- Deny\n\nDecision: approved\n");
        exported.ShouldContain("Run rm?\n\n- Approve\n- Deny\n\nDecision: denied\n");
        exported.ShouldContain("Run ls?\n\n- Approve\n- Deny\n");
        exported.Split("Decision:").Length.ShouldBe(3, "only the two decided requests write a decision");
    }

    /// <summary>A decision with a line break cannot split the line it is written on.</summary>
    [Fact]
    public void ADecisionIsWrittenOnOneLine()
    {
        var transcript = new UiTree
        {
            Blocks = [new PermissionBlock { Id = "p1", Prompt = "Run?", Options = [], Decision = "late\n## Injected" }],
        };

        var exported = TranscriptMarkdownExporter.Export(transcript, "Run");

        exported.ShouldContain("Decision: late ## Injected\n");
        exported.Split('\n').Count(static line => line.StartsWith("## ", StringComparison.Ordinal)).ShouldBe(1);
    }

    /// <summary>A tool result is exported under its tool's name with its status, call and output in a text fence.</summary>
    [Fact]
    public void ExportIncludesTheToolResult()
    {
        var transcript = new UiTree
        {
            Blocks = [new ToolResultBlock { Id = "r1", CallId = "call-1", ToolName = "ls", Status = "completed", Output = "a.txt\r\nb.txt\n" }],
        };

        var exported = TranscriptMarkdownExporter.Export(transcript, "Run");

        exported.ShouldBe("# Run\n\n## Tool result: ls\n\nStatus: completed\n\nCall ID: call-1\n\n```text\na.txt\nb.txt\n```\n");
    }

    /// <summary>A file edit exports its before and after text, and a created file exports its new text alone.</summary>
    [Fact]
    public void ExportIncludesTheFileEdits()
    {
        var transcript = new UiTree
        {
            Blocks =
            [
                new FileEditBlock { Id = "e1", CallId = "call-1", Path = "src/a.cs", OldText = "old", NewText = "new" },
                new FileEditBlock { Id = "e2", CallId = "call-2", Path = "src/b.cs", NewText = "created" },
            ],
        };

        var exported = TranscriptMarkdownExporter.Export(transcript, "Run");

        exported.ShouldContain("## File edit: src/a.cs\n\nCall ID: call-1\n\nBefore:\n\n```\nold\n```\n\nAfter:\n\n```\nnew\n```\n");
        exported.ShouldContain("## File edit: src/b.cs\n\nCall ID: call-2\n\nCreated file:\n\n```\ncreated\n```\n");
    }
}
