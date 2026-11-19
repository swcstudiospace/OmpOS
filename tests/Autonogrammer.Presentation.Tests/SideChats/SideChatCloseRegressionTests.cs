using Autonogrammer.Presentation.Chats;
using Autonogrammer.Presentation.SideChats;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.SideChats;

/// <summary>Verifies that closing a side chat removes it from the hub's open list and from the panel's list together, and that the panel's selection never points at a removed side chat.</summary>
public sealed class SideChatCloseRegressionTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ClosingASideChatRemovesItFromBothListsAndKeepsTheOthers()
    {
        var panel = new SideChatPanel("parent");
        var hub = new SideChatHub(panel, new ScriptedSideChatEngine(), () => []);
        var first = await hub.OpenAsync(string.Empty, Token).ConfigureAwait(true);
        var second = await hub.OpenAsync(string.Empty, Token).ConfigureAwait(true);

        await hub.CloseAsync(first, Token).ConfigureAwait(true);

        hub.Open.ShouldHaveSingleItem().ShouldBeSameAs(second);
        panel.Chats.ShouldHaveSingleItem().ShouldBeSameAs(second.Chat);
    }

    [Fact]
    public async Task ClosingTheSelectedSideChatSelectsTheNextOneThenTheLastRemainingOne()
    {
        var panel = new SideChatPanel("parent");
        var hub = new SideChatHub(panel, new ScriptedSideChatEngine(), () => []);
        var first = await hub.OpenAsync(string.Empty, Token).ConfigureAwait(true);
        var second = await hub.OpenAsync(string.Empty, Token).ConfigureAwait(true);
        var third = await hub.OpenAsync(string.Empty, Token).ConfigureAwait(true);
        panel.Select(first.Chat);

        await hub.CloseAsync(first, Token).ConfigureAwait(true);
        panel.Selected.ShouldBeSameAs(second.Chat);

        await hub.CloseAsync(third, Token).ConfigureAwait(true);
        panel.Selected.ShouldBeSameAs(second.Chat);
        panel.Chats.ShouldHaveSingleItem().ShouldBeSameAs(second.Chat);

        await hub.CloseAsync(second, Token).ConfigureAwait(true);
        panel.Selected.ShouldBeNull();
        panel.Chats.ShouldBeEmpty();
        hub.Open.ShouldBeEmpty();
    }

    [Fact]
    public async Task ACancelledCloseStillRemovesTheSideChatFromBothLists()
    {
        var panel = new SideChatPanel("parent");
        var hub = new SideChatHub(panel, new CancellingSideChatEngine(), () => []);
        var chat = await hub.OpenAsync(string.Empty, Token).ConfigureAwait(true);

        await Should.ThrowAsync<OperationCanceledException>(() => hub.CloseAsync(chat, Token)).ConfigureAwait(true);

        chat.IsClosed.ShouldBeTrue();
        hub.Open.ShouldBeEmpty();
        panel.Chats.ShouldBeEmpty();
        panel.Selected.ShouldBeNull();
    }

    [Fact]
    public void RemovingAnUnknownSideChatChangesNothing()
    {
        var panel = new SideChatPanel("parent");
        var kept = panel.Open();
        var stranger = new SideChat("parent", "parent/side-9", "Side chat 9");

        panel.Remove(stranger).ShouldBeFalse();

        panel.Chats.ShouldHaveSingleItem().ShouldBeSameAs(kept);
        panel.Selected.ShouldBeSameAs(kept);
    }

    /// <summary>A side chat engine whose close is cancelled, as a cancelled token would make it.</summary>
    private sealed class CancellingSideChatEngine : ISideChatEngine
    {
        /// <inheritdoc/>
        public Task<string> AskAsync(SideChatRequest request, CancellationToken cancellationToken) => Task.FromResult("an answer");

        /// <inheritdoc/>
        public Task CloseAsync(string chatId, CancellationToken cancellationToken) => Task.FromCanceled(new CancellationToken(canceled: true));
    }
}
