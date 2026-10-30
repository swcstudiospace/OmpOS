using System.Text.Json;
using Autonogrammer.Contracts.Streaming;
using Autonogrammer.Contracts.Ui;
using Autonogrammer.GenUi;
using Autonogrammer.Presentation.State;
using Autonogrammer.UI.Components;
using Bunit;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests;

/// <summary>The engine's context use is session state for a meter: a <c>context_usage</c> event folds to no block and never reads as unsupported content.</summary>
public sealed class ContextUsageFoldTests : UiTestContext
{
    private static readonly DateTimeOffset Stamp = new(2026, 10, 29, 9, 30, 0, TimeSpan.Zero);

    /// <summary>The event leaves the tree as it was, between a streamed answer and its completion.</summary>
    [Fact]
    public void AContextUsageEventAddsNoBlock()
    {
        var tree = UiFolder.Fold(UiTree.Empty, Event(1, new TextDeltaPayload { Delta = "done" }));
        tree = UiFolder.Fold(tree, Event(2, ContextUsage()));
        tree = UiFolder.Fold(tree, Event(3, new CompletedPayload { Reason = "end_turn" }));

        tree.Blocks.Select(static block => block.GetType()).ShouldBe([typeof(MarkdownBlock)]);
    }

    /// <summary>The transcript shows the answer and no "Unsupported content" disclosure after it.</summary>
    [Fact]
    public void TheTranscriptShowsNoUnsupportedContentForIt()
    {
        var tree = UiFolder.Fold(UiTree.Empty, Event(1, new TextDeltaPayload { Delta = "done" }));
        tree = UiFolder.Fold(tree, Event(2, ContextUsage()));
        tree = UiFolder.Fold(tree, Event(3, new CompletedPayload { Reason = "end_turn" }));

        var cut = Render<SessionView>(parameters => parameters
            .Add(p => p.Tree, tree)
            .Add(p => p.ViewState, SessionViewState.Success));

        cut.Markup.ShouldNotContain("Unsupported content");
        cut.Markup.ShouldNotContain("context_usage");
    }

    private static StreamEvent Event(long seq, StreamEventPayload payload) => new()
    {
        Seq = seq,
        SessionId = "s-1",
        CorrelationId = "s-1/turn-1",
        Timestamp = Stamp,
        Payload = payload,
    };

    private static UnknownPayload ContextUsage()
    {
        using var document = JsonDocument.Parse("""{"kind":"context_usage","used":"1234","size":"200000"}""");
        return new UnknownPayload { OriginalKind = "context_usage", Raw = document.RootElement.Clone() };
    }
}
