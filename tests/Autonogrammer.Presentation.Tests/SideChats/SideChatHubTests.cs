using Autonogrammer.Contracts.Ui;
using Autonogrammer.Presentation.Chats;
using Autonogrammer.Presentation.SideChats;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.SideChats;

/// <summary>Verifies <c>/side</c> and <c>/btw</c>: the summary is taken at open time, each side chat closes alone and the parent transcript is never written.</summary>
public sealed class SideChatHubTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task OpeningWithATypedQuestionAsksItAndSeedsTheChatWithTheParentAsItIsNow()
    {
        var blocks = new List<UiBlock> { new UserMessageBlock { Id = "u1", Text = "refactor the parser" } };
        var engine = new ScriptedSideChatEngine();
        var panel = new SideChatPanel("parent");
        var hub = new SideChatHub(panel, engine, () => blocks);

        var chat = await hub.OpenAsync("what is a token?", Token).ConfigureAwait(true);
        blocks.Add(new UserMessageBlock { Id = "u2", Text = "later message" });

        engine.Requests.ShouldHaveSingleItem().ParentSummary.ShouldBe("User: refactor the parser");
        chat.ParentSummary.ShouldBe("User: refactor the parser");
        chat.Chat.Messages.Count.ShouldBe(2);
        panel.Selected.ShouldBeSameAs(chat.Chat);
        hub.Open.ShouldHaveSingleItem().ShouldBeSameAs(chat);
        blocks.Count.ShouldBe(2);
    }

    [Fact]
    public async Task OpeningWithoutAQuestionAsksNothing()
    {
        var engine = new ScriptedSideChatEngine();
        var hub = new SideChatHub(new SideChatPanel("parent"), engine, () => []);

        var chat = await hub.OpenAsync("  ", Token).ConfigureAwait(true);

        engine.Requests.ShouldBeEmpty();
        chat.Chat.Messages.ShouldBeEmpty();
    }

    [Fact]
    public async Task EachSideChatClosesAloneAndTheOthersKeepWorking()
    {
        var engine = new ScriptedSideChatEngine();
        var hub = new SideChatHub(new SideChatPanel("parent"), engine, () => []);
        var first = await hub.OpenAsync("one", Token).ConfigureAwait(true);
        var second = await hub.OpenAsync("two", Token).ConfigureAwait(true);

        await hub.CloseAsync(first, Token).ConfigureAwait(true);

        hub.Open.ShouldHaveSingleItem().ShouldBeSameAs(second);
        first.IsClosed.ShouldBeTrue();
        second.IsClosed.ShouldBeFalse();
        engine.Closed.ShouldBe([first.Id]);
        (await second.AskAsync("still there?", Token).ConfigureAwait(true)).ShouldBeTrue();
        first.Id.ShouldNotBe(second.Id, StringComparer.Ordinal);
    }
}
