using Shouldly;
using Xunit;

namespace Autonogrammer.Sandbox.Tests;

/// <summary>
/// Tests for <see cref="CloudSessionHost"/> against the fake cloud environment: one container and volume per session,
/// idle sleep, and transcript restore after sleep.
/// </summary>
public sealed class CloudSessionHostTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 26, 8, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan IdleWindow = TimeSpan.FromHours(2);

    [Fact]
    public async Task EachSessionGetsOneContainerAndOneVolume()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = new FakeCloudEnvironmentClient();
        using var host = CreateHost(client, new FakeTimeProvider(Start));

        var first = await host.OpenAsync("session-a", ct).ConfigureAwait(true);
        var firstAgain = await host.OpenAsync("session-a", ct).ConfigureAwait(true);
        var second = await host.OpenAsync("session-b", ct).ConfigureAwait(true);

        client.ContainersCreated.ShouldBe(2);
        client.VolumesCreated.ShouldBe(2);
        firstAgain.ContainerId.ShouldBe(first.ContainerId);
        firstAgain.VolumeId.ShouldBe(first.VolumeId);
        second.ContainerId.ShouldNotBe(first.ContainerId, StringComparer.Ordinal);
        second.VolumeId.ShouldNotBe(first.VolumeId, StringComparer.Ordinal);
        client.MountedVolumeId(first.ContainerId).ShouldBe(first.VolumeId);
        client.MountedVolumeId(second.ContainerId).ShouldBe(second.VolumeId);
        client.MountPathOf(first.ContainerId).ShouldBe("/var/omp/session");
    }

    [Fact]
    public async Task IdleSessionSleepsAfterTheConfiguredWindow()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeTimeProvider(Start);
        var client = new FakeCloudEnvironmentClient();
        using var host = CreateHost(client, clock);
        await host.OpenAsync("idle", ct).ConfigureAwait(true);
        await host.OpenAsync("busy", ct).ConfigureAwait(true);

        clock.Advance(IdleWindow - TimeSpan.FromMinutes(1));
        await host.AppendAsync("busy", "user", "still here", ct).ConfigureAwait(true);
        var beforeWindow = await host.SleepIdleAsync(ct).ConfigureAwait(true);

        clock.Advance(TimeSpan.FromMinutes(1));
        var atWindow = await host.SleepIdleAsync(ct).ConfigureAwait(true);
        var again = await host.SleepIdleAsync(ct).ConfigureAwait(true);

        beforeWindow.ShouldBeEmpty();
        atWindow.Count.ShouldBe(1);
        atWindow[0].ShouldBe("idle");
        again.ShouldBeEmpty();
        client.IdleCalls.ShouldBe(1);
        host.Get("idle")!.State.ShouldBe(CloudSessionState.Sleeping);
        host.Get("busy")!.State.ShouldBe(CloudSessionState.Running);
    }

    [Fact]
    public async Task ReattachRestoresTheTranscriptFromTheVolume()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeTimeProvider(Start);
        var client = new FakeCloudEnvironmentClient();
        using (var original = CreateHost(client, clock))
        {
            await original.OpenAsync("s1", ct).ConfigureAwait(true);
            await original.AppendAsync("s1", "user", "line one\nwith a newline and \"quotes\"", ct).ConfigureAwait(true);
            await original.AppendAsync("s1", "assistant", "reply", ct).ConfigureAwait(true);
            clock.Advance(IdleWindow);
            await original.SleepIdleAsync(ct).ConfigureAwait(true);
        }

        // A different host instance has no in-memory state: only the volume can supply the transcript.
        using var fresh = CreateHost(client, clock);
        var restore = await fresh.ReattachAsync("s1", ct).ConfigureAwait(true);

        client.EnsureAwakeCalls.ShouldBe(1);
        client.ContainersCreated.ShouldBe(1);
        client.VolumesCreated.ShouldBe(1);
        restore.Session.State.ShouldBe(CloudSessionState.Running);
        restore.Transcript[0].Role.ShouldBe("user");
        restore.Transcript[0].Text.ShouldBe("line one\nwith a newline and \"quotes\"");
        restore.Transcript[1].Role.ShouldBe("assistant");
        restore.Transcript[1].Text.ShouldBe("reply");
        restore.Transcript[0].At.ShouldBe(Start);
    }

    [Fact]
    public async Task RestoredSessionShowsTheStoredTranscriptAndAResumeMarker()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeTimeProvider(Start);
        var client = new FakeCloudEnvironmentClient();
        using var host = CreateHost(client, clock);
        await host.OpenAsync("s1", ct).ConfigureAwait(true);
        await host.AppendAsync("s1", "user", "build it", ct).ConfigureAwait(true);
        clock.Advance(IdleWindow);
        await host.SleepIdleAsync(ct).ConfigureAwait(true);
        clock.Advance(TimeSpan.FromHours(1));

        var restore = await host.ReattachAsync("s1", ct).ConfigureAwait(true);

        restore.Transcript.Count.ShouldBe(2);
        restore.Transcript[0].Text.ShouldBe("build it");
        restore.Transcript[0].IsResumeMarker.ShouldBeFalse();
        var marker = restore.ResumeMarker.ShouldNotBeNull();
        restore.Transcript[1].ShouldBe(marker);
        marker.IsResumeMarker.ShouldBeTrue();
        marker.Text.ShouldBe(CloudSessionHost.ResumeMarkerText);
        marker.At.ShouldBe(Start + IdleWindow + TimeSpan.FromHours(1));

        // The marker is stored on the volume: a second sleep and wake shows both markers after the original message.
        clock.Advance(IdleWindow);
        await host.SleepIdleAsync(ct).ConfigureAwait(true);
        var second = await host.ReattachAsync("s1", ct).ConfigureAwait(true);
        second.Transcript.Count.ShouldBe(3);
        second.Transcript[0].IsResumeMarker.ShouldBeFalse();
        second.Transcript[1].IsResumeMarker.ShouldBeTrue();
        second.Transcript[2].IsResumeMarker.ShouldBeTrue();
    }

    [Fact]
    public async Task NoProcessStateAcrossSleepIsClaimed()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeTimeProvider(Start);
        var client = new FakeCloudEnvironmentClient();
        using var host = CreateHost(client, clock);
        var opened = await host.OpenAsync("s1", ct).ConfigureAwait(true);
        client.SetProcessState(opened.ContainerId, "shell.cwd", "/work/app");
        client.ProcessStateCount(opened.ContainerId).ShouldBe(1);
        clock.Advance(IdleWindow);
        await host.SleepIdleAsync(ct).ConfigureAwait(true);

        var restore = await host.ReattachAsync("s1", ct).ConfigureAwait(true);

        client.ProcessStateCount(opened.ContainerId).ShouldBe(0);
        restore.Session.BootGeneration.ShouldBe(opened.BootGeneration + 1);
        CloudSessionRestore.ProcessStateRestored.ShouldBeFalse();
        restore.ResumeMarker!.Text.ShouldContain("not preserved");
        restore.Transcript.ShouldAllBe(e => !e.Text.Contains("process state restored", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ReattachOfARunningSessionWritesNoMarker()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = new FakeCloudEnvironmentClient();
        using var host = CreateHost(client, new FakeTimeProvider(Start));
        await host.OpenAsync("s1", ct).ConfigureAwait(true);
        await host.AppendAsync("s1", "user", "hello", ct).ConfigureAwait(true);

        var restore = await host.ReattachAsync("s1", ct).ConfigureAwait(true);

        client.EnsureAwakeCalls.ShouldBe(0);
        restore.ResumeMarker.ShouldBeNull();
        restore.Transcript.Count.ShouldBe(1);
    }

    [Fact]
    public async Task WritingToASleepingSessionIsRejectedUntilItIsReattached()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeTimeProvider(Start);
        using var host = CreateHost(new FakeCloudEnvironmentClient(), clock);
        await host.OpenAsync("s1", ct).ConfigureAwait(true);
        clock.Advance(IdleWindow);
        await host.SleepIdleAsync(ct).ConfigureAwait(true);

        await Should.ThrowAsync<InvalidOperationException>(
            () => host.AppendAsync("s1", "user", "too early", ct)).ConfigureAwait(true);
        await Should.ThrowAsync<InvalidOperationException>(
            () => host.OpenAsync("s1", ct)).ConfigureAwait(true);
    }

    [Fact]
    public async Task ReattachOfAnUnknownSessionFails()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = CreateHost(new FakeCloudEnvironmentClient(), new FakeTimeProvider(Start));

        await Should.ThrowAsync<InvalidOperationException>(
            () => host.ReattachAsync("missing", ct)).ConfigureAwait(true);
    }

    [Fact]
    public void DefaultIdleWindowIsTwentyFourHoursAndNonPositiveWindowsAreRejected()
    {
        new CloudSessionHostOptions().IdleWindow.ShouldBe(TimeSpan.FromHours(24));
        Should.Throw<ArgumentOutOfRangeException>(() =>
        {
            using var rejected = new CloudSessionHost(
                new FakeCloudEnvironmentClient(),
                new FakeTimeProvider(Start),
                new CloudSessionHostOptions { IdleWindow = TimeSpan.Zero });
        });
    }

    [Fact]
    public async Task IdleRequestMarksTheSessionSleepingAndReattachWakesOnceAndAppendsTheResumeMarker()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeTimeProvider(Start);
        var client = new FakeCloudEnvironmentClient();
        using var host = CreateHost(client, clock);
        await host.OpenAsync("s1", ct).ConfigureAwait(true);
        await host.AppendAsync("s1", "user", "before idle", ct).ConfigureAwait(true);
        clock.Advance(IdleWindow);

        var idled = await host.SleepIdleAsync(ct).ConfigureAwait(true);
        var restore = await host.ReattachAsync("s1", ct).ConfigureAwait(true);

        idled.Count.ShouldBe(1);
        idled[0].ShouldBe("s1");
        client.IdleCalls.ShouldBe(1);
        client.EnsureAwakeCalls.ShouldBe(1);
        restore.Session.State.ShouldBe(CloudSessionState.Running);
        restore.Transcript.Count.ShouldBe(2);
        restore.Transcript[0].Text.ShouldBe("before idle");
        restore.Transcript[1].IsResumeMarker.ShouldBeTrue();
        restore.ResumeMarker.ShouldBe(restore.Transcript[1]);
    }

    [Fact]
    public async Task AnIdleContainerCountsAsSleepingForOpenAndReattach()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeTimeProvider(Start);
        var inner = new FakeCloudEnvironmentClient();
        var client = new IdleReportingClient(inner);
        using var host = new CloudSessionHost(client, clock, new CloudSessionHostOptions { IdleWindow = IdleWindow });
        await host.OpenAsync("s1", ct).ConfigureAwait(true);
        clock.Advance(IdleWindow);

        var idled = await host.SleepIdleAsync(ct).ConfigureAwait(true);

        idled.Count.ShouldBe(1);
        idled[0].ShouldBe("s1");
        host.Get("s1")!.State.ShouldBe(CloudSessionState.Sleeping);
        (await client.FindContainerAsync("s1", ct).ConfigureAwait(true))!.Phase.ShouldBe(CloudContainerPhase.Idle);
        await Should.ThrowAsync<InvalidOperationException>(
            () => host.OpenAsync("s1", ct)).ConfigureAwait(true);
        await Should.ThrowAsync<InvalidOperationException>(
            () => host.AppendAsync("s1", "user", "too early", ct)).ConfigureAwait(true);

        var restore = await host.ReattachAsync("s1", ct).ConfigureAwait(true);

        inner.EnsureAwakeCalls.ShouldBe(1);
        restore.Session.State.ShouldBe(CloudSessionState.Running);
        restore.ResumeMarker.ShouldNotBeNull();
    }

    [Fact]
    public async Task AFailedWakeSurfacesTheClientExceptionLeavesTheSessionSleepingAndWritesNoMarker()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeTimeProvider(Start);
        var client = new FakeCloudEnvironmentClient();
        using var host = CreateHost(client, clock);
        await host.OpenAsync("s1", ct).ConfigureAwait(true);
        await host.AppendAsync("s1", "user", "kept", ct).ConfigureAwait(true);
        clock.Advance(IdleWindow);
        await host.SleepIdleAsync(ct).ConfigureAwait(true);
        client.FailEnsureAwakeTimes = 1;

        var failure = await Should.ThrowAsync<CloudEnvironmentException>(
            () => host.ReattachAsync("s1", ct)).ConfigureAwait(true);

        failure.Failure.ShouldBe(CloudEnvironmentFailure.WakeTimeout);
        host.Get("s1")!.State.ShouldBe(CloudSessionState.Sleeping);
        var volume = await client.FindVolumeAsync("s1", ct).ConfigureAwait(true);
        var stored = await client.ReadVolumeFileAsync(volume!.VolumeId, CloudTranscriptVolume.DefaultPath, ct).ConfigureAwait(true);
        stored!.ShouldNotContain(CloudTranscriptEntry.ResumeMarkerKind, Case.Sensitive);

        var retry = await host.ReattachAsync("s1", ct).ConfigureAwait(true);

        client.EnsureAwakeCalls.ShouldBe(2);
        retry.Transcript.Count.ShouldBe(2);
        retry.Transcript.Count(entry => entry.IsResumeMarker).ShouldBe(1);
    }

    private static CloudSessionHost CreateHost(FakeCloudEnvironmentClient client, FakeTimeProvider clock) =>
        new(client, clock, new CloudSessionHostOptions { IdleWindow = IdleWindow });

    /// <summary>Reports every sleeping container as idle, as a provider that sleeps containers later on its own would.</summary>
    private sealed class IdleReportingClient(FakeCloudEnvironmentClient inner) : ICloudEnvironmentClient
    {
        public async Task<CloudContainer?> FindContainerAsync(string sessionId, CancellationToken cancellationToken = default) =>
            AsIdle(await inner.FindContainerAsync(sessionId, cancellationToken).ConfigureAwait(true));

        public Task<CloudContainer> CreateContainerAsync(string sessionId, CancellationToken cancellationToken = default) =>
            inner.CreateContainerAsync(sessionId, cancellationToken);

        public Task<CloudVolume?> FindVolumeAsync(string sessionId, CancellationToken cancellationToken = default) =>
            inner.FindVolumeAsync(sessionId, cancellationToken);

        public Task<CloudVolume> CreateVolumeAsync(string sessionId, CancellationToken cancellationToken = default) =>
            inner.CreateVolumeAsync(sessionId, cancellationToken);

        public Task AttachVolumeAsync(string containerId, string volumeId, string mountPath, CancellationToken cancellationToken = default) =>
            inner.AttachVolumeAsync(containerId, volumeId, mountPath, cancellationToken);

        public async Task<CloudContainer> RequestIdleAsync(string containerId, CancellationToken cancellationToken = default) =>
            AsIdle(await inner.RequestIdleAsync(containerId, cancellationToken).ConfigureAwait(true))!;

        public Task<CloudContainer> EnsureAwakeAsync(string containerId, CancellationToken cancellationToken = default) =>
            inner.EnsureAwakeAsync(containerId, cancellationToken);

        public Task AppendVolumeFileAsync(string volumeId, string path, string text, CancellationToken cancellationToken = default) =>
            inner.AppendVolumeFileAsync(volumeId, path, text, cancellationToken);

        public Task<string?> ReadVolumeFileAsync(string volumeId, string path, CancellationToken cancellationToken = default) =>
            inner.ReadVolumeFileAsync(volumeId, path, cancellationToken);

        private static CloudContainer? AsIdle(CloudContainer? container) =>
            container is { Phase: CloudContainerPhase.Sleeping } ? container with { Phase = CloudContainerPhase.Idle } : container;
    }
}
