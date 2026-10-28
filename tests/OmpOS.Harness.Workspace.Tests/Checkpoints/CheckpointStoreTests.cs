using System.Runtime.Versioning;
using System.Text;
using OmpOS.Workspace.Checkpoints;
using Shouldly;
using Xunit;

namespace OmpOS.Workspace.Tests.Checkpoints;

/// <summary>Verifies what the checkpoint store keeps for a turn, what a rewind writes, and the cases in which it refuses to write.</summary>
public sealed class CheckpointStoreTests : IDisposable
{
    private const string Session = "session-1";

    private readonly CheckpointFixture _fixture = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _fixture.Dispose();

    private CheckpointStore Store(CheckpointOptions? options = null, TimeProvider? clock = null) =>
        new(_fixture.StatePath, [_fixture.WorkspacePath], options, clock);

    private static CheckpointFileChange Change(string path, string? before, string after) => CheckpointFileChange.FromText(path, before, after);

    [Fact]
    public async Task ARecordedTurnListsItsFilesWithHashesAndAFlagForWhatCanBeRestored()
    {
        using var store = Store();
        _fixture.Write("a.txt", "new\n");
        _fixture.Write("b.txt", "created\n");

        var turn = await store.RecordTurnAsync(Session, "u1", "Fix a and add b", [Change("a.txt", "old\n", "new\n"), Change("b.txt", null, "created\n")], Token).ConfigureAwait(true);

        turn.ShouldNotBeNull();
        turn.TurnId.ShouldBe(1);
        turn.Label.ShouldBe("Fix a and add b");
        turn.Files.Select(file => file.Path).ShouldBe(["a.txt", "b.txt"]);
        turn.Files[0].BeforeHash.ShouldNotBeNull().ShouldNotBe(turn.Files[0].AfterHash, StringComparer.Ordinal);
        turn.Files[0].Restorable.ShouldBeTrue();
        turn.Files[1].BeforeHash.ShouldBeNull();
        turn.Files[1].Restorable.ShouldBeTrue();
        (await store.ListAsync(Session, Token).ConfigureAwait(true)).ShouldHaveSingleItem().TurnKey.ShouldBe("u1");
    }

    [Fact]
    public async Task RecordingTheSameTurnKeyAgainReplacesItsFilesAndKeepsItsNumber()
    {
        using var store = Store();
        _fixture.Write("a.txt", "v2\n");

        await store.RecordTurnAsync(Session, "u1", "first", [Change("a.txt", "v0\n", "v1\n")], Token).ConfigureAwait(true);
        var again = await store.RecordTurnAsync(Session, "u1", string.Empty, [Change("a.txt", "v0\n", "v2\n"), Change("c.txt", null, "c\n")], Token).ConfigureAwait(true);

        again.ShouldNotBeNull().TurnId.ShouldBe(1);
        again.Label.ShouldBe("first");
        again.Files.Count.ShouldBe(2);
        (await store.ListAsync(Session, Token).ConfigureAwait(true)).Count.ShouldBe(1);
    }

    [Fact]
    public async Task ATurnWithNoNetChangeOrOnlyFilesOutsideTheWorkspaceRecordsNothing()
    {
        using var store = Store();
        var outside = Path.Combine(_fixture.BasePath, "outside.txt");

        var unchanged = await store.RecordTurnAsync(Session, "u1", null, [Change("a.txt", "same\n", "same\n")], Token).ConfigureAwait(true);
        var elsewhere = await store.RecordTurnAsync(Session, "u2", null, [Change(outside, "x", "y"), Change("../outside.txt", "x", "y")], Token).ConfigureAwait(true);

        unchanged.ShouldBeNull();
        elsewhere.ShouldBeNull();
        (await store.ListAsync(Session, Token).ConfigureAwait(true)).ShouldBeEmpty();
    }

    [Fact]
    public async Task APathThatLeavesTheWorkspaceThroughASymbolicLinkIsNotRecorded()
    {
        using var store = Store();
        var outside = Path.Combine(_fixture.BasePath, "elsewhere");
        Directory.CreateDirectory(outside);
        try
        {
            Directory.CreateSymbolicLink(Path.Combine(_fixture.WorkspacePath, "link"), outside);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Assert.Skip("This host cannot create symbolic links.");
        }

        var turn = await store.RecordTurnAsync(Session, "u1", null, [Change("link/secret.txt", "a", "b")], Token).ConfigureAwait(true);

        turn.ShouldBeNull();
    }

    [Fact]
    public async Task RewindRestoresAnEditedFileAndDeletesACreatedOne()
    {
        using var store = Store();
        _fixture.Write("a.txt", "new\n");
        _fixture.Write("sub/b.txt", "created\n");
        await store.RecordTurnAsync(Session, "u1", null, [Change("a.txt", "old\n", "new\n"), Change("sub/b.txt", null, "created\n")], Token).ConfigureAwait(true);

        var result = await store.RewindAsync(Session, 1, force: false, Token).ConfigureAwait(true);

        result.Status.ShouldBe(RewindStatus.Completed);
        result.Written.ShouldBe(["a.txt", "sub/b.txt"]);
        _fixture.Read("a.txt").ShouldBe("old\n");
        _fixture.Exists("sub/b.txt").ShouldBeFalse();
        (await store.ListAsync(Session, Token).ConfigureAwait(true)).ShouldBeEmpty();
    }

    [Fact]
    public async Task RewindingToAnEarlierTurnUndoesEveryLaterTurnToo()
    {
        using var store = Store();
        _fixture.Write("a.txt", "v2\n");
        await store.RecordTurnAsync(Session, "u1", null, [Change("a.txt", "v0\n", "v1\n")], Token).ConfigureAwait(true);
        await store.RecordTurnAsync(Session, "u2", null, [Change("a.txt", "v1\n", "v2\n")], Token).ConfigureAwait(true);

        var plan = await store.PlanRewindAsync(Session, 2, Token).ConfigureAwait(true);
        var second = await store.RewindAsync(Session, 2, force: false, Token).ConfigureAwait(true);

        plan.Files.ShouldHaveSingleItem().Action.ShouldBe(RewindFileAction.Restore);
        second.Status.ShouldBe(RewindStatus.Completed);
        _fixture.Read("a.txt").ShouldBe("v1\n");

        var first = await store.RewindAsync(Session, 1, force: false, Token).ConfigureAwait(true);

        first.Status.ShouldBe(RewindStatus.Completed);
        _fixture.Read("a.txt").ShouldBe("v0\n");
    }

    [Fact]
    public async Task RewindingTurnOneAfterTwoTurnsTouchedTheFileGoesBackToTheStateBeforeTurnOne()
    {
        using var store = Store();
        _fixture.Write("a.txt", "v2\n");
        await store.RecordTurnAsync(Session, "u1", null, [Change("a.txt", "v0\n", "v1\n")], Token).ConfigureAwait(true);
        await store.RecordTurnAsync(Session, "u2", null, [Change("a.txt", "v1\n", "v2\n")], Token).ConfigureAwait(true);

        var result = await store.RewindAsync(Session, 1, force: false, Token).ConfigureAwait(true);

        result.Status.ShouldBe(RewindStatus.Completed);
        _fixture.Read("a.txt").ShouldBe("v0\n");
        (await store.ListAsync(Session, Token).ConfigureAwait(true)).ShouldBeEmpty();
    }

    [Fact]
    public async Task AFileThatChangedSinceTheAgentsEditBlocksTheRewindUntilItIsForced()
    {
        using var store = Store();
        _fixture.Write("a.txt", "agent\n");
        _fixture.Write("b.txt", "agent b\n");
        await store.RecordTurnAsync(Session, "u1", null, [Change("a.txt", "old\n", "agent\n"), Change("b.txt", "old b\n", "agent b\n")], Token).ConfigureAwait(true);
        _fixture.Write("a.txt", "the user typed this\n");

        var blocked = await store.RewindAsync(Session, 1, force: false, Token).ConfigureAwait(true);

        blocked.Status.ShouldBe(RewindStatus.Blocked);
        blocked.Plan.StaleCount.ShouldBe(1);
        blocked.Plan.Files.Single(file => string.Equals(file.Path, "a.txt", StringComparison.Ordinal)).Action.ShouldBe(RewindFileAction.Stale);
        _fixture.Read("a.txt").ShouldBe("the user typed this\n");
        _fixture.Read("b.txt").ShouldBe("agent b\n");
        (await store.ListAsync(Session, Token).ConfigureAwait(true)).Count.ShouldBe(1);

        var forced = await store.RewindAsync(Session, 1, force: true, Token).ConfigureAwait(true);

        forced.Status.ShouldBe(RewindStatus.Completed);
        _fixture.Read("a.txt").ShouldBe("old\n");
        _fixture.Read("b.txt").ShouldBe("old b\n");
    }

    [Fact]
    public async Task AFileTheUserDeletedIsStaleAndAFileAlreadyBackAtItsEarlierContentIsLeftAlone()
    {
        using var store = Store();
        _fixture.Write("a.txt", "agent\n");
        _fixture.Write("b.txt", "agent b\n");
        await store.RecordTurnAsync(Session, "u1", null, [Change("a.txt", "old\n", "agent\n"), Change("b.txt", "old b\n", "agent b\n")], Token).ConfigureAwait(true);
        File.Delete(Path.Combine(_fixture.WorkspacePath, "a.txt"));
        _fixture.Write("b.txt", "old b\n");

        var plan = await store.PlanRewindAsync(Session, 1, Token).ConfigureAwait(true);

        plan.Files.Single(file => string.Equals(file.Path, "a.txt", StringComparison.Ordinal)).Action.ShouldBe(RewindFileAction.Stale);
        plan.Files.Single(file => string.Equals(file.Path, "b.txt", StringComparison.Ordinal)).Action.ShouldBe(RewindFileAction.AlreadyAtTarget);
        plan.ChangeCount.ShouldBe(0);
    }

    [Fact]
    public async Task RewindKeepsCrlfWhetherTheStoredTextHadItOrNot()
    {
        using var store = Store();
        _fixture.WriteBytes("crlf.txt", Encoding.UTF8.GetBytes("one\r\nTWO\r\n"));
        _fixture.WriteBytes("lf-recorded.txt", Encoding.UTF8.GetBytes("one\r\nTWO\r\n"));
        await store.RecordTurnAsync(
            Session,
            "u1",
            null,
            [
                Change("crlf.txt", "one\r\ntwo\r\n", "one\r\nTWO\r\n"),
                Change("lf-recorded.txt", "one\ntwo\n", "one\nTWO\n"),
            ],
            Token).ConfigureAwait(true);

        var result = await store.RewindAsync(Session, 1, force: false, Token).ConfigureAwait(true);

        result.Status.ShouldBe(RewindStatus.Completed);
        _fixture.ReadBytes("crlf.txt").ShouldBe(Encoding.UTF8.GetBytes("one\r\ntwo\r\n"));
        _fixture.ReadBytes("lf-recorded.txt").ShouldBe(Encoding.UTF8.GetBytes("one\r\ntwo\r\n"));
    }

    [Fact]
    public async Task RewindKeepsTheByteOrderMarkOfTheFileAsItIsNow()
    {
        using var store = Store();
        byte[] bom = [0xEF, 0xBB, 0xBF];
        byte[] current = [.. bom, .. Encoding.UTF8.GetBytes("new\n")];
        byte[] restored = [.. bom, .. Encoding.UTF8.GetBytes("old\n")];
        _fixture.WriteBytes("bom.txt", current);
        await store.RecordTurnAsync(Session, "u1", null, [Change("bom.txt", "old\n", "new\n")], Token).ConfigureAwait(true);

        var result = await store.RewindAsync(Session, 1, force: false, Token).ConfigureAwait(true);

        result.Status.ShouldBe(RewindStatus.Completed);
        _fixture.ReadBytes("bom.txt").ShouldBe(restored);
    }

    [Fact]
    public async Task ABinaryFileIsRecordedByHashOnlyAndAnEarlierStateOfItIsNeverRestored()
    {
        using var store = Store();
        byte[] before = [1, 0, 2, 3];
        byte[] after = [9, 0, 8];
        _fixture.WriteBytes("image.bin", after);
        _fixture.WriteBytes("new.bin", after);

        var turn = await store.RecordTurnAsync(
            Session,
            "u1",
            null,
            [new CheckpointFileChange("image.bin", before, after), new CheckpointFileChange("new.bin", null, after)],
            Token).ConfigureAwait(true);

        turn.ShouldNotBeNull();
        turn.Files[0].Restorable.ShouldBeFalse();
        turn.Files[0].Note.ShouldContain("Binary", Case.Sensitive);
        turn.Files[0].BeforeHash.ShouldNotBeNullOrEmpty();
        turn.Files[1].Restorable.ShouldBeTrue();

        var result = await store.RewindAsync(Session, 1, force: false, Token).ConfigureAwait(true);

        result.Status.ShouldBe(RewindStatus.Completed);
        result.Plan.Files.Single(file => string.Equals(file.Path, "image.bin", StringComparison.Ordinal)).Action.ShouldBe(RewindFileAction.Unrestorable);
        _fixture.ReadBytes("image.bin").ShouldBe(after);
        _fixture.Exists("new.bin").ShouldBeFalse();
        result.Message.ShouldContain("could not be restored", Case.Sensitive);
    }

    [Fact]
    public async Task AFileOverTheSizeLimitKeepsNoCopyAndCannotBeRestored()
    {
        using var store = Store(new CheckpointOptions { MaxFileBytes = 8 });
        _fixture.Write("big.txt", "after\n");

        var turn = await store.RecordTurnAsync(Session, "u1", null, [Change("big.txt", "a long earlier text\n", "after\n")], Token).ConfigureAwait(true);
        var result = await store.RewindAsync(Session, 1, force: false, Token).ConfigureAwait(true);

        turn.ShouldNotBeNull().Files.ShouldHaveSingleItem().Restorable.ShouldBeFalse();
        result.Status.ShouldBe(RewindStatus.NothingToDo);
        _fixture.Read("big.txt").ShouldBe("after\n");
    }

    [Fact]
    public async Task TheOldestTurnsAreDroppedWhenTheTurnCapOrTheByteCapIsExceeded()
    {
        using var byTurns = Store(new CheckpointOptions { MaxTurns = 2 });
        foreach (var key in new[] { "u1", "u2", "u3" })
        {
            await byTurns.RecordTurnAsync(Session, key, null, [Change(key + ".txt", "earlier " + key, "later " + key)], Token).ConfigureAwait(true);
        }

        (await byTurns.ListAsync(Session, Token).ConfigureAwait(true)).Select(turn => turn.TurnKey).ShouldBe(["u2", "u3"]);

        using var byBytes = Store(new CheckpointOptions { MaxSessionBytes = 20 });
        foreach (var key in new[] { "k1", "k2", "k3" })
        {
            await byBytes.RecordTurnAsync("bytes", key, null, [Change(key + ".txt", "earlier-" + key, "after " + key)], Token).ConfigureAwait(true);
        }

        var kept = await byBytes.ListAsync("bytes", Token).ConfigureAwait(true);
        kept.Select(turn => turn.TurnKey).ShouldBe(["k2", "k3"]);
        kept.Select(turn => turn.TurnId).ShouldBe([2, 3]);
    }

    [Fact]
    public async Task ARewoundTurnIsNotRecordedAgainAndTurnNumbersAreNotReused()
    {
        using var store = Store();
        _fixture.Write("a.txt", "new\n");
        await store.RecordTurnAsync(Session, "u1", null, [Change("a.txt", "old\n", "new\n")], Token).ConfigureAwait(true);
        await store.RewindAsync(Session, 1, force: false, Token).ConfigureAwait(true);

        var again = await store.RecordTurnAsync(Session, "u1", null, [Change("a.txt", "old\n", "new\n")], Token).ConfigureAwait(true);
        var next = await store.RecordTurnAsync(Session, "u2", null, [Change("a.txt", "old\n", "newer\n")], Token).ConfigureAwait(true);

        again.ShouldBeNull();
        next.ShouldNotBeNull().TurnId.ShouldBe(2);
    }

    [Fact]
    public async Task AFolderThatIsNotOpenAnyMoreIsRefusedAndNeverTouched()
    {
        var other = Path.Combine(_fixture.BasePath, "other");
        Directory.CreateDirectory(other);
        await File.WriteAllTextAsync(Path.Combine(other, "x.txt"), "agent\n", Token).ConfigureAwait(true);
        using (var both = new CheckpointStore(_fixture.StatePath, [_fixture.WorkspacePath, other]))
        {
            var turn = await both.RecordTurnAsync(Session, "u1", null, [CheckpointFileChange.FromText(Path.Combine(other, "x.txt"), "old\n", "agent\n")], Token).ConfigureAwait(true);
            turn.ShouldNotBeNull();
        }

        using var onlyPrimary = Store();
        var result = await onlyPrimary.RewindAsync(Session, 1, force: true, Token).ConfigureAwait(true);

        result.Plan.Files.ShouldHaveSingleItem().Action.ShouldBe(RewindFileAction.Refused);
        result.Status.ShouldBe(RewindStatus.NothingToDo);
        (await File.ReadAllTextAsync(Path.Combine(other, "x.txt"), Token).ConfigureAwait(true)).ShouldBe("agent\n");
    }

    [Fact]
    public async Task AnUnknownTurnIsReportedAndNothingIsWritten()
    {
        using var store = Store();

        var result = await store.RewindAsync(Session, 7, force: true, Token).ConfigureAwait(true);

        result.Status.ShouldBe(RewindStatus.UnknownTurn);
        (await store.PlanRewindAsync(Session, 7, Token).ConfigureAwait(true)).Files.ShouldBeEmpty();
    }

    [Fact]
    public async Task TheTimelineSurvivesANewStoreAndSessionsAreKeptApart()
    {
        _fixture.Write("a.txt", "new\n");
        using (var first = Store())
        {
            await first.RecordTurnAsync(Session, "u1", "kept", [Change("a.txt", "old\n", "new\n")], Token).ConfigureAwait(true);
        }

        using var second = Store();

        (await second.ListAsync(Session, Token).ConfigureAwait(true)).ShouldHaveSingleItem().Label.ShouldBe("kept");
        (await second.ListAsync("../other session", Token).ConfigureAwait(true)).ShouldBeEmpty();
        var result = await second.RewindAsync(Session, 1, force: false, Token).ConfigureAwait(true);
        result.Status.ShouldBe(RewindStatus.Completed);
    }

    [Fact]
    public async Task ADamagedIndexReadsAsAnEmptyTimelineAndTheNextRecordStartsAgain()
    {
        using var store = Store();
        await store.RecordTurnAsync(Session, "u1", null, [Change("a.txt", "old\n", "new\n")], Token).ConfigureAwait(true);
        var index = Directory.EnumerateFiles(_fixture.StatePath, "index.json", SearchOption.AllDirectories).ShouldHaveSingleItem();
        await File.WriteAllTextAsync(index, "{ not json", Token).ConfigureAwait(true);

        (await store.ListAsync(Session, Token).ConfigureAwait(true)).ShouldBeEmpty();
        var turn = await store.RecordTurnAsync(Session, "u2", null, [Change("a.txt", "old\n", "newer\n")], Token).ConfigureAwait(true);

        turn.ShouldNotBeNull().TurnId.ShouldBe(1);
    }

    [Fact]
    [SupportedOSPlatform("linux")]
    public async Task TheStoredFilesAreOwnerOnlyAndTheirContentIsKeptOnceAndRemovedWithTheSession()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix permissions do not exist on Windows.");
        using var store = Store();
        _fixture.Write("a.txt", "new\n");
        _fixture.Write("b.txt", "new\n");
        await store.RecordTurnAsync(Session, "u1", null, [Change("a.txt", "same earlier\n", "new\n"), Change("b.txt", "same earlier\n", "new\n")], Token).ConfigureAwait(true);

        var blobs = Directory.EnumerateFiles(_fixture.StatePath, "*", SearchOption.AllDirectories).Where(path => path.Contains("blobs", StringComparison.Ordinal)).ToList();
        var everything = Directory.EnumerateFileSystemEntries(_fixture.StatePath, "*", SearchOption.AllDirectories).ToList();

        blobs.Count.ShouldBe(1);
        foreach (var entry in everything)
        {
            (File.GetUnixFileMode(entry) & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute))
                .ShouldBe(UnixFileMode.None, entry);
        }

        await store.DeleteSessionAsync(Session, Token).ConfigureAwait(true);

        (await store.ListAsync(Session, Token).ConfigureAwait(true)).ShouldBeEmpty();
        Directory.EnumerateFiles(_fixture.StatePath, "*", SearchOption.AllDirectories).ShouldBeEmpty();
    }

    [Fact]
    [SupportedOSPlatform("linux")]
    public async Task ARestoredFileKeepsItsExecutableBit()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix permissions do not exist on Windows.");
        using var store = Store();
        _fixture.Write("run.sh", "new\n");
        var path = Path.Combine(_fixture.WorkspacePath, "run.sh");
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        await store.RecordTurnAsync(Session, "u1", null, [Change("run.sh", "old\n", "new\n")], Token).ConfigureAwait(true);

        await store.RewindAsync(Session, 1, force: false, Token).ConfigureAwait(true);

        _fixture.Read("run.sh").ShouldBe("old\n");
        File.GetUnixFileMode(path).HasFlag(UnixFileMode.UserExecute).ShouldBeTrue();
    }

    [Fact]
    public async Task TurnsAreStampedWithTheInjectedClock()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 10, 27, 9, 30, 0, TimeSpan.Zero));
        using var store = Store(clock: clock);

        var turn = await store.RecordTurnAsync(Session, "u1", null, [Change("a.txt", "old\n", "new\n")], Token).ConfigureAwait(true);

        turn.ShouldNotBeNull().RecordedAt.ShouldBe(clock.GetUtcNow());
    }

    [Fact]
    public async Task ALongMultiLineLabelIsFlattenedAndShortened()
    {
        using var store = Store();
        var label = "first line\nsecond line " + new string('x', 400);

        var turn = await store.RecordTurnAsync(Session, "u1", label, [Change("a.txt", "old\n", "new\n")], Token).ConfigureAwait(true);

        turn.ShouldNotBeNull().Label.Length.ShouldBe(200);
        turn.Label.ShouldStartWith("first line second line", Case.Sensitive);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
