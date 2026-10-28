using System.Text.Json;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Herdr.Tests;

public sealed class HerdrSessionServiceTests
{
    private static readonly CancellationToken NoCancellation = CancellationToken.None;
    private static readonly DateTimeOffset Start = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly HerdrSessionId Alpha = new("alpha");
    private static readonly HerdrSessionId Beta = new("beta");
    private static readonly TimeSpan JustBeforeStale = TimeSpan.FromMilliseconds(44_900);
    private static readonly TimeSpan Hundredth = TimeSpan.FromMilliseconds(100);

    [Fact]
    public async Task CreatePersistsTheIdProviderAndLastKnownStatusAcrossReopen()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "herdr-sessions.json");
        var client = new FakeHerdrSessionClient();
        using var store = await HerdrSessionStore.OpenAsync(path, NoCancellation).ConfigureAwait(true);
        using var service = new HerdrSessionService(client, store, new ManualTimeProvider(Start));

        var created = await service.CreateAsync(Alpha, "local-omp", NoCancellation).ConfigureAwait(true);
        var detached = await service.DetachAsync(Alpha, NoCancellation).ConfigureAwait(true);

        using var reopened = await HerdrSessionStore.OpenAsync(path, NoCancellation).ConfigureAwait(true);
        var stored = reopened.Find(Alpha)!;
        stored.Id.ShouldBe(Alpha);
        stored.ProviderId.ShouldBe("local-omp");
        stored.LastKnownState.ShouldBe(HerdrSessionState.Running);
        stored.NativeReference.ShouldBe(created.Snapshot.NativeReference);
        stored.LastSequence.ShouldBe(detached.Snapshot.LastSequence);
        detached.Snapshot.LastSequence.ShouldBeGreaterThan(created.Snapshot.LastSequence);
    }

    [Fact]
    public async Task DetachKeepsTheSessionAliveAndDoesNotRecordItAsEnded()
    {
        using var directory = new TempDirectory();
        using var store = await HerdrSessionStore.OpenAsync(Path.Combine(directory.Path, "herdr-sessions.json"), NoCancellation).ConfigureAwait(true);
        using var service = new HerdrSessionService(new FakeHerdrSessionClient(), store, new ManualTimeProvider(Start));
        await service.CreateAsync(Alpha, "local-omp", NoCancellation).ConfigureAwait(true);

        var detached = await service.DetachAsync(Alpha, NoCancellation).ConfigureAwait(true);
        var status = await service.StatusAsync(Alpha, NoCancellation).ConfigureAwait(true);

        detached.Snapshot.IsAttached.ShouldBeFalse();
        status.Snapshot.State.ShouldBe(HerdrSessionState.Running);
        store.Find(Alpha)!.LastKnownState.ShouldBe(HerdrSessionState.Running);
    }

    [Fact]
    public async Task StaleIsReportedAfterFortyFiveSecondsOfFakeClockSilence()
    {
        using var directory = new TempDirectory();
        using var store = await HerdrSessionStore.OpenAsync(Path.Combine(directory.Path, "herdr-sessions.json"), NoCancellation).ConfigureAwait(true);
        var clock = new ManualTimeProvider(Start);
        using var service = new HerdrSessionService(new FakeHerdrSessionClient(), store, clock);
        await service.CreateAsync(Alpha, "local-omp", NoCancellation).ConfigureAwait(true);

        clock.Advance(JustBeforeStale);
        (await service.StatusAsync(Alpha, NoCancellation).ConfigureAwait(true)).IsStale.ShouldBeFalse();

        clock.Advance(Hundredth);
        (await service.StatusAsync(Alpha, NoCancellation).ConfigureAwait(true)).IsStale.ShouldBeTrue();
    }

    [Fact]
    public async Task IsStaleReportsSilenceWithoutAServerCall()
    {
        using var directory = new TempDirectory();
        using var store = await HerdrSessionStore.OpenAsync(Path.Combine(directory.Path, "herdr-sessions.json"), NoCancellation).ConfigureAwait(true);
        var client = new FakeHerdrSessionClient();
        var clock = new ManualTimeProvider(Start);
        using var service = new HerdrSessionService(client, store, clock);
        await service.CreateAsync(Alpha, "local-omp", NoCancellation).ConfigureAwait(true);
        var callsBefore = client.Transcript.Count;

        service.IsStale(Alpha).ShouldBeFalse();
        clock.Advance(HerdrHeartbeatMonitor.StaleAfter);

        service.IsStale(Alpha).ShouldBeTrue();
        client.Transcript.Count.ShouldBe(callsBefore);
    }

    [Fact]
    public async Task ABeatFromTheStreamRestartsTheStaleCount()
    {
        using var directory = new TempDirectory();
        using var store = await HerdrSessionStore.OpenAsync(Path.Combine(directory.Path, "herdr-sessions.json"), NoCancellation).ConfigureAwait(true);
        var client = new FakeHerdrSessionClient();
        var clock = new ManualTimeProvider(Start);
        using var service = new HerdrSessionService(client, store, clock);
        await service.CreateAsync(Alpha, "local-omp", NoCancellation).ConfigureAwait(true);

        clock.Advance(TimeSpan.FromSeconds(30));
        client.EmitHeartbeats();
        client.CompleteHeartbeats();
        await service.RecordHeartbeatsAsync(NoCancellation).ConfigureAwait(true);

        clock.Advance(JustBeforeStale);
        (await service.StatusAsync(Alpha, NoCancellation).ConfigureAwait(true)).IsStale.ShouldBeFalse();

        clock.Advance(Hundredth);
        (await service.StatusAsync(Alpha, NoCancellation).ConfigureAwait(true)).IsStale.ShouldBeTrue();
    }

    [Fact]
    public async Task AStateChangeSeenOnlyInTheBeatStreamIsRecorded()
    {
        using var directory = new TempDirectory();
        using var store = await HerdrSessionStore.OpenAsync(Path.Combine(directory.Path, "herdr-sessions.json"), NoCancellation).ConfigureAwait(true);
        var client = new FakeHerdrSessionClient();
        using var service = new HerdrSessionService(client, store, new ManualTimeProvider(Start));
        await service.CreateAsync(Alpha, "local-omp", NoCancellation).ConfigureAwait(true);

        await client.PauseAsync(Alpha, NoCancellation).ConfigureAwait(true);
        client.EmitHeartbeats();
        client.CompleteHeartbeats();
        await service.RecordHeartbeatsAsync(NoCancellation).ConfigureAwait(true);

        store.Find(Alpha)!.LastKnownState.ShouldBe(HerdrSessionState.Paused);
    }

    [Fact]
    public async Task ASequenceAdvanceWithTheSameStateIsRecorded()
    {
        using var directory = new TempDirectory();
        using var store = await HerdrSessionStore.OpenAsync(Path.Combine(directory.Path, "herdr-sessions.json"), NoCancellation).ConfigureAwait(true);
        var client = new FakeHerdrSessionClient();
        using var service = new HerdrSessionService(client, store, new ManualTimeProvider(Start));
        await service.CreateAsync(Alpha, "local-omp", NoCancellation).ConfigureAwait(true);

        var detached = await client.DetachAsync(Alpha, NoCancellation).ConfigureAwait(true);
        client.EmitHeartbeats();
        client.CompleteHeartbeats();
        await service.RecordHeartbeatsAsync(NoCancellation).ConfigureAwait(true);

        var stored = store.Find(Alpha)!;
        stored.LastKnownState.ShouldBe(HerdrSessionState.Running);
        stored.LastSequence.ShouldBe(detached.LastSequence);
    }

    [Fact]
    public async Task ALostRecordIsNotRevivedByABeatForASessionTheServerHoldsAlive()
    {
        using var directory = new TempDirectory();
        using var store = await HerdrSessionStore.OpenAsync(Path.Combine(directory.Path, "herdr-sessions.json"), NoCancellation).ConfigureAwait(true);
        var client = new FakeHerdrSessionClient();
        using var service = new HerdrSessionService(client, store, new ManualTimeProvider(Start));
        await service.CreateAsync(Alpha, "local-omp", NoCancellation).ConfigureAwait(true);
        client.SimulateServerRestart();
        var lost = await service.StatusAsync(Alpha, NoCancellation).ConfigureAwait(true);

        // The server resumes the session on its own; the store is not told, so it still records the session as lost.
        await client.CreateAsync(new HerdrCreateRequest(Alpha, "local-omp", lost.Snapshot.NativeReference), NoCancellation).ConfigureAwait(true);
        client.EmitHeartbeats();
        client.CompleteHeartbeats();
        await service.RecordHeartbeatsAsync(NoCancellation).ConfigureAwait(true);

        (await client.StatusAsync(Alpha, NoCancellation).ConfigureAwait(true)).State.ShouldBe(HerdrSessionState.Running);
        store.Find(Alpha)!.LastKnownState.ShouldBe(HerdrSessionState.Lost);
    }

    [Fact]
    public async Task ATerminateThatRacesABeatLeavesTheSessionTerminated()
    {
        using var directory = new TempDirectory();
        using var store = await HerdrSessionStore.OpenAsync(Path.Combine(directory.Path, "herdr-sessions.json"), NoCancellation).ConfigureAwait(true);
        var client = new FakeHerdrSessionClient();
        using var service = new HerdrSessionService(client, store, new ManualTimeProvider(Start));
        await service.CreateAsync(Alpha, "local-omp", NoCancellation).ConfigureAwait(true);

        // The beat is read while the session is running. The terminate is recorded before the beat is delivered, so the
        // delivered beat must not overwrite the terminated record.
        client.RunBeforeNextBeat(() => service.TerminateAsync(Alpha, NoCancellation));
        client.EmitHeartbeats();
        client.CompleteHeartbeats();
        await service.RecordHeartbeatsAsync(NoCancellation).ConfigureAwait(true);

        store.Find(Alpha)!.LastKnownState.ShouldBe(HerdrSessionState.Terminated);
    }

    [Fact]
    public async Task AfterAServerRestartRestoreRecordsLostSessionsWithTheirNativeReferences()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "herdr-sessions.json");
        var client = new FakeHerdrSessionClient();
        using (var store = await HerdrSessionStore.OpenAsync(path, NoCancellation).ConfigureAwait(true))
        {
            using var service = new HerdrSessionService(client, store, new ManualTimeProvider(Start));
            await service.CreateAsync(Alpha, "local-omp", NoCancellation).ConfigureAwait(true);
            await service.CreateAsync(Beta, "local-omp", NoCancellation).ConfigureAwait(true);
            await service.PauseAsync(Beta, NoCancellation).ConfigureAwait(true);
        }

        client.SimulateServerRestart();
        using var reopened = await HerdrSessionStore.OpenAsync(path, NoCancellation).ConfigureAwait(true);
        using var restarted = new HerdrSessionService(client, reopened, new ManualTimeProvider(Start));
        var views = await restarted.RestoreAsync(NoCancellation).ConfigureAwait(true);

        views.Select(view => view.Snapshot.Id).ShouldBe(new[] { Alpha, Beta });
        views.All(view => view.Snapshot.State == HerdrSessionState.Lost).ShouldBeTrue();
        views.All(view => !view.Snapshot.IsAttached).ShouldBeTrue();
        views.All(view => string.Equals(view.ProviderId, "local-omp", StringComparison.Ordinal)).ShouldBeTrue();
        reopened.Find(Alpha)!.LastKnownState.ShouldBe(HerdrSessionState.Lost);
        reopened.Find(Beta)!.NativeReference.ShouldBe(views[1].Snapshot.NativeReference);
        reopened.Find(Beta)!.NativeReference.ShouldNotBeNull();
    }

    [Fact]
    public async Task ALostSessionIsNotRunningUntilItResumesFromItsNativeReference()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "herdr-sessions.json");
        var client = new FakeHerdrSessionClient();
        using (var store = await HerdrSessionStore.OpenAsync(path, NoCancellation).ConfigureAwait(true))
        {
            using var service = new HerdrSessionService(client, store, new ManualTimeProvider(Start));
            await service.CreateAsync(Alpha, "local-omp", NoCancellation).ConfigureAwait(true);
        }

        client.SimulateServerRestart();
        using var reopened = await HerdrSessionStore.OpenAsync(path, NoCancellation).ConfigureAwait(true);
        using var restarted = new HerdrSessionService(client, reopened, new ManualTimeProvider(Start));
        var before = (await restarted.RestoreAsync(NoCancellation).ConfigureAwait(true)).Single();
        var nativeReference = before.Snapshot.NativeReference;

        var lostStatus = await restarted.StatusAsync(Alpha, NoCancellation).ConfigureAwait(true);
        var resumed = await restarted.ResumeFromNativeReferenceAsync(Alpha, NoCancellation).ConfigureAwait(true);

        before.Snapshot.State.ShouldBe(HerdrSessionState.Lost);
        lostStatus.Snapshot.State.ShouldBe(HerdrSessionState.Lost);
        resumed.Snapshot.State.ShouldBe(HerdrSessionState.Running);
        resumed.Snapshot.NativeReference.ShouldBe(nativeReference);
        reopened.Find(Alpha)!.LastKnownState.ShouldBe(HerdrSessionState.Running);
        reopened.Find(Alpha)!.NativeReference.ShouldBe(nativeReference);
    }

    [Fact]
    public async Task ResumingFromNativeReferenceIsRejectedForASessionThatIsNotLost()
    {
        using var directory = new TempDirectory();
        using var store = await HerdrSessionStore.OpenAsync(Path.Combine(directory.Path, "herdr-sessions.json"), NoCancellation).ConfigureAwait(true);
        using var service = new HerdrSessionService(new FakeHerdrSessionClient(), store, new ManualTimeProvider(Start));
        await service.CreateAsync(Alpha, "local-omp", NoCancellation).ConfigureAwait(true);

        await Should.ThrowAsync<HerdrInvalidTransitionException>(async () =>
        {
            await service.ResumeFromNativeReferenceAsync(Alpha, NoCancellation).ConfigureAwait(true);
        }).ConfigureAwait(true);
    }

    [Fact]
    public async Task RestoreReattachesALiveSessionByItsStoredId()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "herdr-sessions.json");
        var client = new FakeHerdrSessionClient();
        using (var store = await HerdrSessionStore.OpenAsync(path, NoCancellation).ConfigureAwait(true))
        {
            using var service = new HerdrSessionService(client, store, new ManualTimeProvider(Start));
            await service.CreateAsync(Alpha, "local-omp", NoCancellation).ConfigureAwait(true);
            await service.DetachAsync(Alpha, NoCancellation).ConfigureAwait(true);
        }

        using var reopened = await HerdrSessionStore.OpenAsync(path, NoCancellation).ConfigureAwait(true);
        using var restarted = new HerdrSessionService(client, reopened, new ManualTimeProvider(Start));
        var view = (await restarted.RestoreAsync(NoCancellation).ConfigureAwait(true)).Single();

        view.Snapshot.State.ShouldBe(HerdrSessionState.Running);
        view.Snapshot.IsAttached.ShouldBeTrue();
        client.Transcript.Any(entry => string.Equals(entry.Operation, "Attach", StringComparison.Ordinal) && string.Equals(entry.SessionId, "alpha", StringComparison.Ordinal)).ShouldBeTrue();
    }

    [Fact]
    public async Task ASessionThatLapsesBetweenItsStatusReadAndItsAttachIsRecordedLostWhileTheOthersRestore()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "herdr-sessions.json");
        var client = new FakeHerdrSessionClient();
        using (var store = await HerdrSessionStore.OpenAsync(path, NoCancellation).ConfigureAwait(true))
        {
            using var service = new HerdrSessionService(client, store, new ManualTimeProvider(Start));
            await service.CreateAsync(Alpha, "local-omp", NoCancellation).ConfigureAwait(true);
            await service.CreateAsync(Beta, "local-omp", NoCancellation).ConfigureAwait(true);
        }

        using var reopened = await HerdrSessionStore.OpenAsync(path, NoCancellation).ConfigureAwait(true);
        using var restarted = new HerdrSessionService(client, reopened, new ManualTimeProvider(Start));
        client.RestartBeforeNextAttach();

        var views = await restarted.RestoreAsync(NoCancellation).ConfigureAwait(true);

        views.Select(view => view.Snapshot.Id).ShouldBe(new[] { Alpha, Beta });
        views.All(view => view.Snapshot.State == HerdrSessionState.Lost).ShouldBeTrue();
        client.Transcript.Any(entry => string.Equals(entry.Operation, "Attach", StringComparison.Ordinal) && string.Equals(entry.Outcome, nameof(HerdrInvalidTransitionException), StringComparison.Ordinal)).ShouldBeTrue();
        reopened.Find(Alpha)!.LastKnownState.ShouldBe(HerdrSessionState.Lost);
        reopened.Find(Beta)!.LastKnownState.ShouldBe(HerdrSessionState.Lost);
    }

    [Fact]
    public async Task ARecordTheServerNoLongerKnowsIsRestoredAsLostAndResumesFromItsReference()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "herdr-sessions.json");
        await File.WriteAllTextAsync(path, StoreJson(sessionId: "orphan", nativeReference: "omp-orphan", state: "Running"), NoCancellation).ConfigureAwait(true);
        using var store = await HerdrSessionStore.OpenAsync(path, NoCancellation).ConfigureAwait(true);
        var client = new FakeHerdrSessionClient();
        using var service = new HerdrSessionService(client, store, new ManualTimeProvider(Start));

        var view = (await service.RestoreAsync(NoCancellation).ConfigureAwait(true)).Single();
        var resumed = await service.ResumeFromNativeReferenceAsync(new HerdrSessionId("orphan"), NoCancellation).ConfigureAwait(true);

        view.Snapshot.State.ShouldBe(HerdrSessionState.Lost);
        view.Snapshot.NativeReference.ShouldBe(new NativeSessionReference("omp-orphan"));
        resumed.Snapshot.State.ShouldBe(HerdrSessionState.Running);
        resumed.Snapshot.NativeReference.ShouldBe(new NativeSessionReference("omp-orphan"));
    }

    [Fact]
    public async Task ALostRecordWithoutANativeReferenceCannotBeResumed()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "herdr-sessions.json");
        await File.WriteAllTextAsync(path, StoreJson(sessionId: "orphan", nativeReference: null, state: "Running"), NoCancellation).ConfigureAwait(true);
        using var store = await HerdrSessionStore.OpenAsync(path, NoCancellation).ConfigureAwait(true);
        var client = new FakeHerdrSessionClient();
        using var service = new HerdrSessionService(client, store, new ManualTimeProvider(Start));

        var view = (await service.RestoreAsync(NoCancellation).ConfigureAwait(true)).Single();

        view.Snapshot.State.ShouldBe(HerdrSessionState.Lost);
        await Should.ThrowAsync<HerdrInvalidTransitionException>(async () =>
        {
            await service.ResumeFromNativeReferenceAsync(new HerdrSessionId("orphan"), NoCancellation).ConfigureAwait(true);
        }).ConfigureAwait(true);
        client.Transcript.Where(entry => string.Equals(entry.Operation, "Create", StringComparison.Ordinal)).ShouldBeEmpty();
    }

    [Fact]
    public async Task ATerminatedSessionIsRestoredWithoutContactingTheServer()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "herdr-sessions.json");
        var client = new FakeHerdrSessionClient();
        using (var store = await HerdrSessionStore.OpenAsync(path, NoCancellation).ConfigureAwait(true))
        {
            using var service = new HerdrSessionService(client, store, new ManualTimeProvider(Start));
            await service.CreateAsync(Alpha, "local-omp", NoCancellation).ConfigureAwait(true);
            await service.TerminateAsync(Alpha, NoCancellation).ConfigureAwait(true);
        }

        using var reopened = await HerdrSessionStore.OpenAsync(path, NoCancellation).ConfigureAwait(true);
        using var restarted = new HerdrSessionService(client, reopened, new ManualTimeProvider(Start));
        var view = (await restarted.RestoreAsync(NoCancellation).ConfigureAwait(true)).Single();

        view.Snapshot.State.ShouldBe(HerdrSessionState.Terminated);
        client.Transcript
            .Where(entry => entry.Operation is "Status" or "Attach")
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task OperationsOnAnUnrecordedSessionAreNotFound()
    {
        using var directory = new TempDirectory();
        using var store = await HerdrSessionStore.OpenAsync(Path.Combine(directory.Path, "herdr-sessions.json"), NoCancellation).ConfigureAwait(true);
        using var service = new HerdrSessionService(new FakeHerdrSessionClient(), store, new ManualTimeProvider(Start));

        await Should.ThrowAsync<HerdrSessionNotFoundException>(async () =>
        {
            await service.DetachAsync(new HerdrSessionId("missing"), NoCancellation).ConfigureAwait(true);
        }).ConfigureAwait(true);
    }

    [Fact]
    public async Task CreatingAnIdentifierAlreadyRecordedIsAConflict()
    {
        using var directory = new TempDirectory();
        using var store = await HerdrSessionStore.OpenAsync(Path.Combine(directory.Path, "herdr-sessions.json"), NoCancellation).ConfigureAwait(true);
        using var service = new HerdrSessionService(new FakeHerdrSessionClient(), store, new ManualTimeProvider(Start));
        await service.CreateAsync(Alpha, "local-omp", NoCancellation).ConfigureAwait(true);

        await Should.ThrowAsync<HerdrSessionConflictException>(async () =>
        {
            await service.CreateAsync(Alpha, "local-omp", NoCancellation).ConfigureAwait(true);
        }).ConfigureAwait(true);
    }

    [Fact]
    public async Task ConcurrentCreatesOfOneIdentifierReachTheServerOnce()
    {
        using var directory = new TempDirectory();
        using var store = await HerdrSessionStore.OpenAsync(Path.Combine(directory.Path, "herdr-sessions.json"), NoCancellation).ConfigureAwait(true);
        var client = new FakeHerdrSessionClient();
        using var service = new HerdrSessionService(client, store, new ManualTimeProvider(Start));

        var outcomes = await Task.WhenAll(AttemptCreateAsync(service), AttemptCreateAsync(service)).ConfigureAwait(true);

        outcomes.Count(outcome => outcome is null).ShouldBe(1);
        outcomes.Count(outcome => outcome is HerdrSessionConflictException).ShouldBe(1);
        client.Transcript.Count(entry => string.Equals(entry.Operation, "Create", StringComparison.Ordinal)).ShouldBe(1);
    }

    [Fact]
    public async Task ACreateWhoseRecordCannotBeStoredEndsTheServerSessionAndRethrowsTheFailure()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "herdr-sessions.json");
        using var store = await HerdrSessionStore.OpenAsync(path, NoCancellation).ConfigureAwait(true);
        // The store file's path is now a directory, so the store cannot replace it and the record cannot be written.
        Directory.CreateDirectory(path);
        var client = new FakeHerdrSessionClient();
        using var service = new HerdrSessionService(client, store, new ManualTimeProvider(Start));

        var failure = await StoreFileFailure.CaptureAsync(async () =>
        {
            await service.CreateAsync(Alpha, "local-omp", NoCancellation).ConfigureAwait(true);
        }).ConfigureAwait(true);

        (failure is IOException or UnauthorizedAccessException).ShouldBeTrue();
        (await client.StatusAsync(Alpha, NoCancellation).ConfigureAwait(true)).State.ShouldBe(HerdrSessionState.Terminated);
        store.Find(Alpha).ShouldBeNull();
    }

    [Fact]
    public async Task AnUnreadableStoreStopsTheServiceFromStartingWithoutResetting()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "herdr-sessions.json");
        var original = "not a store"u8.ToArray();
        await File.WriteAllBytesAsync(path, original, NoCancellation).ConfigureAwait(true);

        await Should.ThrowAsync<JsonException>(async () =>
        {
            using var store = await HerdrSessionStore.OpenAsync(path, NoCancellation).ConfigureAwait(true);
        }).ConfigureAwait(true);

        (await File.ReadAllBytesAsync(path, NoCancellation).ConfigureAwait(true)).ShouldBe(original);
    }

    [Fact]
    public async Task ATerminateThatFailsDuringCompensationStillRethrowsThePersistFailure()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "herdr-sessions.json");
        using var store = await HerdrSessionStore.OpenAsync(path, NoCancellation).ConfigureAwait(true);
        // The store file's path is a directory, so the record cannot be written, and the server session must be ended.
        Directory.CreateDirectory(path);
        var client = new FakeHerdrSessionClient();
        using var service = new HerdrSessionService(client, store, new ManualTimeProvider(Start));
        client.FailNextTerminate();

        await StoreFileFailure.CaptureAsync(async () =>
        {
            await service.CreateAsync(Alpha, "local-omp", NoCancellation).ConfigureAwait(true);
        }).ConfigureAwait(true);

        // The refused terminate is recorded, and the persist failure above is the exception that surfaced.
        client.Transcript.Single(entry => string.Equals(entry.Operation, "Terminate", StringComparison.Ordinal)).Outcome.ShouldBe(nameof(HerdrInvalidTransitionException));
        (await client.StatusAsync(Alpha, NoCancellation).ConfigureAwait(true)).State.ShouldBe(HerdrSessionState.Running);
    }

    [Fact]
    public async Task AResumeWhoseRecordCannotBeStoredEndsTheServerSessionAndRethrowsTheFailure()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "herdr-sessions.json");
        var client = new FakeHerdrSessionClient();
        using (var store = await HerdrSessionStore.OpenAsync(path, NoCancellation).ConfigureAwait(true))
        {
            using var service = new HerdrSessionService(client, store, new ManualTimeProvider(Start));
            await service.CreateAsync(Alpha, "local-omp", NoCancellation).ConfigureAwait(true);
        }

        client.SimulateServerRestart();
        using var reopened = await HerdrSessionStore.OpenAsync(path, NoCancellation).ConfigureAwait(true);
        using var restarted = new HerdrSessionService(client, reopened, new ManualTimeProvider(Start));
        await restarted.RestoreAsync(NoCancellation).ConfigureAwait(true);
        reopened.Find(Alpha)!.LastKnownState.ShouldBe(HerdrSessionState.Lost);

        // The store file is replaced by a directory, so the record cannot be written once the server resumes the session.
        File.Delete(path);
        Directory.CreateDirectory(path);

        await StoreFileFailure.CaptureAsync(async () =>
        {
            await restarted.ResumeFromNativeReferenceAsync(Alpha, NoCancellation).ConfigureAwait(true);
        }).ConfigureAwait(true);

        (await client.StatusAsync(Alpha, NoCancellation).ConfigureAwait(true)).State.ShouldBe(HerdrSessionState.Terminated);
        reopened.Find(Alpha)!.LastKnownState.ShouldBe(HerdrSessionState.Lost);
    }

    private static async Task<Exception?> AttemptCreateAsync(HerdrSessionService service)
    {
        try
        {
            await service.CreateAsync(Alpha, "local-omp", NoCancellation).ConfigureAwait(true);
            return null;
        }
        catch (HerdrSessionConflictException failure)
        {
            return failure;
        }
    }

    private static string StoreJson(string sessionId, string? nativeReference, string state)
    {
        var native = nativeReference is null ? "null" : $"\"{nativeReference}\"";
        return $"{{\"version\":1,\"sessions\":[{{\"sessionId\":\"{sessionId}\",\"providerId\":\"local-omp\",\"nativeReference\":{native},\"lastKnownState\":\"{state}\",\"lastSequence\":3}}]}}";
    }
}
