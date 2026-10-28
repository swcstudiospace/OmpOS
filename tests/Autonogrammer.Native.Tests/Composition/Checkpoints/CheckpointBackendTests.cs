using Autonogrammer.Native.Composition.Checkpoints;
using Autonogrammer.Presentation.Checkpoints;
using Autonogrammer.Workspace.Checkpoints;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Composition.Checkpoints;

/// <summary>The checkpoint adapter over a real store: what is recorded is listed, previewed and put back, and the statuses reach the presentation types.</summary>
public sealed class CheckpointBackendTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "autonogrammer-ckpt-" + Guid.NewGuid().ToString("N"));
    private readonly string _state;
    private readonly string _workspace;

    /// <summary>Initializes a new instance of the <see cref="CheckpointBackendTests"/> class.</summary>
    public CheckpointBackendTests()
    {
        _state = Path.Combine(_root, "state");
        _workspace = Path.Combine(_root, "workspace");
        Directory.CreateDirectory(_state);
        Directory.CreateDirectory(_workspace);
    }

    /// <summary>Removes the temporary directory.</summary>
    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static CheckpointTurnEdits Turn(string key, string path, string? oldText, string newText) =>
        new(key, "turn " + key, [new CheckpointFileEdit(path, oldText, newText)]);

    /// <summary>Two recorded turns are listed oldest first with their label and file.</summary>
    [Fact]
    public async Task RecordedTurnsAreListedOldestFirst()
    {
        var ct = TestContext.Current.CancellationToken;
        using var store = new CheckpointStore(_state, [_workspace], clock: TimeProvider.System);
        var backend = new CheckpointBackend(store);
        var file = Path.Combine(_workspace, "a.txt");
        await File.WriteAllTextAsync(file, "two", ct).ConfigureAwait(true);
        await backend.RecordTurnAsync("s1", Turn("t1", file, "one", "two"), ct).ConfigureAwait(true);
        await File.WriteAllTextAsync(file, "three", ct).ConfigureAwait(true);
        await backend.RecordTurnAsync("s1", Turn("t2", file, "two", "three"), ct).ConfigureAwait(true);

        var turns = await backend.ListAsync("s1", ct).ConfigureAwait(true);

        turns.Count.ShouldBe(2);
        turns[0].Label.ShouldBe("turn t1");
        turns[1].Label.ShouldBe("turn t2");
        turns[0].TurnId.ShouldBeLessThan(turns[1].TurnId);
        turns[1].Files.ShouldBe(["a.txt"]);
    }

    /// <summary>A preview writes nothing, and a rewind puts the file back to its text before the turn.</summary>
    [Fact]
    public async Task RewindRestoresTheTextBeforeTheTurn()
    {
        var ct = TestContext.Current.CancellationToken;
        using var store = new CheckpointStore(_state, [_workspace], clock: TimeProvider.System);
        var backend = new CheckpointBackend(store);
        var file = Path.Combine(_workspace, "a.txt");
        await File.WriteAllTextAsync(file, "two", ct).ConfigureAwait(true);
        await backend.RecordTurnAsync("s1", Turn("t1", file, "one", "two"), ct).ConfigureAwait(true);
        await File.WriteAllTextAsync(file, "three", ct).ConfigureAwait(true);
        await backend.RecordTurnAsync("s1", Turn("t2", file, "two", "three"), ct).ConfigureAwait(true);
        var turnId = (await backend.ListAsync("s1", ct).ConfigureAwait(true))[1].TurnId;

        var preview = await backend.PreviewAsync("s1", turnId, ct).ConfigureAwait(true);
        (await File.ReadAllTextAsync(file, ct).ConfigureAwait(true)).ShouldBe("three");
        var outcome = await backend.RewindAsync("s1", turnId, force: false, ct).ConfigureAwait(true);

        preview.Files.Count.ShouldBe(1);
        preview.Files[0].State.ShouldBe(CheckpointFileState.Restore);
        outcome.Status.ShouldBe(CheckpointRewindStatus.Completed);
        (await File.ReadAllTextAsync(file, ct).ConfigureAwait(true)).ShouldBe("two");
    }

    /// <summary>A file changed after the agent's last edit is stale: it blocks the rewind until forced.</summary>
    [Fact]
    public async Task AFileEditedAfterTheTurnBlocksTheRewindUntilForced()
    {
        var ct = TestContext.Current.CancellationToken;
        using var store = new CheckpointStore(_state, [_workspace], clock: TimeProvider.System);
        var backend = new CheckpointBackend(store);
        var file = Path.Combine(_workspace, "a.txt");
        await File.WriteAllTextAsync(file, "two", ct).ConfigureAwait(true);
        await backend.RecordTurnAsync("s1", Turn("t1", file, "one", "two"), ct).ConfigureAwait(true);
        await File.WriteAllTextAsync(file, "by hand", ct).ConfigureAwait(true);
        var turnId = (await backend.ListAsync("s1", ct).ConfigureAwait(true))[0].TurnId;

        var blocked = await backend.RewindAsync("s1", turnId, force: false, ct).ConfigureAwait(true);
        (await File.ReadAllTextAsync(file, ct).ConfigureAwait(true)).ShouldBe("by hand");
        var forced = await backend.RewindAsync("s1", turnId, force: true, ct).ConfigureAwait(true);

        blocked.Status.ShouldBe(CheckpointRewindStatus.Blocked);
        blocked.Preview.Files[0].State.ShouldBe(CheckpointFileState.Stale);
        blocked.Preview.StaleCount.ShouldBe(1);
        forced.Status.ShouldBe(CheckpointRewindStatus.Completed);
        (await File.ReadAllTextAsync(file, ct).ConfigureAwait(true)).ShouldBe("one");
    }

    /// <summary>A file the turn created is deleted by the rewind, and an unknown turn is a status, not an exception.</summary>
    [Fact]
    public async Task ACreatedFileIsDeletedAndAnUnknownTurnIsAStatus()
    {
        var ct = TestContext.Current.CancellationToken;
        using var store = new CheckpointStore(_state, [_workspace], clock: TimeProvider.System);
        var backend = new CheckpointBackend(store);
        var file = Path.Combine(_workspace, "new.txt");
        await File.WriteAllTextAsync(file, "hello", ct).ConfigureAwait(true);
        await backend.RecordTurnAsync("s1", Turn("t1", file, null, "hello"), ct).ConfigureAwait(true);
        var turnId = (await backend.ListAsync("s1", ct).ConfigureAwait(true))[0].TurnId;

        var preview = await backend.PreviewAsync("s1", turnId, ct).ConfigureAwait(true);
        var unknown = await backend.RewindAsync("s1", turnId + 100, force: false, ct).ConfigureAwait(true);
        var outcome = await backend.RewindAsync("s1", turnId, force: false, ct).ConfigureAwait(true);

        preview.Files[0].State.ShouldBe(CheckpointFileState.Delete);
        unknown.Status.ShouldBe(CheckpointRewindStatus.UnknownTurn);
        outcome.Status.ShouldBe(CheckpointRewindStatus.Completed);
        File.Exists(file).ShouldBeFalse();
    }
}
