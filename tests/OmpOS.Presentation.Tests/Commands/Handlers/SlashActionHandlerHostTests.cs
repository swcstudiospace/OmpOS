using OmpOS.Presentation.Commands;
using OmpOS.Presentation.Commands.Handlers;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests.Commands.Handlers;

/// <summary>Verifies that the handler host routes by action id, prefers handlers over the head's host and never throws into the palette.</summary>
public sealed class SlashActionHandlerHostTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static SlashActionRequest Request(string id, string typed = "") => new(id, string.Empty, typed);

    [Fact]
    public async Task AHandlerRunsItsActionAndTheHeadsHostRunsEverythingElse()
    {
        var handler = new RecordingSlashActionHandler(HandlerActionIds.ChatFork);
        var head = new FakeSlashActionHost(SlashActionIds.ChatNew, SlashActionIds.SideOpen);
        var host = new SlashActionHandlerHost([handler], head);

        var forked = await host.RunAsync(Request(HandlerActionIds.ChatFork, "3"), Token).ConfigureAwait(true);
        var fresh = await host.RunAsync(Request(SlashActionIds.ChatNew), Token).ConfigureAwait(true);

        forked.Message.ShouldBe("done");
        handler.Requests.ShouldHaveSingleItem().TypedText.ShouldBe("3");
        fresh.Message.ShouldBe("head ran chat.new");
        host.CanRun(HandlerActionIds.ChatFork).ShouldBeTrue();
        host.CanRun(SlashActionIds.ChatNew).ShouldBeTrue();
        host.CanRun("unknown.action").ShouldBeFalse();
    }

    [Fact]
    public async Task AHandlerWinsOverTheHeadForTheSameActionId()
    {
        var handler = new RecordingSlashActionHandler(SlashActionIds.SideOpen, SlashActionResult.Completed("handler"));
        var head = new FakeSlashActionHost(SlashActionIds.SideOpen);
        var host = new SlashActionHandlerHost([handler], head);

        var result = await host.RunAsync(Request(SlashActionIds.SideOpen, "why?"), Token).ConfigureAwait(true);

        result.Message.ShouldBe("handler");
        head.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task AHandlerThatCannotRunIsNotAvailableAndDoesNotFallBackToTheHead()
    {
        var handler = new RecordingSlashActionHandler(HandlerActionIds.ChatRewind) { Ready = false };
        var head = new FakeSlashActionHost(HandlerActionIds.ChatRewind);
        var host = new SlashActionHandlerHost([handler], head);

        var result = await host.RunAsync(Request(HandlerActionIds.ChatRewind), Token).ConfigureAwait(true);

        host.CanRun(HandlerActionIds.ChatRewind).ShouldBeFalse();
        result.Outcome.ShouldBe(SlashActionOutcome.NotAvailable);
        result.Message.ShouldBe(HandlerCopy.NotReady);
        handler.Requests.ShouldBeEmpty();
        head.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task AnExceptionFromAHandlerIsAFailedResultAndCancellationStillPropagates()
    {
        var handler = new RecordingSlashActionHandler(HandlerActionIds.AppLogs) { Failure = new IOException("disk gone") };
        var host = new SlashActionHandlerHost([handler]);

        var result = await host.RunAsync(Request(HandlerActionIds.AppLogs), Token).ConfigureAwait(true);

        result.Outcome.ShouldBe(SlashActionOutcome.Failed);
        result.Message.ShouldContain("disk gone", Case.Sensitive);

        handler.Failure = new OperationCanceledException();
        await Should.ThrowAsync<OperationCanceledException>(() => host.RunAsync(Request(HandlerActionIds.AppLogs), Token)).ConfigureAwait(true);
    }

    [Fact]
    public async Task WithoutAFallbackAnUnknownActionIsNotAvailableAndTwoHandlersForOneIdAreRefused()
    {
        var host = new SlashActionHandlerHost([]);

        var result = await host.RunAsync(Request("nothing"), Token).ConfigureAwait(true);

        result.Outcome.ShouldBe(SlashActionOutcome.NotAvailable);
        Should.Throw<ArgumentException>(() => new SlashActionHandlerHost([new RecordingSlashActionHandler("a"), new RecordingSlashActionHandler("a")]));
    }
}
