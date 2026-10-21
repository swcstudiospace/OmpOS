using Bunit;
using OmpOS.Presentation.Components;
using OmpOS.UI.Components;
using Shouldly;
using Xunit;

namespace OmpOS.UI.Tests;

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

        var items = cut.FindAll(".ompos-conversation-item");
        items.Select(static item => item.TextContent).ShouldBe(["First", "Second"]);
        cut.Find("button[aria-current=\"true\"]").TextContent.ShouldBe("Second");
    }

    /// <summary>Activating a conversation reports its identifier.</summary>
    [Fact]
    public void SelectingReportsIdentifier()
    {
        string? picked = null;
        var cut = Render<ConversationList>(parameters => parameters
            .Add(p => p.Conversations, new List<ConversationSummary> { new() { Id = "s-1", Title = "First" } })
            .Add(p => p.OnSelected, (string id) => picked = id));

        cut.Find(".ompos-conversation-item").Click();

        picked.ShouldBe("s-1");
    }

    /// <summary>An empty list renders the empty prompt, not a blank nav.</summary>
    [Fact]
    public void EmptyListRendersEmptyPrompt()
    {
        var cut = Render<ConversationList>(parameters => parameters
            .Add(p => p.Conversations, new List<ConversationSummary>()));

        cut.Find(".ompos-empty").ShouldNotBeNull();
        cut.FindAll("button").Count.ShouldBe(0);
    }
}
