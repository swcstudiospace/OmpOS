using System.Text.Json;
using OmpOS.Contracts.Serialization;
using OmpOS.Contracts.Streaming;
using OmpOS.Contracts.Ui;
using OmpOS.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace OmpOS.Sessions.Tests;

/// <summary>
/// The tool-result, file-edit and approval-decision rules of <see cref="HarnessEventProjection"/> (contract 1.1.0).
/// </summary>
public sealed class ToolResultProjectionTests
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
    public void ToolUpdateProjectsToAToolResultBlock()
    {
        var state = Fold(
            new ToolCallEvent("call-1", "ls", "{}"),
            new ToolCallUpdateEvent("call-1", "completed", TextUpdate));

        state.Blocks.Count.ShouldBe(2);
        state.Blocks[0].ShouldBeOfType<ToolCallBlock>().Status.ShouldBe("completed");
        var result = state.Blocks[1].ShouldBeOfType<ToolResultBlock>();
        result.CallId.ShouldBe("call-1");
        result.ToolName.ShouldBe("ls");
        result.Status.ShouldBe("completed");
        result.Output.ShouldBe("3 files");
    }

    /// <summary>An update carrying a diff entry appends a file-edit block with the path and both texts.</summary>
    [Fact]
    public void FileEditProjectsToAFileEditBlock()
    {
        var state = Fold(
            new ToolCallEvent("call-1", "edit", "{}"),
            new ToolCallUpdateEvent("call-1", "completed", DiffUpdate));

        state.Blocks.Count.ShouldBe(2);
        var edit = state.Blocks[1].ShouldBeOfType<FileEditBlock>();
        edit.CallId.ShouldBe("call-1");
        edit.Path.ShouldBe("src/a.cs");
        edit.OldText.ShouldBe("old");
        edit.NewText.ShouldBe("new");
    }

    /// <summary>A later update of the same call replaces its result in place, so the transcript never shows two copies.</summary>
    [Fact]
    public void ALaterUpdateReplacesTheResultInPlace()
    {
        var state = Fold(
            new ToolCallEvent("call-1", "ls", "{}"),
            new ToolCallUpdateEvent("call-1", "in_progress", TextUpdate.Replace("3 files", "1 file", StringComparison.Ordinal)),
            new ToolCallUpdateEvent("call-1", "completed", TextUpdate));

        state.Blocks.Count.ShouldBe(2);
        var result = state.Blocks[1].ShouldBeOfType<ToolResultBlock>();
        result.Output.ShouldBe("3 files");
        result.Status.ShouldBe("completed");
    }

    /// <summary>An update with no output and no edits changes only the card, and does not end the streaming text.</summary>
    [Fact]
    public void AnUpdateWithoutContentAddsNoBlockAndKeepsTheStreamingText()
    {
        var state = Fold(
            new ToolCallEvent("call-1", "ls", "{}"),
            new TextDeltaEvent("working"),
            new ToolCallUpdateEvent("call-1", "completed", "{}"));

        state.Blocks.ShouldHaveSingleItem().ShouldBeOfType<ToolCallBlock>();
        state.StreamingText.ShouldBe("working");
    }

    /// <summary>A result that arrives while text is streaming ends that text first, so the result follows it in order.</summary>
    [Fact]
    public void AResultFlushesTheStreamingTextBeforeIt()
    {
        var state = Fold(
            new ToolCallEvent("call-1", "ls", "{}"),
            new TextDeltaEvent("listing"),
            new ToolCallUpdateEvent("call-1", "completed", TextUpdate));

        state.StreamingText.ShouldBeEmpty();
        state.Blocks.Select(static block => block.GetType()).ShouldBe([typeof(ToolCallBlock), typeof(MarkdownBlock), typeof(ToolResultBlock)]);
    }

    /// <summary>An update for a call with no card is still kept as an unknown block and adds no result.</summary>
    [Fact]
    public void AnUpdateForAnUnknownCallStaysAnUnknownBlock()
    {
        var state = Fold(new ToolCallUpdateEvent("ghost", "completed", TextUpdate));

        state.Blocks.ShouldHaveSingleItem().ShouldBeOfType<UnknownBlock>().OriginalKind.ShouldBe("tool_call_update");
    }

    /// <summary>Replaying the serialized stream-event log reproduces the result and edit blocks exactly.</summary>
    [Fact]
    public void ReplayingTheSerializedLogReproducesTheResultAndEditBlocks()
    {
        HarnessEvent[] script =
        [
            new ToolCallEvent("call-1", "edit", "{}"),
            new ToolCallUpdateEvent("call-1", "completed", DiffUpdate),
            new ToolCallUpdateEvent("call-1", "completed", TextUpdate),
        ];
        var live = script.Aggregate(ProjectionState.Empty, HarnessEventProjection.Fold);
        var log = script.SelectMany((evt, index) => HarnessEventProjection.ToStreamEvents(index + 1, "s-1", "s-1/turn-1", Stamp, evt)).ToArray();

        var json = JsonSerializer.Serialize(log, OmpOsJsonContext.Default.StreamEventArray);
        var replayed = JsonSerializer.Deserialize(json, OmpOsJsonContext.Default.StreamEventArray)!
            .Aggregate(ProjectionState.Empty, HarnessEventProjection.Fold);

        Serialize(replayed).ShouldBe(Serialize(live));
        live.Blocks.Select(static block => block.GetType()).ShouldBe([typeof(ToolCallBlock), typeof(FileEditBlock), typeof(ToolResultBlock)]);
    }

    /// <summary>An approval event sets the decision on the permission block its request identifier addresses.</summary>
    [Theory]
    [InlineData("tool_approval_approved", ApprovalDecisions.Approved)]
    [InlineData("tool_approval_denied", ApprovalDecisions.Denied)]
    public void AnApprovalEventSetsTheDecisionOnItsPermissionBlock(string kind, string expected)
    {
        var request = new PermissionRequestEvent("call-2", "bash", "{\"options\":[\"allow\",\"deny\"]}");
        var decided = Decide(kind, "call-2");

        var state = Fold(request);
        state = HarnessEventProjection.Fold(state, decided);

        state.Blocks.ShouldHaveSingleItem().ShouldBeOfType<PermissionBlock>().Decision.ShouldBe(expected);
    }

    /// <summary>A run-scoped request identifier of the form runId:callId addresses the permission block of that call.</summary>
    [Fact]
    public void ARunScopedRequestIdentifierAddressesItsPermissionBlock()
    {
        var state = Fold(new PermissionRequestEvent("call-2", "bash", "{}"));

        state = HarnessEventProjection.Fold(state, Decide("tool_approval_approved", "run-7:call-2"));

        state.Blocks.ShouldHaveSingleItem().ShouldBeOfType<PermissionBlock>().Decision.ShouldBe(ApprovalDecisions.Approved);
    }

    /// <summary>A decision for a request no permission block addresses stays an unknown block, and a decided block is not decided twice.</summary>
    [Fact]
    public void ADecisionWithNoPermissionBlockStaysAnUnknownBlock()
    {
        var state = Fold(new PermissionRequestEvent("call-2", "bash", "{}"));
        state = HarnessEventProjection.Fold(state, Decide("tool_approval_denied", "call-2"));

        state = HarnessEventProjection.Fold(state, Decide("tool_approval_approved", "call-2"));

        state.Blocks.Count.ShouldBe(2);
        state.Blocks[0].ShouldBeOfType<PermissionBlock>().Decision.ShouldBe(ApprovalDecisions.Denied);
        state.Blocks[1].ShouldBeOfType<UnknownBlock>().OriginalKind.ShouldBe("tool_approval_approved");
    }

    private static StreamEvent Decide(string kind, string requestId) => new()
    {
        Seq = 99,
        SessionId = "s-1",
        CorrelationId = "s-1/turn-1",
        Timestamp = Stamp,
        Payload = new UnknownPayload
        {
            OriginalKind = kind,
            Raw = HarnessEventProjection.BuildRaw(kind, ("requestId", requestId), ("approved", kind.EndsWith("approved", StringComparison.Ordinal) ? "true" : "false")),
        },
    };

    private static ProjectionState Fold(params HarnessEvent[] events) =>
        events.Aggregate(ProjectionState.Empty, HarnessEventProjection.Fold);

    private static string Serialize(ProjectionState state) =>
        JsonSerializer.Serialize(state.Blocks.ToArray(), OmpOsJsonContext.Default.UiBlockArray) + "|" + state.StreamingText;
}
