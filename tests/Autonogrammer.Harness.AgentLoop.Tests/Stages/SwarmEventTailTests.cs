using System.Text;
using Autonogrammer.Harness.AgentLoop.Stages;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.AgentLoop.Tests.Stages;

/// <summary>Verifies the incremental reader of the append-only event log: offsets, partial lines, caps and a shrinking file.</summary>
public sealed class SwarmEventTailTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string[] Text(TailPoll poll) => [.. poll.Lines.Select(static line => Encoding.UTF8.GetString(line.Span))];

    [Fact]
    public async Task MissingFileYieldsNoLinesAndReportsMissing()
    {
        var tail = new SwarmEventTail();

        var poll = await tail.PollAsync(Path.Combine(Path.GetTempPath(), "autonogrammer-missing-" + Guid.NewGuid().ToString("N")), Token).ConfigureAwait(true);

        poll.Status.ShouldBe(FileReadStatus.Missing);
        poll.Lines.ShouldBeEmpty();
        tail.Offset.ShouldBe(0);
    }

    [Fact]
    public async Task OnlyNewCompleteLinesAreReturnedEachPoll()
    {
        using var directory = new StageTempDirectory();
        var scope = directory.Scope();
        var path = StageTempDirectory.EventsPath(scope);
        StageTempDirectory.WriteEvents(scope, StageFixtures.Utf8("one\ntwo\n"));
        var tail = new SwarmEventTail();

        Text(await tail.PollAsync(path, Token).ConfigureAwait(true)).ShouldBe(["one", "two"]);
        Text(await tail.PollAsync(path, Token).ConfigureAwait(true)).ShouldBeEmpty();

        StageTempDirectory.AppendEvents(scope, StageFixtures.Utf8("three\r\n\nfour\n"));
        Text(await tail.PollAsync(path, Token).ConfigureAwait(true)).ShouldBe(["three", "four"]);
    }

    [Fact]
    public async Task TrailingPartialLineIsIgnoredUntilItsTerminatorArrives()
    {
        using var directory = new StageTempDirectory();
        var scope = directory.Scope();
        var path = StageTempDirectory.EventsPath(scope);
        StageTempDirectory.WriteEvents(scope, StageFixtures.Utf8("complete\npart"));
        var tail = new SwarmEventTail();

        Text(await tail.PollAsync(path, Token).ConfigureAwait(true)).ShouldBe(["complete"]);
        tail.Offset.ShouldBe("complete\n".Length);
        Text(await tail.PollAsync(path, Token).ConfigureAwait(true)).ShouldBeEmpty();

        StageTempDirectory.AppendEvents(scope, StageFixtures.Utf8("ial\n"));
        Text(await tail.PollAsync(path, Token).ConfigureAwait(true)).ShouldBe(["partial"]);
    }

    [Fact]
    public async Task FileThatShrankIsReadAgainFromTheStartInTheSamePoll()
    {
        using var directory = new StageTempDirectory();
        var scope = directory.Scope();
        var path = StageTempDirectory.EventsPath(scope);
        StageTempDirectory.WriteEvents(scope, StageFixtures.Utf8("first-generation-line-1\nfirst-generation-line-2\n"));
        var tail = new SwarmEventTail();
        await tail.PollAsync(path, Token).ConfigureAwait(true);

        StageTempDirectory.WriteEvents(scope, StageFixtures.Utf8("fresh\n"));

        Text(await tail.PollAsync(path, Token).ConfigureAwait(true)).ShouldBe(["fresh"]);
        tail.Offset.ShouldBe("fresh\n".Length);
    }

    [Fact]
    public async Task LineOverTheCapIsSkippedAndTheLinesAroundItSurvive()
    {
        using var directory = new StageTempDirectory();
        var scope = directory.Scope();
        var path = StageTempDirectory.EventsPath(scope);
        var huge = new string('h', SwarmEventTail.MaxLineBytes + 1);
        StageTempDirectory.WriteEvents(scope, StageFixtures.Utf8("before\n" + huge + "\nafter\n"));
        var tail = new SwarmEventTail();

        Text(await tail.PollAsync(path, Token).ConfigureAwait(true)).ShouldBe(["before", "after"]);
    }

    [Fact]
    public async Task LineExactlyAtTheCapIsPassedOn()
    {
        using var directory = new StageTempDirectory();
        var scope = directory.Scope();
        var path = StageTempDirectory.EventsPath(scope);
        var exact = new string('e', SwarmEventTail.MaxLineBytes);
        StageTempDirectory.WriteEvents(scope, StageFixtures.Utf8(exact + "\n"));
        var tail = new SwarmEventTail();

        var lines = Text(await tail.PollAsync(path, Token).ConfigureAwait(true));

        lines.ShouldHaveSingleItem().Length.ShouldBe(SwarmEventTail.MaxLineBytes);
    }

    [Fact]
    public async Task EndlessLineWithoutATerminatorIsDiscardedAcrossPollsUntilItEnds()
    {
        using var directory = new StageTempDirectory();
        var scope = directory.Scope();
        var path = StageTempDirectory.EventsPath(scope);
        StageTempDirectory.WriteEvents(scope, StageFixtures.Utf8("ok\n" + new string('x', SwarmEventTail.MaxLineBytes + 10)));
        var tail = new SwarmEventTail();

        Text(await tail.PollAsync(path, Token).ConfigureAwait(true)).ShouldBe(["ok"]);

        StageTempDirectory.AppendEvents(scope, StageFixtures.Utf8(new string('y', 50)));
        Text(await tail.PollAsync(path, Token).ConfigureAwait(true)).ShouldBeEmpty();

        StageTempDirectory.AppendEvents(scope, StageFixtures.Utf8("rest-of-the-huge-line\nnext\n"));
        Text(await tail.PollAsync(path, Token).ConfigureAwait(true)).ShouldBe(["next"]);
    }

    [Fact]
    public async Task AtMostOneChunkIsReadPerPollAndNoLineIsLostOrRepeated()
    {
        using var directory = new StageTempDirectory();
        var scope = directory.Scope();
        var path = StageTempDirectory.EventsPath(scope);
        StageTempDirectory.WriteEvents(scope, StageFixtures.Utf8("seed\n"));
        var tail = new SwarmEventTail();
        await tail.PollAsync(path, Token).ConfigureAwait(true);

        var builder = new StringBuilder();
        const int lineCount = 20_000;
        for (var i = 0; i < lineCount; i++)
        {
            builder.Append("line-").Append(i).Append('-').Append('p', 40).Append('\n');
        }

        StageTempDirectory.AppendEvents(scope, StageFixtures.Utf8(builder.ToString()));
        var received = new List<string>();
        var polls = 0;
        while (received.Count < lineCount && polls < 100)
        {
            var before = tail.Offset;
            var poll = await tail.PollAsync(path, Token).ConfigureAwait(true);
            (tail.Offset - before).ShouldBeLessThanOrEqualTo(SwarmEventTail.MaxChunkBytes);
            received.AddRange(Text(poll));
            polls++;
        }

        polls.ShouldBeGreaterThan(1);
        received.Count.ShouldBe(lineCount);
        received.Select((line, index) => line.StartsWith("line-" + index + "-", StringComparison.Ordinal)).ShouldAllBe(static ok => ok);
        tail.Offset.ShouldBe(new FileInfo(path).Length);
    }

    [Fact]
    public async Task FirstPollOfAHugeLogStartsOneChunkBeforeTheEndAndSkipsTheCutLine()
    {
        using var directory = new StageTempDirectory();
        var scope = directory.Scope();
        var path = StageTempDirectory.EventsPath(scope);
        var builder = new StringBuilder();
        for (var i = 0; i < 30_000; i++)
        {
            builder.Append("line-").Append(i).Append('-').Append('p', 40).Append('\n');
        }

        StageTempDirectory.WriteEvents(scope, StageFixtures.Utf8(builder.ToString()));
        var length = new FileInfo(path).Length;
        length.ShouldBeGreaterThan(SwarmEventTail.MaxChunkBytes * 2L);
        var tail = new SwarmEventTail();

        var lines = Text(await tail.PollAsync(path, Token).ConfigureAwait(true));

        lines.ShouldNotBeEmpty();
        lines[^1].ShouldStartWith("line-29999-");
        lines.ShouldAllBe(static line => line.StartsWith("line-", StringComparison.Ordinal) && line.EndsWith(new string('p', 40), StringComparison.Ordinal));
        lines.Length.ShouldBeLessThan(30_000);
        tail.Offset.ShouldBe(length);
    }
}
