using Bunit;
using OmpOS.Contracts.Streaming;
using OmpOS.Contracts.Ui;
using OmpOS.GenUi;
using OmpOS.Presentation.State;
using OmpOS.UI.Components;
using Shouldly;
using Xunit;

namespace OmpOS.UI.Tests;

/// <summary>A <c>user_message</c> event folds to one user turn with the sent text, and the Razor transcript renders it as a user turn.</summary>
public sealed class UserMessagePayloadFoldTests : UiTestContext
{
    private const string SessionId = "s-1";
    private const string CorrelationId = "s-1/turn-1";
    private static readonly DateTimeOffset Stamp = new(2026, 10, 24, 9, 0, 0, TimeSpan.Zero);

    /// <summary>A user_message event folds to exactly one user message carrying the text, and the transcript renders it as a user turn.</summary>
    [Fact]
    public void UserMessagePayloadFoldsToOneUserTurn()
    {
        var tree = UiFolder.Fold(UiTree.Empty, Stream(new UserMessagePayload { Text = "Ship it" }));

        tree.Blocks.Single().ShouldBeOfType<UserMessageBlock>().Text.ShouldBe("Ship it");
        tree.Blocks.OfType<UnknownBlock>().ShouldBeEmpty();

        var cut = Render<SessionView>(parameters => parameters
            .Add(p => p.Tree, tree)
            .Add(p => p.ViewState, SessionViewState.Success));

        cut.Find("[data-role=\"user\"]").TextContent.ShouldContain("Ship it");
        cut.Markup.ShouldNotContain("Unsupported content");
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
