using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using OmpOS.Contracts.Agents;
using OmpOS.Harness.AgentLoop.Sessions;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.AgentLoop.Tests.Sessions;

/// <summary>
/// Verifies the session registry through its persisted file, its snapshots and its events: restart recovery, quarantine of an
/// unreadable file, the session cap, concurrent writers and the redaction of the resume reference.
/// </summary>
public sealed partial class AgentSessionRegistryTests
{
    private const string Secret = "omp-native-ref-SECRET-9f3a";

    private static readonly DateTimeOffset Start = new(2026, 10, 26, 8, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [GeneratedRegex("^s-[a-z2-7]{26}$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex IdPattern();

    [Fact]
    public async Task RecordsSurviveAReopenWithEveryField()
    {
        using var directory = new SessionTestDirectory();
        var clock = new SessionTestClock(Start);
        var statePath = directory.PathOf(Path.Combine("state", "sessions.json"));
        var registry = new AgentSessionRegistry(statePath, clock);
        var created = registry.Create("Fix login", "/work/app");
        clock.Advance(TimeSpan.FromSeconds(5));
        registry.SetAcpSession(created.Id, "acp-1", "native-ref-1").ShouldBeTrue();
        registry.SetGraphId(created.Id, "ut-abc-12345678").ShouldBeTrue();
        registry.SetTaskId(created.Id, "task-7").ShouldBeTrue();
        registry.SetLifecycle(created.Id, SessionLifecycle.Running).ShouldBeTrue();
        var expected = registry.SnapshotRecords();

        var asynchronous = await AgentSessionRegistry.OpenAsync(statePath, clock, null, null, Token);
        var synchronous = new AgentSessionRegistry(statePath, clock);

        created.StateDirectory.ShouldBe(Path.Combine(directory.Path, "state", "sessions", created.Id));
        expected.Single().ShouldSatisfyAllConditions(
            record => record.Title.ShouldBe("Fix login"),
            record => record.WorkspaceRoot.ShouldBe("/work/app"),
            record => record.AcpSessionId.ShouldBe("acp-1"),
            record => record.ResumeReference.ShouldBe("native-ref-1"),
            record => record.GraphId.ShouldBe("ut-abc-12345678"),
            record => record.TaskId.ShouldBe("task-7"),
            record => record.Lifecycle.ShouldBe(SessionLifecycle.Running),
            record => record.Heartbeat.ShouldBe(Start.AddSeconds(5)));
        asynchronous.SnapshotRecords().ShouldBe(expected);
        synchronous.SnapshotRecords().ShouldBe(expected);
    }

    [Fact]
    [SupportedOSPlatform("linux")]
    public void StateFileIsOwnerOnlyAndItsDirectoryIsCreatedOwnerOnly()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Unix file modes only.");
        }

        using var directory = new SessionTestDirectory();
        var stateDirectory = directory.PathOf("state");
        var statePath = Path.Combine(stateDirectory, "sessions.json");

        _ = new AgentSessionRegistry(statePath, new SessionTestClock(Start)).Create("t", null);

        File.GetUnixFileMode(statePath).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        File.GetUnixFileMode(stateDirectory).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    [Fact]
    public async Task TemporaryFileLeftBySimulatedCrashIsIgnoredOnNextOpen()
    {
        using var directory = new SessionTestDirectory();
        var clock = new SessionTestClock(Start);
        var registry = new AgentSessionRegistry(directory.StatePath, clock);
        var committed = registry.Create("Committed", null);
        var leftover = directory.StatePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        await File.WriteAllTextAsync(leftover, "{\"schemaVersion\":1,\"sessions\":[{\"id\":\"s-half\"", Token);

        var reopened = await AgentSessionRegistry.OpenAsync(directory.StatePath, clock, null, null, Token).ConfigureAwait(true);

        reopened.SnapshotRecords().Select(record => record.Id).ShouldBe(new[] { committed.Id });
        Directory.GetFiles(directory.Path, "*.quarantined-*").ShouldBeEmpty();
    }

    [Fact]
    public void FailedRenameLeavesNoTemporaryFileAndKeepsTheInMemoryChange()
    {
        using var directory = new SessionTestDirectory();
        var logger = new SessionTestLogger();
        var blockedPath = directory.PathOf("sessions.json");
        Directory.CreateDirectory(blockedPath);
        var registry = new AgentSessionRegistry(blockedPath, new SessionTestClock(Start), logger);

        var created = registry.Create("Unsaved", null);

        registry.TryGet(created.Id, out var found).ShouldBeTrue();
        found.Title.ShouldBe("Unsaved");
        Directory.GetFiles(directory.Path, "*.tmp").ShouldBeEmpty();
        logger.Messages.ShouldContain(message => message.Contains("sessions.json", StringComparison.Ordinal) && message.Contains("could not be written", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json {")]
    [InlineData("null")]
    [InlineData("{\"schemaVersion\":2,\"sessions\":[]}")]
    [InlineData("{\"sessions\":[]}")]
    [InlineData("{\"schemaVersion\":1}")]
    [InlineData("{\"schemaVersion\":1,\"sessions\":null}")]
    public async Task UnusableFileIsQuarantinedWithTheExactNameAndTheRegistryStartsEmpty(string content)
    {
        using var directory = new SessionTestDirectory();
        var clock = new SessionTestClock(Start.AddSeconds(5));
        var logger = new SessionTestLogger();
        await File.WriteAllTextAsync(directory.StatePath, content, Token);

        var registry = await AgentSessionRegistry.OpenAsync(directory.StatePath, clock, logger, null, Token);

        registry.SnapshotRecords().ShouldBeEmpty();
        File.Exists(directory.StatePath).ShouldBeFalse();
        var quarantined = directory.StatePath + ".quarantined-20261026T080005Z";
        (await File.ReadAllTextAsync(quarantined, Token)).ShouldBe(content);
        logger.Messages.ShouldHaveSingleItem().ShouldContain("sessions.json");
        registry.Create("After quarantine", null);
        File.Exists(directory.StatePath).ShouldBeTrue();
    }

    [Fact]
    public async Task OversizedFileIsQuarantinedWithoutBeingParsed()
    {
        using var directory = new SessionTestDirectory();
        await File.WriteAllTextAsync(directory.StatePath, "{\"schemaVersion\":1,\"sessions\":[],\"pad\":\"" + new string('x', 1024 * 1024) + "\"}", Token);

        var registry = new AgentSessionRegistry(directory.StatePath, new SessionTestClock(Start));

        registry.SnapshotRecords().ShouldBeEmpty();
        Directory.GetFiles(directory.Path, "sessions.json.quarantined-*").Length.ShouldBe(1);
    }

    [Fact]
    public async Task SecondQuarantineInTheSameSecondGetsADistinctName()
    {
        using var directory = new SessionTestDirectory();
        var clock = new SessionTestClock(Start);
        await File.WriteAllTextAsync(directory.StatePath, "garbage one", Token);
        _ = new AgentSessionRegistry(directory.StatePath, clock);
        await File.WriteAllTextAsync(directory.StatePath, "garbage two", Token);

        _ = new AgentSessionRegistry(directory.StatePath, clock);

        var first = directory.StatePath + ".quarantined-20261026T080000Z";
        (await File.ReadAllTextAsync(first, Token)).ShouldBe("garbage one");
        (await File.ReadAllTextAsync(first + "-1", Token)).ShouldBe("garbage two");
    }

    [Fact]
    public async Task UnknownMembersAreSkippedAndUnusableEntriesAreDropped()
    {
        using var directory = new SessionTestDirectory();
        await File.WriteAllTextAsync(
            directory.StatePath,
            """
            {"schemaVersion":1,"future":{"x":1},"sessions":[
              {"id":"s-keep","title":"Kept","lifecycle":"Paused","createdAt":"2026-10-26T07:00:00+00:00","extra":true},
              {"id":"../escape","title":"bad"},
              {"id":"..","title":"bad"},
              {"id":"s-keep","title":"duplicate"},
              {"id":"s-odd","lifecycle":"Bogus"},
              {"id":"s-number","lifecycle":"2"},
              null
            ]}
            """,
            Token);

        var registry = new AgentSessionRegistry(directory.StatePath, new SessionTestClock(Start));

        var records = registry.SnapshotRecords().ToDictionary(record => record.Id, StringComparer.Ordinal);
        records.Keys.Order(StringComparer.Ordinal).ToArray().ShouldBe(["s-keep", "s-number", "s-odd"]);
        records["s-keep"].Lifecycle.ShouldBe(SessionLifecycle.Paused);
        records["s-keep"].Title.ShouldBe("Kept");
        records["s-odd"].Lifecycle.ShouldBe(SessionLifecycle.Lost);
        records["s-number"].Lifecycle.ShouldBe(SessionLifecycle.Lost);
        records["s-odd"].Title.ShouldBe("New session");
        Directory.GetFiles(directory.Path, "*.quarantined-*").ShouldBeEmpty();
    }

    [Fact]
    public async Task RecoverAfterRestartMarksOnlyRunningSessionsAsLostAndPersistsIt()
    {
        using var directory = new SessionTestDirectory();
        var clock = new SessionTestClock(Start);
        var registry = new AgentSessionRegistry(directory.StatePath, clock);
        var ids = new Dictionary<SessionLifecycle, string>();
        foreach (var lifecycle in Enum.GetValues<SessionLifecycle>())
        {
            var record = registry.Create(lifecycle.ToString(), null);
            registry.SetLifecycle(record.Id, lifecycle);
            ids[lifecycle] = record.Id;
        }

        var secondRunning = registry.Create("Running too", null);
        registry.SetLifecycle(secondRunning.Id, SessionLifecycle.Running);
        clock.Advance(TimeSpan.FromHours(1));
        var reopened = await AgentSessionRegistry.OpenAsync(directory.StatePath, clock, null, null, Token).ConfigureAwait(true);
        var changes = 0;
        reopened.Changed += (sender, args) => changes++;

        var lost = reopened.RecoverAfterRestart();

        lost.Order(StringComparer.Ordinal).ToArray().ShouldBe(new[] { ids[SessionLifecycle.Running], secondRunning.Id }.Order(StringComparer.Ordinal).ToArray());
        foreach (var (lifecycle, id) in ids.Where(pair => pair.Key != SessionLifecycle.Running))
        {
            reopened.TryGet(id, out var record).ShouldBeTrue();
            record.Lifecycle.ShouldBe(lifecycle);
        }

        reopened.TryGet(ids[SessionLifecycle.Running], out var running).ShouldBeTrue();
        running.Lifecycle.ShouldBe(SessionLifecycle.Lost);
        changes.ShouldBe(1);
        reopened.RecoverAfterRestart().ShouldBeEmpty();
        changes.ShouldBe(1);
        var third = await AgentSessionRegistry.OpenAsync(directory.StatePath, clock, null, null, Token).ConfigureAwait(true);
        third.TryGet(secondRunning.Id, out var persisted).ShouldBeTrue();
        persisted.Lifecycle.ShouldBe(SessionLifecycle.Lost);
    }

    [Fact]
    public void DetectLostMarksOnlyRunningSessionsWhoseHeartbeatIsOlderThanStaleAfter()
    {
        using var directory = new SessionTestDirectory();
        var clock = new SessionTestClock(Start);
        var registry = new AgentSessionRegistry(directory.StatePath, clock);
        var silent = registry.Create("Silent", null);
        var beating = registry.Create("Beating", null);
        var paused = registry.Create("Paused", null);
        registry.SetLifecycle(silent.Id, SessionLifecycle.Running);
        registry.SetLifecycle(beating.Id, SessionLifecycle.Running);
        registry.SetLifecycle(paused.Id, SessionLifecycle.Paused);
        clock.Advance(TimeSpan.FromSeconds(40));
        registry.Touch(beating.Id).ShouldBeTrue();
        clock.Advance(TimeSpan.FromSeconds(5));

        registry.DetectLost().ShouldBeEmpty();

        clock.Advance(TimeSpan.FromSeconds(1));
        registry.DetectLost().ShouldBe(new[] { silent.Id });
        registry.TryGet(silent.Id, out var lost).ShouldBeTrue();
        lost.Lifecycle.ShouldBe(SessionLifecycle.Lost);
        registry.TryGet(beating.Id, out var alive).ShouldBeTrue();
        alive.Lifecycle.ShouldBe(SessionLifecycle.Running);
        registry.TryGet(paused.Id, out var stillPaused).ShouldBeTrue();
        stillPaused.Lifecycle.ShouldBe(SessionLifecycle.Paused);
    }

    [Fact]
    public void SnapshotListsTheMostRecentlyActiveSessionFirst()
    {
        using var directory = new SessionTestDirectory();
        var clock = new SessionTestClock(Start);
        var registry = new AgentSessionRegistry(directory.StatePath, clock);
        var first = registry.Create("first", "/w/one");
        clock.Advance(TimeSpan.FromSeconds(1));
        var second = registry.Create("second", null);
        clock.Advance(TimeSpan.FromSeconds(1));
        var third = registry.Create("third", null);
        registry.Snapshot().Select(summary => summary.Id).ShouldBe(new[] { third.Id, second.Id, first.Id });

        clock.Advance(TimeSpan.FromSeconds(1));
        registry.Touch(first.Id).ShouldBeTrue();

        var snapshot = registry.Snapshot();
        snapshot.Select(summary => summary.Id).ShouldBe(new[] { first.Id, third.Id, second.Id });
        snapshot[0].ShouldSatisfyAllConditions(
            summary => summary.Title.ShouldBe("first"),
            summary => summary.WorkspaceRoot.ShouldBe("/w/one"),
            summary => summary.CreatedAt.ShouldBe(Start),
            summary => summary.LastActivity.ShouldBe(Start.AddSeconds(3)));
    }

    [Fact]
    public void ChangedIsRaisedOncePerMutationAndNeverForANoOp()
    {
        using var directory = new SessionTestDirectory();
        var registry = new AgentSessionRegistry(directory.StatePath, new SessionTestClock(Start));
        var raised = 0;
        registry.Changed += (sender, args) => raised++;

        var record = registry.Create("a", null);
        raised.ShouldBe(1);
        registry.Rename(record.Id, "b").ShouldBeTrue();
        raised.ShouldBe(2);
        registry.SetLifecycle(record.Id, SessionLifecycle.Running).ShouldBeTrue();
        raised.ShouldBe(3);
        registry.SetAcpSession(record.Id, "acp", "ref").ShouldBeTrue();
        raised.ShouldBe(4);
        registry.SetGraphId(record.Id, "ut-1").ShouldBeTrue();
        raised.ShouldBe(5);
        registry.SetTaskId(record.Id, "t-1").ShouldBeTrue();
        raised.ShouldBe(6);
        registry.SetLifecycle(record.Id, SessionLifecycle.Running).ShouldBeTrue();
        registry.SetGraphId(record.Id, "ut-1").ShouldBeTrue();
        raised.ShouldBe(6);
        registry.Close(record.Id).ShouldBeTrue();
        raised.ShouldBe(7);
        registry.Close(record.Id).ShouldBeFalse();
        registry.Rename(record.Id, "gone").ShouldBeFalse();
        raised.ShouldBe(7);
    }

    [Fact]
    public void TouchUpdatesMemoryAlwaysButWritesAndRaisesAtMostOncePerSecond()
    {
        using var directory = new SessionTestDirectory();
        var clock = new SessionTestClock(Start);
        var registry = new AgentSessionRegistry(directory.StatePath, clock);
        var record = registry.Create("a", null);
        var raised = 0;
        registry.Changed += (sender, args) => raised++;

        clock.Advance(TimeSpan.FromSeconds(2));
        registry.Touch(record.Id).ShouldBeTrue();
        raised.ShouldBe(1);
        var written = File.ReadAllText(directory.StatePath);
        clock.Advance(TimeSpan.FromMilliseconds(400));
        registry.Touch(record.Id).ShouldBeTrue();

        raised.ShouldBe(1);
        File.ReadAllText(directory.StatePath).ShouldBe(written);
        registry.TryGet(record.Id, out var inMemory).ShouldBeTrue();
        inMemory.LastActivity.ShouldBe(Start.AddMilliseconds(2400));
        clock.Advance(TimeSpan.FromMilliseconds(600));
        registry.Touch(record.Id).ShouldBeTrue();
        raised.ShouldBe(2);
        File.ReadAllText(directory.StatePath).ShouldNotBe(written, StringComparer.Ordinal);
        registry.Touch("missing").ShouldBeFalse();
    }

    [Fact]
    public void ThrowingHandlerNeitherBreaksTheRegistryNorStopsOtherHandlers()
    {
        using var directory = new SessionTestDirectory();
        var logger = new SessionTestLogger();
        var registry = new AgentSessionRegistry(directory.StatePath, new SessionTestClock(Start), logger);
        var before = 0;
        var after = 0;
        registry.Changed += (sender, args) => before++;
        registry.Changed += (sender, args) => throw new InvalidOperationException("handler failure");
        registry.Changed += (sender, args) => after++;

        var record = registry.Create("a", null);
        registry.Rename(record.Id, "b").ShouldBeTrue();

        (before, after).ShouldBe((2, 2));
        registry.Snapshot().Single().Title.ShouldBe("b");
        logger.Messages.Count(message => message.Contains("InvalidOperationException", StringComparison.Ordinal)).ShouldBe(2);
    }

    [Fact]
    public void HandlersRunOutsideTheRegistryLock()
    {
        using var directory = new SessionTestDirectory();
        var registry = new AgentSessionRegistry(directory.StatePath, new SessionTestClock(Start));
        var otherThreadProgressed = false;
        registry.Changed += (sender, args) =>
        {
            using var done = new ManualResetEventSlim();
            var thread = new Thread(() =>
            {
                var summaries = registry.Snapshot();
                done.Set();
                GC.KeepAlive(summaries);
            })
            {
                IsBackground = true,
            };
            thread.Start();
            otherThreadProgressed = done.Wait(TimeSpan.FromSeconds(10), Token);
        };

        registry.Create("a", null);

        otherThreadProgressed.ShouldBeTrue();
    }

    [Fact]
    public void CreateBeyondTheCapThrowsATypedExceptionAndAFreedSlotIsReusable()
    {
        using var directory = new SessionTestDirectory();
        var registry = new AgentSessionRegistry(directory.StatePath, new SessionTestClock(Start));
        var first = registry.Create("0", null);
        for (var index = 1; index < AgentSessionRegistry.MaxSessions; index++)
        {
            registry.Create(index.ToString(System.Globalization.CultureInfo.InvariantCulture), null);
        }

        var exception = Should.Throw<AgentSessionLimitException>(() => registry.Create("one too many", null));

        exception.Limit.ShouldBe(200);
        registry.Snapshot().Count.ShouldBe(200);
        registry.Close(first.Id).ShouldBeTrue();
        registry.Create("fits now", null).Id.ShouldNotBe(first.Id, StringComparer.Ordinal);
        registry.Snapshot().Count.ShouldBe(200);
    }

    [Fact]
    public async Task ParallelCreateAndTouchKeepEveryRecordAndAValidFile()
    {
        using var directory = new SessionTestDirectory();
        var clock = new SessionTestClock(Start);
        var registry = new AgentSessionRegistry(directory.StatePath, clock);

        var workers = Enumerable.Range(0, 8).Select(worker => Task.Run(
            () =>
            {
                for (var index = 0; index < 10; index++)
                {
                    var record = registry.Create("w" + worker + "-" + index, null);
                    registry.Touch(record.Id);
                    registry.SetLifecycle(record.Id, SessionLifecycle.Running);
                    registry.Touch(record.Id);
                }
            },
            Token)).ToArray();
        await Task.WhenAll(workers).ConfigureAwait(true);

        var inMemory = registry.SnapshotRecords();
        inMemory.Count.ShouldBe(80);
        inMemory.Select(record => record.Id).Distinct(StringComparer.Ordinal).Count().ShouldBe(80);
        inMemory.ShouldAllBe(record => IdPattern().IsMatch(record.Id));
        var reopened = await AgentSessionRegistry.OpenAsync(directory.StatePath, clock, null, null, Token).ConfigureAwait(true);
        reopened.SnapshotRecords().Select(record => record.Id).Order(StringComparer.Ordinal).ToArray()
            .ShouldBe(inMemory.Select(record => record.Id).Order(StringComparer.Ordinal).ToArray());
        reopened.SnapshotRecords().ShouldAllBe(record => record.Lifecycle == SessionLifecycle.Running);
        Directory.GetFiles(directory.Path, "*.tmp").ShouldBeEmpty();
    }

    [Fact]
    public void CloseRemovesTheRecordButKeepsTheStateDirectory()
    {
        using var directory = new SessionTestDirectory();
        var registry = new AgentSessionRegistry(directory.StatePath, new SessionTestClock(Start));
        var record = registry.Create("a", null);
        Directory.CreateDirectory(record.StateDirectory);
        File.WriteAllText(Path.Combine(record.StateDirectory, "keep.txt"), "x");

        registry.Close(record.Id).ShouldBeTrue();

        registry.TryGet(record.Id, out _).ShouldBeFalse();
        File.Exists(Path.Combine(record.StateDirectory, "keep.txt")).ShouldBeTrue();
        new AgentSessionRegistry(directory.StatePath, new SessionTestClock(Start)).SnapshotRecords().ShouldBeEmpty();
    }

    [Fact]
    public void DeleteStateDirectoryRemovesOnlyTheDirectoryOfAClosedSession()
    {
        using var directory = new SessionTestDirectory();
        var registry = new AgentSessionRegistry(directory.StatePath, new SessionTestClock(Start));
        var gone = registry.Create("gone", null);
        var kept = registry.Create("kept", null);
        foreach (var record in new[] { gone, kept })
        {
            Directory.CreateDirectory(Path.Combine(record.StateDirectory, "swarm"));
            File.WriteAllText(Path.Combine(record.StateDirectory, "swarm", "live.json"), "{}");
        }

        registry.DeleteStateDirectory(gone.Id).ShouldBeFalse("a session the registry still holds keeps its directory");
        Directory.Exists(gone.StateDirectory).ShouldBeTrue();

        registry.Close(gone.Id).ShouldBeTrue();
        registry.DeleteStateDirectory(gone.Id).ShouldBeTrue();

        Directory.Exists(gone.StateDirectory).ShouldBeFalse();
        File.Exists(Path.Combine(kept.StateDirectory, "swarm", "live.json")).ShouldBeTrue();
        Directory.Exists(Path.GetDirectoryName(gone.StateDirectory)).ShouldBeTrue("the sessions directory itself stays");
        registry.DeleteStateDirectory(gone.Id).ShouldBeFalse("nothing is left to delete");
    }

    [Theory]
    [InlineData("..")]
    [InlineData(".")]
    [InlineData("../outside")]
    [InlineData("..\\outside")]
    [InlineData("a/b")]
    [InlineData("/etc")]
    [InlineData("sessions/../../outside")]
    public void DeleteStateDirectoryRefusesAnIdThatIsNotOneSafeSegment(string id)
    {
        using var directory = new SessionTestDirectory();
        var registry = new AgentSessionRegistry(directory.StatePath, new SessionTestClock(Start));
        var outside = directory.PathOf("outside");
        Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside, "precious.txt"), "x");
        var sibling = registry.Create("sibling", null);
        Directory.CreateDirectory(sibling.StateDirectory);

        registry.DeleteStateDirectory(id).ShouldBeFalse();

        File.Exists(Path.Combine(outside, "precious.txt")).ShouldBeTrue("a path escape deletes nothing");
        Directory.Exists(sibling.StateDirectory).ShouldBeTrue();
        Directory.Exists(directory.Path).ShouldBeTrue();
    }

    [Fact]
    [SupportedOSPlatform("linux")]
    public void DeleteStateDirectoryRemovesALinkWithoutFollowingIt()
    {
        using var directory = new SessionTestDirectory();
        var registry = new AgentSessionRegistry(directory.StatePath, new SessionTestClock(Start));
        var record = registry.Create("linked", null);
        var target = directory.PathOf("target");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "precious.txt"), "x");
        Directory.CreateDirectory(Path.GetDirectoryName(record.StateDirectory)!);
        Directory.CreateSymbolicLink(record.StateDirectory, target);
        registry.Close(record.Id).ShouldBeTrue();

        registry.DeleteStateDirectory(record.Id).ShouldBeTrue("the entry of that name is the link");

        Directory.Exists(record.StateDirectory).ShouldBeFalse();
        File.Exists(Path.Combine(target, "precious.txt")).ShouldBeTrue("what the link pointed at is untouched");
    }

    [Fact]
    [SupportedOSPlatform("linux")]
    public void DeleteStateDirectoryDoesNotEnterALinkInsideTheDirectory()
    {
        using var directory = new SessionTestDirectory();
        var registry = new AgentSessionRegistry(directory.StatePath, new SessionTestClock(Start));
        var record = registry.Create("inner", null);
        var target = directory.PathOf("target");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "precious.txt"), "x");
        Directory.CreateDirectory(record.StateDirectory);
        Directory.CreateSymbolicLink(Path.Combine(record.StateDirectory, "escape"), target);
        registry.Close(record.Id).ShouldBeTrue();

        registry.DeleteStateDirectory(record.Id).ShouldBeTrue();

        Directory.Exists(record.StateDirectory).ShouldBeFalse();
        File.Exists(Path.Combine(target, "precious.txt")).ShouldBeTrue("a link inside the directory is removed, not entered");
    }

    [Fact]
    [SupportedOSPlatform("linux")]
    public void DeleteStateDirectoryDeletesNothingWhenTheSessionsDirectoryIsALink()
    {
        using var directory = new SessionTestDirectory();
        var registry = new AgentSessionRegistry(directory.StatePath, new SessionTestClock(Start));
        var record = registry.Create("rooted", null);
        var elsewhere = directory.PathOf("elsewhere");
        Directory.CreateDirectory(Path.Combine(elsewhere, record.Id));
        File.WriteAllText(Path.Combine(elsewhere, record.Id, "precious.txt"), "x");
        registry.Close(record.Id).ShouldBeTrue();
        Directory.CreateSymbolicLink(Path.GetDirectoryName(record.StateDirectory)!, elsewhere);

        registry.DeleteStateDirectory(record.Id).ShouldBeFalse();

        File.Exists(Path.Combine(elsewhere, record.Id, "precious.txt")).ShouldBeTrue();
    }

    [Fact]
    public void CreatedIdsAreSafeLowercaseBase32DirectoryNamesAndBlankTitlesGetADefault()
    {
        using var directory = new SessionTestDirectory();
        var registry = new AgentSessionRegistry(directory.StatePath, new SessionTestClock(Start));

        var records = Enumerable.Range(0, 20).Select(_ => registry.Create("  ", "  ")).ToArray();

        records.ShouldAllBe(record => IdPattern().IsMatch(record.Id));
        records.Select(record => record.Id).Distinct(StringComparer.Ordinal).Count().ShouldBe(20);
        records.ShouldAllBe(record => record.Title == "New session" && record.WorkspaceRoot == null);
        registry.Create(new string('t', 500), null).Title.Length.ShouldBe(200);
        Should.Throw<ArgumentException>(() => registry.Rename(records[0].Id, " "));
    }

    [Fact]
    public async Task ResumeReferenceIsNeverPrintedLoggedOrExposedInSummariesButIsPersisted()
    {
        using var directory = new SessionTestDirectory();
        var clock = new SessionTestClock(Start);
        var logger = new SessionTestLogger();
        var registry = new AgentSessionRegistry(directory.StatePath, clock, logger);
        var record = registry.Create("Secret holder", null);
        registry.SetAcpSession(record.Id, "acp-9", Secret);
        registry.SetLifecycle(record.Id, SessionLifecycle.Running);
        registry.Changed += (sender, args) => throw new InvalidOperationException("handler failure");
        registry.Touch(record.Id);
        clock.Advance(TimeSpan.FromMinutes(5));
        registry.DetectLost();
        registry.TryGet(record.Id, out var current).ShouldBeTrue();

        var text = current.ToString() + $"{current}" + string.Join('|', registry.Snapshot().Select(summary => summary.ToString()));
        var failingLogger = new SessionTestLogger();
        var blocked = directory.PathOf("blocked.json");
        Directory.CreateDirectory(blocked);
        new AgentSessionRegistry(blocked, clock, failingLogger).Create("x", null);
        await File.WriteAllTextAsync(directory.PathOf("corrupt.json"), "corrupt " + Secret, Token);
        _ = new AgentSessionRegistry(directory.PathOf("corrupt.json"), clock, failingLogger);

        text.ShouldNotContain(Secret);
        text.ShouldContain("[redacted]");
        string.Join('\n', logger.Messages).ShouldNotContain(Secret);
        string.Join('\n', failingLogger.Messages).ShouldNotContain(Secret);
        (await File.ReadAllTextAsync(directory.StatePath, Token)).ShouldContain(Secret);
    }

    [Fact]
    public void InvalidReferencesAreRejected()
    {
        using var directory = new SessionTestDirectory();
        var registry = new AgentSessionRegistry(directory.StatePath, new SessionTestClock(Start));
        var record = registry.Create("a", null);

        Should.Throw<ArgumentException>(() => registry.SetAcpSession(record.Id, " ", "ref"));
        Should.Throw<ArgumentException>(() => registry.SetAcpSession(record.Id, "acp", new string('r', 5000)));
        registry.SetAcpSession(record.Id, "acp", null).ShouldBeTrue();
        registry.TryGet(record.Id, out var stored).ShouldBeTrue();
        stored.ResumeReference.ShouldBeNull();
    }
}
