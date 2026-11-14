using System.Text.Json;
using Autonogrammer.Cowork.Sessions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Cowork.Tests.Sessions;

/// <summary>
/// The first-prompt record: the snapshot is kept at the task's first start, a block is sent once, an unknown outcome stops sends,
/// a replay keeps its bytes, and the files are owner-only, atomic and per account. No test here claims what a live omp stores.
/// </summary>
public sealed class CoworkFirstPromptStoreTests
{
    private const string Block = "global-canary\n\n<untrusted>project-canary</untrusted>";

    [Fact]
    public void ANewTaskIsPendingAndALaterStartReusesTheFirstSnapshot()
    {
        using var root = new StoreRoot();
        var store = root.Store("acct");

        var created = store.Ensure("task-1", Block);
        created.ShouldNotBeNull();
        created.State.ShouldBe(CoworkFirstPromptState.Pending);
        created.Text.ShouldBe(Block);
        created.Uri.ShouldBe(CoworkFirstPromptStore.ResourceUriFor("task-1"));
        created.MimeType.ShouldBe(CoworkFirstPromptStore.ResourceMimeType);

        // A restart with changed account data reads the stored snapshot, never a recalculated one.
        var reopened = root.Store("acct").Ensure("task-1", "changed-account-data");
        reopened.ShouldNotBeNull();
        reopened.Text.ShouldBe(Block);
        reopened.State.ShouldBe(CoworkFirstPromptState.Pending);
    }

    [Fact]
    public void ATaskWithNoBlockHasNoRecord()
    {
        using var root = new StoreRoot();
        var store = root.Store("acct");

        store.Ensure("task-empty", null).ShouldBeNull();
        store.Ensure("task-empty", string.Empty).ShouldBeNull();
        store.Read("task-empty").ShouldBeNull();
    }

    [Fact]
    public void AnAcknowledgedSendIsNotRepeatedAndAFailedOneReturnsToPending()
    {
        using var root = new StoreRoot();
        var store = root.Store("acct");
        store.Ensure("task-ack", Block);
        store.MarkInFlight("task-ack");
        store.Read("task-ack")!.State.ShouldBe(CoworkFirstPromptState.InFlight);

        store.Settle("task-ack", transmitted: true, completed: true);
        store.Read("task-ack")!.State.ShouldBe(CoworkFirstPromptState.Acknowledged);
        Should.Throw<InvalidOperationException>(() => store.MarkInFlight("task-ack"));

        store.Ensure("task-fail", Block);
        store.MarkInFlight("task-fail");
        store.Settle("task-fail", transmitted: false, completed: false);
        store.Read("task-fail")!.State.ShouldBe(CoworkFirstPromptState.Pending);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void AnOutcomeThatIsNotProvenStaysUnconfirmedAndIsNeverResentAutomatically(bool transmitted, bool completed)
    {
        using var root = new StoreRoot();
        var store = root.Store("acct");
        store.Ensure("task-unknown", Block);
        store.MarkInFlight("task-unknown");

        store.Settle("task-unknown", transmitted, completed);

        store.Read("task-unknown")!.State.ShouldBe(CoworkFirstPromptState.Unconfirmed);
        Should.Throw<InvalidOperationException>(() => store.MarkInFlight("task-unknown"));
    }

    [Fact]
    public void AnInFlightBlockAfterARestartIsUnknownAndOnlyTheUserCanResolveIt()
    {
        using var root = new StoreRoot();
        var store = root.Store("acct");
        store.Ensure("task-crash", Block);

        // The record an earlier run left in flight: its send started and that run stopped before the outcome was recorded.
        var crashed = new CoworkFirstPromptDocument
        {
            SessionId = "task-crash",
            State = "inFlight",
            Uri = CoworkFirstPromptStore.ResourceUriFor("task-crash").AbsoluteUri,
            MimeType = CoworkFirstPromptStore.ResourceMimeType,
            Text = Block,
            RunId = "earlier-run",
        };
        File.WriteAllText(
            Path.Combine(store.DirectoryPath, "task-crash.json"),
            JsonSerializer.Serialize(crashed, CoworkFirstPromptJsonContext.Default.CoworkFirstPromptDocument));

        // A new store instance, as after a restart, opens the record: the send may have been transmitted, so it is unknown, not pending.
        var afterRestart = root.Store("acct");
        afterRestart.Read("task-crash")!.State.ShouldBe(CoworkFirstPromptState.Unconfirmed);

        // Only an in-flight send settles, so a settle after the restart cannot return the block to pending.
        afterRestart.Settle("task-crash", transmitted: false, completed: false);
        afterRestart.Read("task-crash")!.State.ShouldBe(CoworkFirstPromptState.Unconfirmed);

        // Only the user can resolve it: a resend is the user's choice.
        afterRestart.Resend("task-crash");
        afterRestart.Read("task-crash")!.State.ShouldBe(CoworkFirstPromptState.Pending);
        afterRestart.Read("task-crash")!.Text.ShouldBe(Block);
    }

    [Fact]
    public void AbandoningAnUnknownBlockKeepsItOutOfEverySend()
    {
        using var root = new StoreRoot();
        var store = root.Store("acct");
        store.Ensure("task-skip", Block);
        store.MarkInFlight("task-skip");
        store.Settle("task-skip", transmitted: true, completed: false);

        store.Abandon("task-skip");

        store.Read("task-skip")!.State.ShouldBe(CoworkFirstPromptState.Abandoned);
        Should.Throw<InvalidOperationException>(() => store.Resend("task-skip"));
    }

    [Fact]
    public void AReplayThatCarriesTheBlockAcknowledgesItAndKeepsTheReplayedBytes()
    {
        using var root = new StoreRoot();
        var store = root.Store("acct");
        var uri = CoworkFirstPromptStore.ResourceUriFor("task-replay");
        store.Ensure("task-replay", Block);
        store.MarkInFlight("task-replay");
        store.Settle("task-replay", transmitted: true, completed: false);
        const string replayed = "héllo 世界 🙂\n" + "x";

        store.RecordReplay("task-replay", uri, replayed);

        var record = store.Read("task-replay")!;
        record.State.ShouldBe(CoworkFirstPromptState.Acknowledged);
        record.ReplayedText.ShouldBe(replayed);
        record.Text.ShouldBe(Block);
    }

    [Fact]
    public void AReplayOfAnotherResourceChangesNothing()
    {
        using var root = new StoreRoot();
        var store = root.Store("acct");
        store.Ensure("task-other", Block);

        store.RecordReplay("task-other", new Uri("autonogrammer://cowork/standing-context/someone-else", UriKind.Absolute), Block);

        var record = store.Read("task-other")!;
        record.State.ShouldBe(CoworkFirstPromptState.Pending);
        record.ReplayedText.ShouldBeNull();
    }

    [Fact]
    public void ARecordIsNeverPrintedWithItsBlockText()
    {
        using var root = new StoreRoot();
        var record = root.Store("acct").Ensure("task-print", Block)!;

        record.ToString().ShouldNotContain("canary", Case.Sensitive);
        record.ToString().ShouldContain("task-print", Case.Sensitive);
    }

    [Fact]
    public void ATornRecordIsRefusedAndNeverResent()
    {
        using var root = new StoreRoot();
        var store = root.Store("acct");
        store.Ensure("task-torn", Block);
        var path = Path.Combine(store.DirectoryPath, "task-torn.json");
        var whole = File.ReadAllText(path);
        File.WriteAllText(path, whole[..(whole.Length / 2)]);

        Should.Throw<InvalidDataException>(() => store.Read("task-torn"));
        Should.Throw<InvalidDataException>(() => store.Ensure("task-torn", Block));
        Should.Throw<InvalidDataException>(() => store.MarkInFlight("task-torn"));
    }

    [Fact]
    public void ALeftoverTemporaryFileDoesNotShadowTheRecord()
    {
        using var root = new StoreRoot();
        var store = root.Store("acct");
        store.Ensure("task-tmp", Block);
        File.WriteAllText(Path.Combine(store.DirectoryPath, "task-tmp.json." + Guid.NewGuid().ToString("N") + ".tmp"), "{ torn");

        store.Read("task-tmp")!.Text.ShouldBe(Block);
    }

    [Fact]
    public void AWrongSessionIdentifierInsideARecordIsRefused()
    {
        using var root = new StoreRoot();
        var store = root.Store("acct");
        store.Ensure("task-a", Block);
        File.Copy(Path.Combine(store.DirectoryPath, "task-a.json"), Path.Combine(store.DirectoryPath, "task-b.json"));

        Should.Throw<InvalidDataException>(() => store.Read("task-b"));
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("has space")]
    [InlineData("dot.name")]
    public void AnUnsafeSessionIdentifierIsRefused(string sessionId)
    {
        using var root = new StoreRoot();
        var store = root.Store("acct");

        Should.Throw<ArgumentException>(() => store.Read(sessionId));
        Should.Throw<ArgumentException>(() => store.Ensure(sessionId, Block));
        Should.Throw<ArgumentException>(() => CoworkFirstPromptStore.ResourceUriFor(sessionId));
    }

    [Fact]
    public void ABlankSessionIdentifierIsRefused()
    {
        using var root = new StoreRoot();
        var store = root.Store("acct");

        Should.Throw<ArgumentException>(() => store.Read("   "));
    }

    [Fact]
    public void AnOverBudgetBlockIsRefusedBeforeAnythingIsWritten()
    {
        using var root = new StoreRoot();
        var store = root.Store("acct");
        var tooLarge = new string('a', CoworkFirstPromptStore.MaxBlockBytes + 1);

        Should.Throw<ArgumentOutOfRangeException>(() => store.Ensure("task-big", tooLarge));
        store.Read("task-big").ShouldBeNull();
    }

    [Fact]
    public void RecordsAreIsolatedPerAccount()
    {
        using var root = new StoreRoot();
        root.Store("acct-a").Ensure("task-shared-id", "block-of-a");

        root.Store("acct-b").Read("task-shared-id").ShouldBeNull();
        root.Store("acct-a").Read("task-shared-id")!.Text.ShouldBe("block-of-a");
    }

    [Fact]
    public void DeleteRemovesOnlyThatTasksRecord()
    {
        using var root = new StoreRoot();
        var store = root.Store("acct");
        store.Ensure("task-keep", Block);
        store.Ensure("task-drop", Block);

        store.Delete("task-drop");
        store.Delete("task-drop");

        store.Read("task-drop").ShouldBeNull();
        store.Read("task-keep")!.Text.ShouldBe(Block);
    }

    [Fact]
    public void TwentyNearLimitTasksSurviveReopenAndLeaveTheSharedMetadataFileAlone()
    {
        using var root = new StoreRoot();
        var accountDirectory = CoworkAccountFolder.AccountDirectory(root.Path, CoworkAccountFolder.KeyFromSubject("acct"));
        var store = root.Store("acct");
        var near = new string('n', CoworkFirstPromptStore.MaxBlockBytes - 64);
        for (var index = 0; index < 20; index++)
        {
            store.Ensure("task-" + index.ToString("D2", System.Globalization.CultureInfo.InvariantCulture), near + index.ToString("D2", System.Globalization.CultureInfo.InvariantCulture))!.Text.Length.ShouldBe(near.Length + 2);
        }

        var reopened = root.Store("acct");
        for (var index = 0; index < 20; index++)
        {
            var record = reopened.Read("task-" + index.ToString("D2", System.Globalization.CultureInfo.InvariantCulture))!;
            record.Text.ShouldBe(near + index.ToString("D2", System.Globalization.CultureInfo.InvariantCulture));
            record.State.ShouldBe(CoworkFirstPromptState.Pending);
        }

        File.Exists(Path.Combine(accountDirectory, CoworkSessionMetaStore.FileName)).ShouldBeFalse();
        Directory.GetFiles(reopened.DirectoryPath, "*.json").Length.ShouldBe(20);
    }

    [Fact]
    public void RecordFilesAndDirectoriesAreOwnerOnly()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var root = new StoreRoot();
        var store = root.Store("acct");
        store.Ensure("task-mode", Block);

        File.GetUnixFileMode(Path.Combine(store.DirectoryPath, "task-mode.json")).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        File.GetUnixFileMode(store.DirectoryPath).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    private sealed class StoreRoot : IDisposable
    {
        public StoreRoot()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cowork-first-prompt-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public CoworkFirstPromptStore Store(string subject) => new(Path, CoworkAccountFolder.KeyFromSubject(subject));

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
