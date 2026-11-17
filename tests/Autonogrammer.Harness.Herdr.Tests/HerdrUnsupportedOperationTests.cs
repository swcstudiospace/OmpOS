using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Herdr.Tests;

/// <summary>
/// The service refuses the optional operations the server does not support, without calling the server. No herdr process is started.
/// </summary>
public sealed class HerdrUnsupportedOperationTests
{
    private static readonly CancellationToken NoCancellation = CancellationToken.None;
    private static readonly DateTimeOffset Start = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly HerdrSessionId Alpha = new("alpha");

    [Fact]
    public async Task ServiceRefusesPauseResumeAttachAndDetachWithoutCallingTheClient()
    {
        using var directory = new TempDirectory();
        using var store = await HerdrSessionStore.OpenAsync(Path.Combine(directory.Path, "herdr-sessions.json"), NoCancellation).ConfigureAwait(true);
        var client = new NoOptionalOperationsClient();
        using var service = new HerdrSessionService(client, store, new ManualTimeProvider(Start));
        await service.CreateAsync(Alpha, "local-omp", NoCancellation).ConfigureAwait(true);
        int callsBefore = client.Calls.Count;

        var pause = await Should.ThrowAsync<HerdrOperationUnsupportedException>(
            () => service.PauseAsync(Alpha, NoCancellation)).ConfigureAwait(true);
        var resume = await Should.ThrowAsync<HerdrOperationUnsupportedException>(
            () => service.ResumeAsync(Alpha, NoCancellation)).ConfigureAwait(true);
        var attach = await Should.ThrowAsync<HerdrOperationUnsupportedException>(
            () => service.AttachAsync(Alpha, 0, NoCancellation)).ConfigureAwait(true);
        var detach = await Should.ThrowAsync<HerdrOperationUnsupportedException>(
            () => service.DetachAsync(Alpha, NoCancellation)).ConfigureAwait(true);

        pause.Operation.ShouldBe("Pause");
        resume.Operation.ShouldBe("Resume");
        attach.Operation.ShouldBe("Attach");
        detach.Operation.ShouldBe("Detach");
        client.Calls.Count.ShouldBe(callsBefore);
        store.Find(Alpha)!.LastKnownState.ShouldBe(HerdrSessionState.Running);
    }

    [Fact]
    public async Task RestoreReportsALiveSessionUnattachedAndNeverAttachesIt()
    {
        using var directory = new TempDirectory();
        using var store = await HerdrSessionStore.OpenAsync(Path.Combine(directory.Path, "herdr-sessions.json"), NoCancellation).ConfigureAwait(true);
        var client = new NoOptionalOperationsClient();
        using (var created = new HerdrSessionService(client, store, new ManualTimeProvider(Start)))
        {
            await created.CreateAsync(Alpha, "local-omp", NoCancellation).ConfigureAwait(true);
        }

        client.Calls.Clear();
        using var restarted = new HerdrSessionService(client, store, new ManualTimeProvider(Start));

        var views = await restarted.RestoreAsync(NoCancellation).ConfigureAwait(true);

        client.Calls.Any(call => string.Equals(call, "Attach", StringComparison.Ordinal)).ShouldBeFalse();
        var view = views.ShouldHaveSingleItem();
        view.Snapshot.State.ShouldBe(HerdrSessionState.Running);
        view.Snapshot.IsAttached.ShouldBeFalse();
    }

    /// <summary>
    /// Delegates every call to an in-memory server and records each call by name. It reports no optional operation, so the
    /// service must refuse pause, resume, attach and detach itself.
    /// </summary>
    private sealed class NoOptionalOperationsClient : IHerdrSessionClient
    {
        private readonly FakeHerdrSessionClient _server = new();

        public List<string> Calls { get; } = [];

        /// <inheritdoc />
        public HerdrSessionCapabilities Capabilities { get; } = new(Pause: false, Resume: false, Attach: false, Detach: false);

        /// <inheritdoc />
        public Task<HerdrSessionSnapshot> CreateAsync(HerdrCreateRequest request, CancellationToken cancellationToken)
        {
            Calls.Add("Create");
            return _server.CreateAsync(request, cancellationToken);
        }

        /// <inheritdoc />
        public Task<HerdrSessionSnapshot> AttachAsync(HerdrSessionId sessionId, long afterSequence, CancellationToken cancellationToken)
        {
            Calls.Add("Attach");
            return _server.AttachAsync(sessionId, afterSequence, cancellationToken);
        }

        /// <inheritdoc />
        public Task<HerdrSessionSnapshot> DetachAsync(HerdrSessionId sessionId, CancellationToken cancellationToken)
        {
            Calls.Add("Detach");
            return _server.DetachAsync(sessionId, cancellationToken);
        }

        /// <inheritdoc />
        public Task<HerdrSessionSnapshot> PauseAsync(HerdrSessionId sessionId, CancellationToken cancellationToken)
        {
            Calls.Add("Pause");
            return _server.PauseAsync(sessionId, cancellationToken);
        }

        /// <inheritdoc />
        public Task<HerdrSessionSnapshot> ResumeAsync(HerdrSessionId sessionId, CancellationToken cancellationToken)
        {
            Calls.Add("Resume");
            return _server.ResumeAsync(sessionId, cancellationToken);
        }

        /// <inheritdoc />
        public Task<HerdrSessionSnapshot> TerminateAsync(HerdrSessionId sessionId, CancellationToken cancellationToken)
        {
            Calls.Add("Terminate");
            return _server.TerminateAsync(sessionId, cancellationToken);
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<HerdrSessionSnapshot>> ListAsync(CancellationToken cancellationToken)
        {
            Calls.Add("List");
            return _server.ListAsync(cancellationToken);
        }

        /// <inheritdoc />
        public Task<HerdrSessionSnapshot> StatusAsync(HerdrSessionId sessionId, CancellationToken cancellationToken)
        {
            Calls.Add("Status");
            return _server.StatusAsync(sessionId, cancellationToken);
        }

        /// <inheritdoc />
        public IAsyncEnumerable<HerdrHeartbeat> HeartbeatsAsync(CancellationToken cancellationToken)
        {
            Calls.Add("Heartbeats");
            return _server.HeartbeatsAsync(cancellationToken);
        }
    }
}
