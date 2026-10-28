using OmpOS.Presentation.SideChats;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests.SideChats;

/// <summary>Verifies fork and duplicate over the forker seam.</summary>
public sealed class ForkModelTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task DuplicateForksTheWholeChatAndAnnouncesTheNewSession()
    {
        var forker = new ScriptedSessionForker(canForkFromMessage: false);
        var model = new ForkModel("s1", forker);
        ForkedEventArgs? raised = null;
        model.Forked += (_, args) => raised = args;

        var result = await model.DuplicateAsync(Token).ConfigureAwait(true);

        result.ShouldNotBeNull().NewSessionId.ShouldBe("forked-1");
        forker.Requests.ShouldBe([new ForkRequest("s1", null)]);
        raised.ShouldNotBeNull().SourceSessionId.ShouldBe("s1");
        raised.Result.ShouldBe(result);
        model.StatusText.ShouldBe("Duplicated the chat as Copy of chat. The original chat is unchanged.");
        model.HasError.ShouldBeFalse();
        model.IsBusy.ShouldBeFalse();
    }

    [Fact]
    public async Task ForkingFromAMessageNeedsAnEngineThatCanAndAPositiveCount()
    {
        var capable = new ScriptedSessionForker(canForkFromMessage: true);
        var model = new ForkModel("s1", capable);

        (await model.ForkFromMessageAsync(0, Token).ConfigureAwait(true)).ShouldBeNull();
        model.StatusText.ShouldBe(ForkCopy.CountOutOfRange);
        (await model.ForkFromMessageAsync(3, Token).ConfigureAwait(true)).ShouldNotBeNull();

        capable.Requests.ShouldBe([new ForkRequest("s1", 3)]);
        model.StatusText.ShouldContain("first 3 messages", Case.Sensitive);

        var whole = new ScriptedSessionForker(canForkFromMessage: false);
        var other = new ForkModel("s1", whole);
        (await other.ForkFromMessageAsync(2, Token).ConfigureAwait(true)).ShouldBeNull();

        other.StatusText.ShouldBe(ForkCopy.WholeChatOnly);
        other.CanForkFromMessage.ShouldBeFalse();
        whole.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task AFailedForkIsASentenceThatSaysNothingWasChangedAndTheModelIsUsableAgain()
    {
        var forker = new ScriptedSessionForker(canForkFromMessage: true) { Failure = new InvalidOperationException("session is running") };
        var model = new ForkModel("s1", forker);
        var raised = false;
        model.Forked += (_, _) => raised = true;

        (await model.DuplicateAsync(Token).ConfigureAwait(true)).ShouldBeNull();

        model.HasError.ShouldBeTrue();
        model.StatusText.ShouldBe("The fork failed: session is running. Nothing was changed.");
        raised.ShouldBeFalse();
        forker.Failure = null;
        (await model.DuplicateAsync(Token).ConfigureAwait(true)).ShouldNotBeNull();
        model.HasError.ShouldBeFalse();
    }
}
