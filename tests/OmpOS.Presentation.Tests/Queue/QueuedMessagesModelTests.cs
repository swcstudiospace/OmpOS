using OmpOS.Presentation.Queue;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests.Queue;

/// <summary>Verifies the order, edit, remove, reorder and send-now rules of the message queue, and that a message is never lost.</summary>
public sealed class QueuedMessagesModelTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static QueuedMessagesModel Queue(params string[] texts)
    {
        var queue = new QueuedMessagesModel("s1", TimeProvider.System);
        foreach (var text in texts)
        {
            queue.Enqueue(text).ShouldBe(QueueOutcome.Queued);
        }

        return queue;
    }

    private static string[] Texts(QueuedMessagesModel queue) => [.. queue.Messages.Select(message => message.Text)];

    [Fact]
    public void MessagesKeepTheirSendOrderWithPlacesFromOneAndBlankTextIsIgnored()
    {
        var queue = Queue("first", "second", "third");

        queue.Enqueue("   ").ShouldBe(QueueOutcome.Ignored);

        Texts(queue).ShouldBe(["first", "second", "third"]);
        queue.Messages.Select(message => message.Position).ShouldBe([1, 2, 3]);
        queue.Messages.Select(message => message.CanMoveUp).ShouldBe([false, true, true]);
        queue.Messages.Select(message => message.CanMoveDown).ShouldBe([true, true, false]);
        queue.PillText.ShouldBe("3 queued");
        queue.IsEmpty.ShouldBeFalse();
        queue.Messages[1].RowName.ShouldBe("Queued message 2: second");
        queue.Messages[1].SendNowName.ShouldBe("Send queued message 2 now");
    }

    [Fact]
    public void AnEmptyQueueSaysSoAndHasNoPill()
    {
        var queue = Queue();

        queue.IsEmpty.ShouldBeTrue();
        queue.PillText.ShouldBeEmpty();
        queue.EmptyText.ShouldBe(QueueCopy.Empty);
    }

    [Fact]
    public void TheQueueRefusesTheMessageAfterTheCapAndSaysSo()
    {
        var queue = Queue();
        for (var index = 0; index < OmpOS.Presentation.Runs.RunInputScheduler.MaxQueuedMessages; index++)
        {
            queue.Enqueue("m" + index).ShouldBe(QueueOutcome.Queued);
        }

        queue.IsFull.ShouldBeTrue();
        queue.Enqueue("one too many").ShouldBe(QueueOutcome.Full);

        queue.Count.ShouldBe(OmpOS.Presentation.Runs.RunInputScheduler.MaxQueuedMessages);
        queue.Announcement.ShouldContain("queue is full", Case.Sensitive);
    }

    [Fact]
    public void MoveUpMoveDownAndMoveToReorderAndAnnounce()
    {
        var queue = Queue("a", "b", "c", "d");
        var c = queue.Messages[2].Id;

        queue.MoveUp(c).ShouldBeTrue();
        Texts(queue).ShouldBe(["a", "c", "b", "d"]);
        queue.Announcement.ShouldBe("Moved message 3 to position 2 of 4.");

        queue.MoveTo(c, 1).ShouldBeTrue();
        Texts(queue).ShouldBe(["c", "a", "b", "d"]);
        queue.MoveTo(c, 99).ShouldBeTrue();
        Texts(queue).ShouldBe(["a", "b", "d", "c"]);
        queue.MoveDown(c).ShouldBeFalse();
        queue.MoveUp(queue.Messages[0].Id).ShouldBeFalse();
        queue.MoveTo(c, 4).ShouldBeFalse();
        queue.MoveUp("missing").ShouldBeFalse();
        queue.Messages.Select(message => message.Position).ShouldBe([1, 2, 3, 4]);
    }

    [Fact]
    public void EditChangesTheTextInPlaceAndRefusesBlankOrMissing()
    {
        var queue = Queue("a", "b");
        var b = queue.Messages[1].Id;

        queue.Edit(b, "b, but better").ShouldBeTrue();
        queue.Edit(b, "  ").ShouldBeFalse();
        queue.Edit("missing", "x").ShouldBeFalse();

        Texts(queue).ShouldBe(["a", "b, but better"]);
        queue.Messages[1].Position.ShouldBe(2);
        queue.Messages[1].RowName.ShouldBe("Queued message 2: b, but better");
    }

    [Fact]
    public void RemoveAndClearDropMessagesAndRenumberTheRest()
    {
        var queue = Queue("a", "b", "c");

        queue.Remove(queue.Messages[0].Id).ShouldBeTrue();
        queue.Remove("missing").ShouldBeFalse();

        Texts(queue).ShouldBe(["b", "c"]);
        queue.Messages[0].Position.ShouldBe(1);
        queue.Clear().ShouldBe(2);
        queue.IsEmpty.ShouldBeTrue();
        queue.Announcement.ShouldBe(QueueCopy.Cleared);
        queue.Clear().ShouldBe(0);
    }

    [Fact]
    public void TakeNextRunsTheFirstMessageAndPutBackReturnsItToTheFront()
    {
        var queue = Queue("a", "b");

        var next = queue.TakeNext().ShouldNotBeNull();
        next.Text.ShouldBe("a");
        Texts(queue).ShouldBe(["b"]);
        queue.PutBack(next);
        queue.PutBack(next);

        Texts(queue).ShouldBe(["a", "b"]);
        Queue().TakeNext().ShouldBeNull();
    }

    [Fact]
    public async Task SendNowRemovesTheMessageOnlyWhenItWasDelivered()
    {
        var queue = Queue("a", "b", "c");
        var sent = new List<string>();
        var answer = QueueSendOutcome.Sent;
        queue.Sender = (text, _) =>
        {
            sent.Add(text);
            return Task.FromResult(answer);
        };
        var b = queue.Messages[1].Id;

        answer = QueueSendOutcome.Failed;
        (await queue.SendNowAsync(b, Token).ConfigureAwait(true)).ShouldBe(QueueSendOutcome.Failed);
        Texts(queue).ShouldBe(["a", "b", "c"]);
        queue.Announcement.ShouldContain("stays in the queue", Case.Sensitive);

        answer = QueueSendOutcome.NoActiveRun;
        (await queue.SendNowAsync(b, Token).ConfigureAwait(true)).ShouldBe(QueueSendOutcome.NoActiveRun);
        Texts(queue).ShouldBe(["a", "b", "c"]);
        queue.Announcement.ShouldBe(QueueCopy.NoRun);

        answer = QueueSendOutcome.Sent;
        (await queue.SendNowAsync(b, Token).ConfigureAwait(true)).ShouldBe(QueueSendOutcome.Sent);
        Texts(queue).ShouldBe(["a", "c"]);
        sent.ShouldBe(["b", "b", "b"]);
        queue.Announcement.ShouldBe("Sent queued message 2 to the agent now.");
        (await queue.SendNowAsync(b, Token).ConfigureAwait(true)).ShouldBe(QueueSendOutcome.NotFound);
    }

    [Fact]
    public async Task ASenderThatThrowsKeepsTheMessageAndAQueueWithoutASenderCannotSendNow()
    {
        var queue = Queue("a");
        var id = queue.Messages[0].Id;

        (await queue.SendNowAsync(id, Token).ConfigureAwait(true)).ShouldBe(QueueSendOutcome.Failed);

        queue.Sender = (_, _) => throw new InvalidOperationException("channel closed");
        (await queue.SendNowAsync(id, Token).ConfigureAwait(true)).ShouldBe(QueueSendOutcome.Failed);

        queue.Announcement.ShouldContain("channel closed", Case.Sensitive);
        Texts(queue).ShouldBe(["a"]);
        queue.SendNowCommand.CanExecute(id).ShouldBeTrue();
    }

    [Fact]
    public async Task TheRowCommandsTakeTheMessageIdAsTheirParameter()
    {
        var queue = Queue("a", "b");
        var b = queue.Messages[1].Id;

        queue.MoveUpCommand.CanExecute(null).ShouldBeFalse();
        await queue.MoveUpCommand.ExecuteAsync(b).ConfigureAwait(true);
        await queue.RemoveCommand.ExecuteAsync(queue.Messages[1].Id).ConfigureAwait(true);

        Texts(queue).ShouldBe(["b"]);
        await queue.ClearCommand.ExecuteAsync(null).ConfigureAwait(true);
        queue.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void LongMultiLineMessagesShowOneShortLineAndKeepTheirFullText()
    {
        var text = "line one\nline two " + new string('x', 200);
        var queue = Queue(text);

        queue.Messages[0].PreviewText.Length.ShouldBe(QueuedMessage.PreviewChars + 1);
        queue.Messages[0].PreviewText.ShouldStartWith("line one line two", Case.Sensitive);
        queue.Messages[0].Text.ShouldBe(text);
    }

    [Fact]
    public void TheRegistryKeepsOneQueuePerSessionUntilItIsRemoved()
    {
        var registry = new QueuedMessagesRegistry(TimeProvider.System);

        var first = registry.GetOrCreate("s1");
        first.Enqueue("kept");

        registry.GetOrCreate("s1").ShouldBeSameAs(first);
        registry.GetOrCreate("s2").ShouldNotBeSameAs(first);
        registry.Count.ShouldBe(2);
        registry.Remove("s1").ShouldBeTrue();
        registry.GetOrCreate("s1").IsEmpty.ShouldBeTrue();
        registry.Remove("never").ShouldBeFalse();
    }
}
