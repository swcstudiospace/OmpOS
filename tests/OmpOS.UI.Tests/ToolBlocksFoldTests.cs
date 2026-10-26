using System.Text.Json;
using OmpOS.Contracts.Serialization;
using OmpOS.Contracts.Streaming;
using OmpOS.Contracts.Ui;
using OmpOS.GenUi;
using OmpOS.Harness.Abstractions;
using OmpOS.Parity.Support;
using OmpOS.Sessions;
using Shouldly;
using Xunit;

namespace OmpOS.UI.Tests;

/// <summary>
/// The tool-result, file-edit and approval-decision rules of <see cref="UiFolder"/> (contract 1.1.0), and their equality
/// with <c>HarnessEventProjection</c> over the same recorded events.
/// </summary>
public sealed class ToolBlocksFoldTests
{
    private const string TextUpdate = """
        {"sessionUpdate":"tool_call_update","toolCallId":"call-1","status":"completed","content":[{"type":"content","content":{"type":"text","text":"3 files"}}]}
        """;

    private const string DiffUpdate = """
        {"sessionUpdate":"tool_call_update","toolCallId":"call-1","status":"completed","content":[{"type":"diff","path":"src/a.cs","oldText":"old","newText":"new"}]}
        """;

    private static readonly DateTimeOffset Stamp = new(2026, 10, 26, 9, 0, 0, TimeSpan.Zero);

    /// <summary>An update carrying output text appends a result block after the card, which keeps the call's name and the update's status.</summary>
    [Fact]
    public void ToolUpdateFoldsToAToolResultBlock()
    {
        var tree = Fold(
            new ToolCallEvent("call-1", "ls", "{}"),
            new ToolCallUpdateEvent("call-1", "completed", TextUpdate));

        tree.Blocks.Count.ShouldBe(2);
        tree.Blocks[0].ShouldBeOfType<ToolCallBlock>().Status.ShouldBe("completed");
        var result = tree.Blocks[1].ShouldBeOfType<ToolResultBlock>();
        result.Id.ShouldBe("call-1-result");
        result.CallId.ShouldBe("call-1");
        result.ToolName.ShouldBe("ls");
        result.Status.ShouldBe("completed");
        result.Output.ShouldBe("3 files");
    }

    /// <summary>An update carrying a diff entry appends a file-edit block with the path and both texts.</summary>
    [Fact]
    public void FileEditFoldsToAFileEditBlock()
    {
        var tree = Fold(
            new ToolCallEvent("call-1", "edit", "{}"),
            new ToolCallUpdateEvent("call-1", "completed", DiffUpdate));

        tree.Blocks.Count.ShouldBe(2);
        var edit = tree.Blocks[1].ShouldBeOfType<FileEditBlock>();
        edit.Id.ShouldBe("call-1-edit-0");
        edit.CallId.ShouldBe("call-1");
        edit.Path.ShouldBe("src/a.cs");
        edit.OldText.ShouldBe("old");
        edit.NewText.ShouldBe("new");
    }

    /// <summary>A later update of the same call replaces its result in place, so the transcript never shows two copies.</summary>
    [Fact]
    public void ALaterUpdateReplacesTheResultInPlace()
    {
        var tree = Fold(
            new ToolCallEvent("call-1", "ls", "{}"),
            new ToolCallUpdateEvent("call-1", "in_progress", TextUpdate.Replace("3 files", "1 file", StringComparison.Ordinal)),
            new ToolCallUpdateEvent("call-1", "completed", TextUpdate));

        tree.Blocks.Count.ShouldBe(2);
        var result = tree.Blocks[1].ShouldBeOfType<ToolResultBlock>();
        result.Output.ShouldBe("3 files");
        result.Status.ShouldBe("completed");
    }

    /// <summary>An update with neither output nor edits adds no block and keeps the streaming text.</summary>
    [Fact]
    public void AnUpdateWithoutContentAddsNoBlockAndKeepsTheStreamingText()
    {
        var tree = Fold(
            new ToolCallEvent("call-1", "ls", "{}"),
            new TextDeltaEvent("working"),
            new ToolCallUpdateEvent("call-1", "completed", "{}"));

        tree.Blocks.ShouldHaveSingleItem().ShouldBeOfType<ToolCallBlock>();
        tree.StreamingText.ShouldBe("working");
    }

    /// <summary>A result that arrives while text is streaming ends that text first, so the result follows it in order.</summary>
    [Fact]
    public void AResultFlushesTheStreamingTextBeforeIt()
    {
        var tree = Fold(
            new ToolCallEvent("call-1", "ls", "{}"),
            new TextDeltaEvent("listing"),
            new ToolCallUpdateEvent("call-1", "completed", TextUpdate));

        tree.StreamingText.ShouldBeEmpty();
        tree.Blocks.Select(static block => block.GetType()).ShouldBe([typeof(ToolCallBlock), typeof(MarkdownBlock), typeof(ToolResultBlock)]);
    }

    /// <summary>An update for a call with no card is kept as an unknown block and adds no result.</summary>
    [Fact]
    public void AnUpdateForAnUnknownCallStaysAnUnknownBlock()
    {
        var tree = Fold(new ToolCallUpdateEvent("ghost", "completed", TextUpdate));

        tree.Blocks.ShouldHaveSingleItem().ShouldBeOfType<UnknownBlock>().OriginalKind.ShouldBe("tool_call_update");
    }

    /// <summary>An approval event sets the decision on the permission block its request identifier addresses.</summary>
    [Theory]
    [InlineData("tool_approval_approved", ApprovalDecisions.Approved)]
    [InlineData("tool_approval_denied", ApprovalDecisions.Denied)]
    public void AnApprovalEventSetsTheDecisionOnItsPermissionBlock(string kind, string expected)
    {
        var tree = Fold(new PermissionRequestEvent("call-2", "bash", "{\"options\":[\"allow\",\"deny\"]}"));

        tree = UiFolder.Fold(tree, Decide(kind, "call-2"));

        tree.Blocks.ShouldHaveSingleItem().ShouldBeOfType<PermissionBlock>().Decision.ShouldBe(expected);
    }

    /// <summary>A run-scoped request identifier of the form runId:callId addresses the permission block of that call.</summary>
    [Fact]
    public void ARunScopedRequestIdentifierAddressesItsPermissionBlock()
    {
        var tree = Fold(new PermissionRequestEvent("call-2", "bash", "{}"));

        tree = UiFolder.Fold(tree, Decide("tool_approval_approved", "run-7:call-2"));

        tree.Blocks.ShouldHaveSingleItem().ShouldBeOfType<PermissionBlock>().Decision.ShouldBe(ApprovalDecisions.Approved);
    }

    /// <summary>A decision for a request no undecided permission block addresses stays an unknown block, and a decided block is not decided twice.</summary>
    [Fact]
    public void ADecisionWithNoPermissionBlockStaysAnUnknownBlock()
    {
        var tree = Fold(new PermissionRequestEvent("call-2", "bash", "{}"));
        tree = UiFolder.Fold(tree, Decide("tool_approval_denied", "call-2"));

        tree = UiFolder.Fold(tree, Decide("tool_approval_approved", "call-2"));

        tree.Blocks.Count.ShouldBe(2);
        tree.Blocks[0].ShouldBeOfType<PermissionBlock>().Decision.ShouldBe(ApprovalDecisions.Denied);
        tree.Blocks[1].ShouldBeOfType<UnknownBlock>().OriginalKind.ShouldBe("tool_approval_approved");
    }

    /// <summary>
    /// The live fold and the harness projection produce the same blocks, with the same identifiers, over one recorded
    /// script that covers results, replacement, edits (modified and created), decisions and the unmatched cases.
    /// </summary>
    [Fact]
    public void TheLiveFoldEqualsTheHarnessProjectionOverTheSameRecordedEvents()
    {
        var log = RecordedLog();

        Func<ProjectionState, StreamEvent, ProjectionState> project = HarnessEventProjection.Fold;
        var live = log.Aggregate(UiTree.Empty, UiFolder.Fold);
        var projected = log.Aggregate(ProjectionState.Empty, project);

        Snapshot(live.Blocks, live.StreamingText).ShouldBe(Snapshot(projected.Blocks, projected.StreamingText));
        live.Blocks.OfType<ToolResultBlock>().Select(static block => block.Id).ShouldBe(["call-1-result"]);
        live.Blocks.OfType<FileEditBlock>().Select(static block => block.Id).ShouldBe(["call-2-edit-0", "call-2-edit-1"]);
        live.Blocks.OfType<PermissionBlock>().Select(static block => block.Decision).ShouldBe([ApprovalDecisions.Approved, ApprovalDecisions.Denied]);
    }

    /// <summary>The recorded script folds to one result block, one edit block per diff entry, and decided permissions, with a replacement leaving no copies.</summary>
    [Fact]
    public void TheRecordedScriptFoldsWithoutDuplicates()
    {
        var tree = RecordedLog().Aggregate(UiTree.Empty, UiFolder.Fold);

        tree.Blocks.Select(static block => block.Id).Distinct(StringComparer.Ordinal).Count().ShouldBe(tree.Blocks.Count);
        tree.Blocks.OfType<FileEditBlock>().First().NewText.ShouldBe("third");
        tree.Blocks.OfType<FileEditBlock>().Last().OldText.ShouldBeNull();
    }

    private static List<StreamEvent> RecordedLog()
    {
        HarnessEvent[] script =
        [
            new TextDeltaEvent("Looking"),
            new ToolCallEvent("call-1", "ls", "{}"),
            new ToolCallUpdateEvent("call-1", "in_progress", TextUpdate.Replace("3 files", "1 file", StringComparison.Ordinal)),
            new ToolCallUpdateEvent("call-1", "completed", TextUpdate),
            new ToolCallEvent("call-2", "edit", "{}"),
            new ToolCallUpdateEvent("call-2", "in_progress", """{"content":[{"type":"diff","path":"src/a.cs","oldText":"old","newText":"second"}]}"""),
            new ToolCallUpdateEvent(
                "call-2",
                "completed",
                """{"content":[{"type":"diff","path":"src/a.cs","oldText":"old","newText":"third"},{"type":"diff","path":"src/new.cs","newText":"fresh"}]}"""),
            new PermissionRequestEvent("perm-1", "bash", """{"options":["allow","deny"]}"""),
            new PermissionRequestEvent("perm-2", "write", """{"options":["allow","deny"]}"""),
            new ToolCallUpdateEvent("call-9", "completed", TextUpdate),
            new ToolCallUpdateEvent("call-1", "completed", "not json"),
            new PlanEvent("1. read\n2. answer"),
            new TextDeltaEvent("tail"),
        ];
        var log = script
            .SelectMany((evt, index) => HarnessEventProjection.ToStreamEvents(index + 1, "s-1", "s-1/turn-1", Stamp, evt))
            .ToList();
        log.Insert(8, Decide("tool_approval_approved", "run-7:perm-1"));
        log.Add(Decide("tool_approval_denied", "perm-2"));
        log.Add(Decide("tool_approval_approved", "ghost"));
        return log;
    }

    private static StreamEvent Decide(string kind, string requestId) => ParityEvents.Create(
        99,
        ParityEvents.Unknown(kind, $$"""{"kind":"{{kind}}","requestId":"{{requestId}}"}"""));

    private static UiTree Fold(params HarnessEvent[] events) => events
        .SelectMany((evt, index) => HarnessEventProjection.ToStreamEvents(index + 1, "s-1", "s-1/turn-1", Stamp, evt))
        .Aggregate(UiTree.Empty, UiFolder.Fold);

    private static string Snapshot(IReadOnlyList<UiBlock> blocks, string streamingText) =>
        JsonSerializer.Serialize(blocks.ToArray(), OmpOsJsonContext.Default.UiBlockArray) + "|" + streamingText;
}
