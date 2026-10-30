using Autonogrammer.Presentation.Components;
using Autonogrammer.Presentation.State;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests;

/// <summary>
/// The session list model (UI-SPEC discretion 13): it starts loading, shows what it loads or why it failed, and
/// announces each change so a bound list can redraw while it is shown.
/// </summary>
public sealed class ConversationListModelTests
{
    private static readonly ConversationSummary First = new() { Id = "s-1", Title = "First" };
    private static readonly ConversationSummary Second = new() { Id = "s-2", Title = "Second" };

    /// <summary>A new list is loading and has no conversations.</summary>
    [Fact]
    public void NewListIsLoadingWithNoConversations()
    {
        var model = new ConversationListModel();

        model.State.ShouldBe(ConversationListState.Loading);
        model.Conversations.ShouldBeEmpty();
    }

    /// <summary>Loading conversations shows them in the order given and announces both the state and the rows.</summary>
    [Fact]
    public void LoadShowsConversationsInOrderAndAnnouncesBothChanges()
    {
        var model = new ConversationListModel();
        var changed = PropertyChangeRecorder.Record(model);

        model.Load([First, Second]);

        model.State.ShouldBe(ConversationListState.Loaded);
        model.Conversations.Select(conversation => conversation.Id).ShouldBe(["s-1", "s-2"]);
        changed.ShouldAnnounce(nameof(ConversationListModel.State));
        changed.ShouldAnnounce(nameof(ConversationListModel.Conversations));
    }

    /// <summary>Loading no conversations is a loaded list with nothing in it, which the view shows as the empty prompt.</summary>
    [Fact]
    public void LoadingNoConversationsIsLoadedAndEmpty()
    {
        var model = new ConversationListModel();

        model.Load([]);

        model.State.ShouldBe(ConversationListState.Loaded);
        model.Conversations.ShouldBeEmpty();
    }

    /// <summary>A later load replaces the conversations shown before it.</summary>
    [Fact]
    public void LoadReplacesTheConversationsShownBefore()
    {
        var model = new ConversationListModel();
        model.Load([First]);

        model.Load([Second]);

        model.Conversations.Select(conversation => conversation.Id).ShouldBe(["s-2"]);
    }

    /// <summary>A failed load clears the conversations and announces the failed state.</summary>
    [Fact]
    public void FailClearsTheConversationsAndAnnouncesTheFailure()
    {
        var model = new ConversationListModel();
        model.Load([First]);
        var changed = PropertyChangeRecorder.Record(model);

        model.Fail();

        model.State.ShouldBe(ConversationListState.Failed);
        model.Conversations.ShouldBeEmpty();
        changed.ShouldAnnounce(nameof(ConversationListModel.State));
        changed.ShouldAnnounce(nameof(ConversationListModel.Conversations));
    }

    /// <summary>A list that failed loads again once the conversations are available.</summary>
    [Fact]
    public void LoadAfterFailureShowsTheConversations()
    {
        var model = new ConversationListModel();
        model.Fail();

        model.Load([First]);

        model.State.ShouldBe(ConversationListState.Loaded);
        model.Conversations.Select(conversation => conversation.Id).ShouldBe(["s-1"]);
    }

    /// <summary>A null list is rejected, and the shown state stays as it was.</summary>
    [Fact]
    public void LoadRejectsNullAndKeepsTheShownState()
    {
        var model = new ConversationListModel();
        model.Load([First]);

        Should.Throw<ArgumentNullException>(() => model.Load(null!));

        model.State.ShouldBe(ConversationListState.Loaded);
        model.Conversations.Select(conversation => conversation.Id).ShouldBe(["s-1"]);
    }
}
