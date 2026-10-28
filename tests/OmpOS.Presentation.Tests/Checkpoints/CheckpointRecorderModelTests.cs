using OmpOS.Contracts.Ui;
using OmpOS.Presentation.Checkpoints;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests.Checkpoints;

/// <summary>Verifies that the recorder writes a turn once per real change and survives a failing backend.</summary>
public sealed class CheckpointRecorderModelTests
{
    private const string Root = "/work/app";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static UserMessageBlock User(string id) => new() { Id = id, Text = "prompt " + id };

    [Fact]
    public async Task ATurnIsRecordedOnceWhileItsEditsStayTheSameAndAgainWhenTheyGrow()
    {
        var backend = new FakeCheckpointBackend();
        using var recorder = new CheckpointRecorderModel("s1", Root, backend);
        List<UiBlock> blocks = [User("u1"), ChangeBlocks.Edit("a.txt", "v0", "v1", "c1")];

        await recorder.UpdateAsync(blocks, Token).ConfigureAwait(true);
        await recorder.UpdateAsync(blocks, Token).ConfigureAwait(true);
        blocks.Add(ChangeBlocks.Edit("a.txt", "v1", "v2", "c2"));
        await recorder.UpdateAsync(blocks, Token).ConfigureAwait(true);

        backend.Recorded.Count.ShouldBe(2);
        backend.Recorded[1].Files.ShouldHaveSingleItem().NewText.ShouldBe("v2");
        recorder.RecordedCount.ShouldBe(2);
    }

    [Fact]
    public async Task EveryTurnThatChangedFilesIsRecordedUnderItsKey()
    {
        var backend = new FakeCheckpointBackend();
        using var recorder = new CheckpointRecorderModel("s1", Root, backend);

        await recorder.UpdateAsync(
            [User("u1"), ChangeBlocks.Edit("a.txt", "a", "b", "c1"), User("u2"), ChangeBlocks.Edit("b.txt", null, "x", "c2")],
            Token).ConfigureAwait(true);

        backend.Recorded.Select(turn => turn.TurnKey).ShouldBe(["u1", "u2"]);
    }

    [Fact]
    public async Task AFailingBackendNeverFailsTheCallerAndTheNextUpdateTriesAgain()
    {
        var backend = new FakeCheckpointBackend { Failure = new IOException("disk full") };
        using var recorder = new CheckpointRecorderModel("s1", Root, backend);
        IReadOnlyList<UiBlock> blocks = [User("u1"), ChangeBlocks.Edit("a.txt", "a", "b", "c1")];

        await recorder.UpdateAsync(blocks, Token).ConfigureAwait(true);

        recorder.LastError.ShouldContain("disk full", Case.Sensitive);
        backend.Recorded.ShouldBeEmpty();

        backend.Failure = null;
        await recorder.UpdateAsync(blocks, Token).ConfigureAwait(true);

        backend.Recorded.ShouldHaveSingleItem();
        recorder.LastError.ShouldBeEmpty();
    }

    [Fact]
    public async Task ATranscriptWithoutEditsRecordsNothing()
    {
        var backend = new FakeCheckpointBackend();
        using var recorder = new CheckpointRecorderModel("s1", Root, backend);

        await recorder.UpdateAsync([User("u1")], Token).ConfigureAwait(true);

        backend.Recorded.ShouldBeEmpty();
    }
}
