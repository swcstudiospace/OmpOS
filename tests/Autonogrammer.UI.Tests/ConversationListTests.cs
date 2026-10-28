using Bunit;
using Autonogrammer.Presentation.Components;
using Autonogrammer.UI.Components;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests;

/// <summary>The conversation list renders titles, marks the selection, and reports picks (UI-01).</summary>
public sealed class ConversationListTests : BunitContext
{
    /// <summary>Conversations render as titled buttons with the selection marked.</summary>
    [Fact]
    public void RendersTitlesWithSelection()
    {
        var cut = Render<ConversationList>(parameters => parameters
            .Add(p => p.Conversations, new List<ConversationSummary>
            {
                new() { Id = "s-1", Title = "First" },
                new() { Id = "s-2", Title = "Second" },
            })
            .Add(p => p.SelectedId, "s-2"));

        var items = cut.FindAll(".autonogrammer-conversation-item");
        items.Select(static item => item.QuerySelector(".autonogrammer-conversation-title")!.TextContent).ShouldBe(["First", "Second"]);
        cut.Find("button[aria-current=\"true\"]").TextContent.Trim().ShouldBe("Second");
    }

    /// <summary>Activating a conversation reports its identifier.</summary>
    [Fact]
    public void SelectingReportsIdentifier()
    {
        string? picked = null;
        var cut = Render<ConversationList>(parameters => parameters
            .Add(p => p.Conversations, new List<ConversationSummary> { new() { Id = "s-1", Title = "First" } })
            .Add(p => p.OnSelected, (string id) => picked = id));

        cut.Find(".autonogrammer-conversation-item").Click();

        picked.ShouldBe("s-1");
    }

    /// <summary>An empty list renders the empty prompt, not a blank nav.</summary>
    [Fact]
    public void EmptyListRendersEmptyPrompt()
    {
        var cut = Render<ConversationList>(parameters => parameters
            .Add(p => p.Conversations, new List<ConversationSummary>()));

        cut.Find(".autonogrammer-empty").ShouldNotBeNull();
        cut.FindAll("button").Count.ShouldBe(0);
    }
}
