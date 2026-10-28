using OmpOS.Presentation.Chats;
using OmpOS.Presentation.SideChats;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests.SideChats;

/// <summary>Verifies a side chat: its own messages, the hidden parent summary, closing on its own and the refusals.</summary>
public sealed class SideChatModelTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static (SideChatModel Model, ScriptedSideChatEngine Engine) Create(string summary = "User: hi")
    {
        var engine = new ScriptedSideChatEngine();
        return (new SideChatModel(new SideChat("parent", "parent/side-1", "Side chat 1"), summary, engine), engine);
    }

    [Fact]
    public async Task AQuestionIsPostedAtOnceAndTheAnswerAddedAndTheEngineSeesTheSummaryAndTheEarlierMessages()
    {
        var (model, engine) = Create("User: add a cache");

        (await model.AskAsync("  what does the cache key look like? ", Token).ConfigureAwait(true)).ShouldBeTrue();
        engine.Answer = "second answer";
        (await model.AskAsync("and its ttl?", Token).ConfigureAwait(true)).ShouldBeTrue();

        model.Chat.Messages.Select(message => (message.Author, message.Text)).ShouldBe(
            [(SideChatAuthor.User, "what does the cache key look like?"), (SideChatAuthor.Assistant, "an answer"), (SideChatAuthor.User, "and its ttl?"), (SideChatAuthor.Assistant, "second answer")]);
        engine.Requests[0].ParentSummary.ShouldBe("User: add a cache");
        engine.Requests[0].History.ShouldBeEmpty();
        engine.Requests[1].History.Count.ShouldBe(2);
        engine.Requests[1].Question.ShouldBe("and its ttl?");
        engine.Requests[1].ParentSessionId.ShouldBe("parent");
        model.Chat.Messages.ShouldAllBe(message => !message.Text.Contains("add a cache", StringComparison.Ordinal));
        model.IsAnswering.ShouldBeFalse();
    }

    [Fact]
    public async Task AFailedAnswerKeepsTheQuestionAndSaysSo()
    {
        var (model, engine) = Create();
        engine.Answer = null;

        (await model.AskAsync("why?", Token).ConfigureAwait(true)).ShouldBeFalse();

        model.Chat.Messages.ShouldHaveSingleItem().Author.ShouldBe(SideChatAuthor.User);
        model.HasError.ShouldBeTrue();
        model.StatusText.ShouldContain("engine offline", Case.Sensitive);
        model.StatusText.ShouldContain("ask again", Case.Sensitive);
        model.IsAnswering.ShouldBeFalse();
    }

    [Fact]
    public async Task ABlankQuestionAndAQuestionToAClosedChatArePostedNowhere()
    {
        var (model, engine) = Create();

        (await model.AskAsync("   ", Token).ConfigureAwait(true)).ShouldBeFalse();
        model.StatusText.ShouldBe(SideChatCopy.BlankQuestion);

        await model.CloseAsync(Token).ConfigureAwait(true);
        (await model.AskAsync("late", Token).ConfigureAwait(true)).ShouldBeFalse();

        model.StatusText.ShouldBe(SideChatCopy.ClosedRefusal);
        model.Chat.Messages.ShouldBeEmpty();
        engine.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ClosingEndsTheChildConversationOnceAndAFailureToCloseIsReported()
    {
        var (model, engine) = Create();

        await model.CloseAsync(Token).ConfigureAwait(true);
        await model.CloseAsync(Token).ConfigureAwait(true);

        model.IsClosed.ShouldBeTrue();
        engine.Closed.ShouldBe(["parent/side-1"]);
        model.StatusText.ShouldBe(SideChatCopy.Closed);

        var (other, failing) = Create();
        failing.CloseFails = true;
        await other.CloseAsync(Token).ConfigureAwait(true);

        other.IsClosed.ShouldBeTrue();
        other.HasError.ShouldBeTrue();
        other.StatusText.ShouldContain("child gone", Case.Sensitive);
    }

    [Fact]
    public async Task ASideChatNeverOpensAnotherAndKnowsItsParent()
    {
        var (model, _) = Create();
        var panel = new SideChatPanel("parent");
        var hub = new SideChatHub(panel, new ScriptedSideChatEngine(), () => []);

        model.CanOpenSideChat.ShouldBeFalse();
        hub.OpenFrom(model).ShouldBeNull();

        hub.StatusText.ShouldBe(SideChatCopy.NoNesting);
        model.ParentSessionId.ShouldBe("parent");
        (await hub.OpenAsync(string.Empty, Token).ConfigureAwait(true)).ShouldNotBeNull();
        panel.Chats.Count.ShouldBe(1);
    }
}
