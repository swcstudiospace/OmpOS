using System.Text.Json;
using System.Text.Json.Nodes;
using Autonogrammer.Contracts.Streaming;
using Autonogrammer.Contracts.Ui;
using Autonogrammer.GenUi;
using Autonogrammer.Presentation.State;
using Autonogrammer.UI.Components;
using Bunit;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests;

/// <summary>A <c>prompt_accepted</c> event folds to exactly one user message with the prompt text (UI-01, UI-04).</summary>
public sealed class PromptAcceptedFoldTests : UiTestContext
{
    private const string SessionId = "s-1";
    private const string CorrelationId = "s-1/turn-1";
    private static readonly DateTimeOffset Stamp = new(2026, 10, 20, 9, 30, 0, TimeSpan.Zero);

    /// <summary>A prompt acceptance yields exactly one user message carrying the prompt text and zero unknown blocks.</summary>
    [Fact]
    public void PromptAcceptedYieldsExactlyOneUserMessage()
    {
        var tree = UiFolder.Fold(UiTree.Empty, PromptAccepted("Do the thing"));

        var user = tree.Blocks.Single().ShouldBeOfType<UserMessageBlock>();
        user.Text.ShouldBe("Do the thing");
        tree.Blocks.OfType<UnknownBlock>().ShouldBeEmpty();
    }

    /// <summary>The user message renders as a user turn with no unknown-block fallback text.</summary>
    [Fact]
    public void PromptAcceptedRendersUserTurn()
    {
        var tree = UiFolder.Fold(UiTree.Empty, PromptAccepted("Do the thing"));

        var cut = Render<SessionView>(parameters => parameters
            .Add(p => p.Tree, tree)
            .Add(p => p.ViewState, SessionViewState.Success));

        cut.Find("[data-role=\"user\"]").TextContent.ShouldContain("Do the thing");
        cut.Markup.ShouldNotContain("Unsupported content");
    }

    /// <summary>The user message lands ahead of the assistant output of the same turn.</summary>
    [Fact]
    public void UserMessageStaysBeforeAssistantOutput()
    {
        var tree = UiTree.Empty;
        tree = UiFolder.Fold(tree, PromptAccepted("Do the thing"));
        tree = UiFolder.Fold(tree, Stream(new TextDeltaPayload { Delta = "on it" }));
        tree = UiFolder.Fold(tree, Stream(new CompletedPayload { Reason = "end_turn" }));

        tree.Blocks.Count.ShouldBe(2);
        tree.Blocks[0].ShouldBeOfType<UserMessageBlock>().Text.ShouldBe("Do the thing");
        tree.Blocks[1].ShouldBeOfType<MarkdownBlock>().Markdown.ShouldBe("on it");
    }

    /// <summary>Two turns keep event order: user, assistant, user, assistant.</summary>
    [Fact]
    public void TwoTurnsKeepEventOrder()
    {
        var tree = UiTree.Empty;
        tree = UiFolder.Fold(tree, PromptAccepted("First"));
        tree = UiFolder.Fold(tree, Stream(new TextDeltaPayload { Delta = "one" }));
        tree = UiFolder.Fold(tree, Stream(new CompletedPayload { Reason = "end_turn" }));
        tree = UiFolder.Fold(tree, PromptAccepted("Second"));
        tree = UiFolder.Fold(tree, Stream(new TextDeltaPayload { Delta = "two" }));
        tree = UiFolder.Fold(tree, Stream(new CompletedPayload { Reason = "end_turn" }));

        tree.Blocks.Select(static block => block switch
        {
            UserMessageBlock user => $"user:{user.Text}",
            MarkdownBlock markdown => $"assistant:{markdown.Markdown}",
            _ => block.GetType().Name,
        }).ShouldBe(["user:First", "assistant:one", "user:Second", "assistant:two"]);
    }

    /// <summary>Folding the same log twice yields the same block sequence: the fold stays pure.</summary>
    [Fact]
    public void PromptAcceptedFoldIsPure()
    {
        var first = UiFolder.Fold(UiFolder.Fold(UiTree.Empty, PromptAccepted("Do the thing")), Stream(new TextDeltaPayload { Delta = "on it" }));
        var second = UiFolder.Fold(UiFolder.Fold(UiTree.Empty, PromptAccepted("Do the thing")), Stream(new TextDeltaPayload { Delta = "on it" }));

        second.Blocks.Count.ShouldBe(first.Blocks.Count);
        second.Blocks.Zip(first.Blocks).All(static pair => pair.First.Equals(pair.Second)).ShouldBeTrue();
    }

    /// <summary>A prompt acceptance without a prompt member still yields a user message, never an unknown block.</summary>
    [Fact]
    public void MalformedPromptAcceptedStillYieldsUserMessage()
    {
        using var doc = JsonDocument.Parse("{\"kind\":\"prompt_accepted\"}");
        var evt = Stream(new UnknownPayload { OriginalKind = "prompt_accepted", Raw = doc.RootElement.Clone() });

        var tree = UiFolder.Fold(UiTree.Empty, evt);

        tree.Blocks.Single().ShouldBeOfType<UserMessageBlock>().Text.ShouldBeEmpty();
        tree.Blocks.OfType<UnknownBlock>().ShouldBeEmpty();
    }

    private static StreamEvent PromptAccepted(string prompt)
    {
        var obj = new JsonObject
        {
            ["kind"] = "prompt_accepted",
            ["prompt"] = prompt,
            ["intentKind"] = "plain",
        };
        using var doc = JsonDocument.Parse(obj.ToJsonString());
        return Stream(new UnknownPayload { OriginalKind = "prompt_accepted", Raw = doc.RootElement.Clone() });
    }

    private static StreamEvent Stream(StreamEventPayload payload) => new()
    {
        Seq = 1,
        SessionId = SessionId,
        CorrelationId = CorrelationId,
        Timestamp = Stamp,
        Payload = payload,
    };
}
