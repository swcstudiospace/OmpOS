using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Herdr.Tests;

public sealed class FakeHerdrSessionClientTests
{
    private static readonly CancellationToken NoCancellation = CancellationToken.None;
    private static readonly string[] ExpectedLiveBeatIds = ["epsilon", "gamma"];

    private static HerdrCreateRequest Request(string id, NativeSessionReference? native = null) =>
        new(new HerdrSessionId(id), "local-omp", native);

    private static HerdrSessionId Id(string value) => new(value);

    [Fact]
    public async Task DetachLeavesTheSessionAliveAndFindable()
    {
        var client = new FakeHerdrSessionClient();
        var created = await client.CreateAsync(Request("alpha"), NoCancellation).ConfigureAwait(true);

        var detached = await client.DetachAsync(created.Id, NoCancellation).ConfigureAwait(true);
        var status = await client.StatusAsync(created.Id, NoCancellation).ConfigureAwait(true);

        detached.IsAttached.ShouldBeFalse();
        status.State.ShouldBe(HerdrSessionState.Running);
        status.IsAttached.ShouldBeFalse();
    }

    [Fact]
    public async Task AfterAServerRestartListReportsLiveSessionsAsLostWithTheirNativeReferences()
    {
        var client = new FakeHerdrSessionClient();
        var alpha = await client.CreateAsync(Request("alpha"), NoCancellation).ConfigureAwait(true);
        var beta = await client.CreateAsync(Request("beta"), NoCancellation).ConfigureAwait(true);
        await client.PauseAsync(beta.Id, NoCancellation).ConfigureAwait(true);

        client.SimulateServerRestart();
        var listed = await client.ListAsync(NoCancellation).ConfigureAwait(true);

        listed.Count.ShouldBe(2);
        var lostAlpha = listed.Single(snapshot => snapshot.Id == alpha.Id);
        lostAlpha.State.ShouldBe(HerdrSessionState.Lost);
        lostAlpha.IsAttached.ShouldBeFalse();
        lostAlpha.NativeReference.ShouldBe(alpha.NativeReference);
        lostAlpha.LastSequence.ShouldBeGreaterThan(alpha.LastSequence);

        var lostBeta = listed.Single(snapshot => snapshot.Id == beta.Id);
        lostBeta.State.ShouldBe(HerdrSessionState.Lost);
        lostBeta.NativeReference.ShouldBe(beta.NativeReference);
    }

    [Fact]
    public async Task LostSessionIsNeverReportedAsRunning()
    {
        var client = new FakeHerdrSessionClient();
        await client.CreateAsync(Request("alpha"), NoCancellation).ConfigureAwait(true);
        await client.CreateAsync(Request("beta"), NoCancellation).ConfigureAwait(true);

        client.SimulateServerRestart();
        var status = await client.StatusAsync(Id("alpha"), NoCancellation).ConfigureAwait(true);
        var listed = await client.ListAsync(NoCancellation).ConfigureAwait(true);

        status.State.ShouldNotBe(HerdrSessionState.Running);
        listed.Where(snapshot => snapshot.State == HerdrSessionState.Running).ShouldBeEmpty();
    }

    [Fact]
    public async Task LostSessionResumesFromItsNativeReference()
    {
        var client = new FakeHerdrSessionClient();
        var created = await client.CreateAsync(Request("alpha"), NoCancellation).ConfigureAwait(true);
        client.SimulateServerRestart();

        var resumed = await client.CreateAsync(Request("alpha", created.NativeReference), NoCancellation).ConfigureAwait(true);
        var status = await client.StatusAsync(Id("alpha"), NoCancellation).ConfigureAwait(true);

        resumed.State.ShouldBe(HerdrSessionState.Running);
        resumed.NativeReference.ShouldBe(created.NativeReference);
        status.State.ShouldBe(HerdrSessionState.Running);
    }

    [Fact]
    public async Task CreatingAnIdentifierHeldByALiveSessionIsAConflict()
    {
        var client = new FakeHerdrSessionClient();
        await client.CreateAsync(Request("alpha"), NoCancellation).ConfigureAwait(true);

        await Should.ThrowAsync<HerdrSessionConflictException>(async () =>
        {
            await client.CreateAsync(Request("alpha"), NoCancellation).ConfigureAwait(true);
        }).ConfigureAwait(true);
    }

    [Fact]
    public async Task ResumingALostSessionWithADifferentNativeReferenceIsAConflict()
    {
        var client = new FakeHerdrSessionClient();
        await client.CreateAsync(Request("alpha"), NoCancellation).ConfigureAwait(true);
        client.SimulateServerRestart();

        await Should.ThrowAsync<HerdrSessionConflictException>(async () =>
        {
            await client.CreateAsync(Request("alpha", new NativeSessionReference("some-other-reference")), NoCancellation).ConfigureAwait(true);
        }).ConfigureAwait(true);
    }

    [Fact]
    public async Task PauseAndResumeMoveBetweenRunningAndPaused()
    {
        var client = new FakeHerdrSessionClient();
        var created = await client.CreateAsync(Request("alpha"), NoCancellation).ConfigureAwait(true);

        var paused = await client.PauseAsync(created.Id, NoCancellation).ConfigureAwait(true);
        var resumed = await client.ResumeAsync(created.Id, NoCancellation).ConfigureAwait(true);

        paused.State.ShouldBe(HerdrSessionState.Paused);
        resumed.State.ShouldBe(HerdrSessionState.Running);
    }

    [Fact]
    public async Task ResumeOfARunningSessionIsAnInvalidTransition()
    {
        var client = new FakeHerdrSessionClient();
        var created = await client.CreateAsync(Request("alpha"), NoCancellation).ConfigureAwait(true);

        await Should.ThrowAsync<HerdrInvalidTransitionException>(async () =>
        {
            await client.ResumeAsync(created.Id, NoCancellation).ConfigureAwait(true);
        }).ConfigureAwait(true);
    }

    [Fact]
    public async Task LostSessionCannotBePausedOrAttached()
    {
        var client = new FakeHerdrSessionClient();
        var created = await client.CreateAsync(Request("alpha"), NoCancellation).ConfigureAwait(true);
        client.SimulateServerRestart();

        await Should.ThrowAsync<HerdrInvalidTransitionException>(async () =>
        {
            await client.PauseAsync(created.Id, NoCancellation).ConfigureAwait(true);
        }).ConfigureAwait(true);
        await Should.ThrowAsync<HerdrInvalidTransitionException>(async () =>
        {
            await client.AttachAsync(created.Id, 0, NoCancellation).ConfigureAwait(true);
        }).ConfigureAwait(true);
    }

    [Fact]
    public async Task TerminatedSessionCannotChangeAgainAndSurvivesARestartAsTerminated()
    {
        var client = new FakeHerdrSessionClient();
        var created = await client.CreateAsync(Request("alpha"), NoCancellation).ConfigureAwait(true);
        await client.TerminateAsync(created.Id, NoCancellation).ConfigureAwait(true);
        client.SimulateServerRestart();

        var listed = await client.ListAsync(NoCancellation).ConfigureAwait(true);

        listed.Single().State.ShouldBe(HerdrSessionState.Terminated);
        await Should.ThrowAsync<HerdrInvalidTransitionException>(async () =>
        {
            await client.TerminateAsync(created.Id, NoCancellation).ConfigureAwait(true);
        }).ConfigureAwait(true);
        await Should.ThrowAsync<HerdrSessionConflictException>(async () =>
        {
            await client.CreateAsync(Request("alpha", created.NativeReference), NoCancellation).ConfigureAwait(true);
        }).ConfigureAwait(true);
    }

    [Fact]
    public async Task AnUnknownSessionIsNotFound()
    {
        var client = new FakeHerdrSessionClient();

        await Should.ThrowAsync<HerdrSessionNotFoundException>(async () =>
        {
            await client.StatusAsync(Id("missing"), NoCancellation).ConfigureAwait(true);
        }).ConfigureAwait(true);
    }

    [Fact]
    public async Task AttachRejectsASequenceOutsideTheSessionsRecordedRange()
    {
        var client = new FakeHerdrSessionClient();
        var created = await client.CreateAsync(Request("alpha"), NoCancellation).ConfigureAwait(true);

        var attached = await client.AttachAsync(created.Id, created.LastSequence, NoCancellation).ConfigureAwait(true);

        attached.LastSequence.ShouldBe(created.LastSequence);
        await Should.ThrowAsync<ArgumentOutOfRangeException>(async () =>
        {
            await client.AttachAsync(created.Id, created.LastSequence + 1, NoCancellation).ConfigureAwait(true);
        }).ConfigureAwait(true);
        await Should.ThrowAsync<ArgumentOutOfRangeException>(async () =>
        {
            await client.AttachAsync(created.Id, -1, NoCancellation).ConfigureAwait(true);
        }).ConfigureAwait(true);
    }

    [Fact]
    public async Task TheTranscriptRecordsEachCallWithItsOutcomeInOrder()
    {
        var client = new FakeHerdrSessionClient();
        await client.CreateAsync(Request("alpha"), NoCancellation).ConfigureAwait(true);
        await client.DetachAsync(Id("alpha"), NoCancellation).ConfigureAwait(true);
        await Should.ThrowAsync<HerdrSessionNotFoundException>(async () =>
        {
            await client.PauseAsync(Id("missing"), NoCancellation).ConfigureAwait(true);
        }).ConfigureAwait(true);

        client.Transcript
            .Select(entry => (entry.Operation, entry.SessionId, entry.Outcome))
            .ShouldBe(new[]
            {
                ("Create", (string?)"alpha", "Running"),
                ("Detach", (string?)"alpha", "Running"),
                ("Pause", (string?)"missing", nameof(HerdrSessionNotFoundException)),
            });
    }

    [Fact]
    public async Task HeartbeatsCoverOnlyLiveSessions()
    {
        var client = new FakeHerdrSessionClient();
        await client.CreateAsync(Request("alpha"), NoCancellation).ConfigureAwait(true);
        await client.CreateAsync(Request("beta"), NoCancellation).ConfigureAwait(true);
        client.SimulateServerRestart();
        await client.CreateAsync(Request("gamma"), NoCancellation).ConfigureAwait(true);
        await client.CreateAsync(Request("delta"), NoCancellation).ConfigureAwait(true);
        await client.TerminateAsync(Id("delta"), NoCancellation).ConfigureAwait(true);
        await client.CreateAsync(Request("epsilon"), NoCancellation).ConfigureAwait(true);
        await client.PauseAsync(Id("epsilon"), NoCancellation).ConfigureAwait(true);

        client.EmitHeartbeats();
        client.CompleteHeartbeats();
        var beats = await CollectBeatsAsync(client).ConfigureAwait(true);

        beats.Select(beat => beat.Id.Value).Order(StringComparer.Ordinal).ShouldBe(ExpectedLiveBeatIds);
        beats.Single(beat => beat.Id == Id("gamma")).State.ShouldBe(HerdrSessionState.Running);
    }

    [Fact]
    public async Task TheHeartbeatStreamEndsWhenTheFakeEndsIt()
    {
        var client = new FakeHerdrSessionClient();
        var created = await client.CreateAsync(Request("alpha"), NoCancellation).ConfigureAwait(true);
        client.EmitHeartbeats();

        var beats = client.HeartbeatsAsync(TestContext.Current.CancellationToken).GetAsyncEnumerator(TestContext.Current.CancellationToken);
        try
        {
            (await beats.MoveNextAsync().ConfigureAwait(true)).ShouldBeTrue();
            beats.Current.Id.ShouldBe(created.Id);

            client.CompleteHeartbeats();
            (await beats.MoveNextAsync().ConfigureAwait(true)).ShouldBeFalse();
        }
        finally
        {
            await beats.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task ABeatQueuedBeforeARestartIsNotDeliveredAsRunning()
    {
        var client = new FakeHerdrSessionClient();
        await client.CreateAsync(Request("alpha"), NoCancellation).ConfigureAwait(true);
        client.EmitHeartbeats();
        client.SimulateServerRestart();
        client.CompleteHeartbeats();

        var beats = await CollectBeatsAsync(client).ConfigureAwait(true);

        beats.Any(beat => beat.Id == Id("alpha") && beat.State == HerdrSessionState.Running).ShouldBeFalse();
    }

    [Fact]
    public async Task ABeatQueuedBeforeATerminateIsNotDelivered()
    {
        var client = new FakeHerdrSessionClient();
        var created = await client.CreateAsync(Request("alpha"), NoCancellation).ConfigureAwait(true);
        client.EmitHeartbeats();
        await client.TerminateAsync(created.Id, NoCancellation).ConfigureAwait(true);
        client.CompleteHeartbeats();

        var beats = await CollectBeatsAsync(client).ConfigureAwait(true);

        beats.ShouldBeEmpty();
    }

    private static async Task<List<HerdrHeartbeat>> CollectBeatsAsync(FakeHerdrSessionClient client)
    {
        var beats = new List<HerdrHeartbeat>();
        var enumerator = client.HeartbeatsAsync(NoCancellation).GetAsyncEnumerator();
        try
        {
            while (await enumerator.MoveNextAsync().ConfigureAwait(true))
            {
                beats.Add(enumerator.Current);
            }
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(true);
        }

        return beats;
    }
}
