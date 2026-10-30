using System.Net;
using Autonogrammer.Contracts.Artifacts;
using Autonogrammer.Cowork.Artifacts.Model;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Autonogrammer.Cowork.Artifacts.Stores.Tests;

/// <summary>Sync stays off until enabled, forks on divergence, and retries an offline queue in order.</summary>
public sealed class ArtifactSyncServiceTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>While sync is off, no version body leaves the device.</summary>
    [Fact]
    public async Task Disabled_sync_sends_metadata_only()
    {
        using var handler = new RecordingHandler();
        using var client = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("https://account.example/") };
        var (service, store) = Create(client, enabled: false);
        var created = await CreateNoteAsync(store, [1, 2, 3, 4]).ConfigureAwait(true);
        service.StatusText.ShouldBe(ArtifactSyncService.SyncOffText);
        var outcome = await service.PushAsync(created, Token).ConfigureAwait(true);

        outcome.Status.ShouldBe(HttpArtifactRemote.RemoteStatus.Accepted);
        handler.Bodies.Count.ShouldBe(1);
        handler.MediaTypes.Single().ShouldBe(ArtifactWireContract.MetadataMediaType);
        handler.Bodies[0].ShouldNotBe([1, 2, 3, 4]);
        Contains(handler.Bodies[0], [1, 2, 3, 4]).ShouldBeFalse();
    }

    /// <summary>The same hash writes nothing. Different bytes for an existing number fork the desktop copy.</summary>
    [Fact]
    public async Task Same_hash_is_idempotent_and_divergence_forks_without_merge()
    {
        using var handler = new RecordingHandler();
        using var client = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("https://account.example/") };
        var (service, store) = Create(client, enabled: true);
        var created = await CreateNoteAsync(store, "alpha"u8.ToArray()).ConfigureAwait(true);
        var hash = ArtifactWireContract.Hash("alpha"u8);
        handler.Seed(created, 1, hash);

        var same = await service.PushAsync(created, Token).ConfigureAwait(true);
        same.Status.ShouldBe(HttpArtifactRemote.RemoteStatus.SameHash);
        handler.Writes.ShouldBe(0);

        handler.Seed(created, 1, ArtifactWireContract.Hash("other"u8));
        var divergent = await service.PushAsync(created, Token).ConfigureAwait(true);
        divergent.Status.ShouldBe(HttpArtifactRemote.RemoteStatus.Divergence);
        divergent.ForkedArtifactId.ShouldNotBeNull();
        string.Equals(divergent.ForkedArtifactId, created, StringComparison.Ordinal).ShouldBeFalse();
        handler.Writes.ShouldBe(0);
        var original = await store.GetVersionsAsync(created, Token).ConfigureAwait(true);
        original.Count.ShouldBe(1);
        original[0].Content.ToArray().ShouldBe("alpha"u8.ToArray());
        var fork = await store.GetVersionsAsync(divergent.ForkedArtifactId, Token).ConfigureAwait(true);
        fork.Count.ShouldBe(1);
        fork[0].Content.ToArray().ShouldBe("alpha"u8.ToArray());
    }

    /// <summary>Failed pushes retry in order, and a repeated hash does not write again.</summary>
    [Fact]
    public async Task Offline_queue_retries_in_order_and_stays_idempotent()
    {
        using var handler = new RecordingHandler { FailuresRemaining = 1 };
        using var client = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("https://account.example/") };
        var (service, store) = Create(client, enabled: true);
        var serviceLayer = new ArtifactService(store, TimeProvider.System, Options.Create(new ArtifactOptions()));
        var created = await serviceLayer.CreateAsync(ArtifactKind.Markdown, "Notes", "v1"u8.ToArray(), null, null, null, null, ArtifactAuthor.Agent, Token).ConfigureAwait(true);
        var id = created.Value!.ArtifactId;
        (await service.PushAsync(id, Token).ConfigureAwait(true)).Status.ShouldBe(HttpArtifactRemote.RemoteStatus.Offline);
        await serviceLayer.UpdateAsync(id, 1, "v2"u8.ToArray(), null, ArtifactAuthor.Agent, Token).ConfigureAwait(true);
        (await service.PushAsync(id, Token).ConfigureAwait(true)).Status.ShouldBe(HttpArtifactRemote.RemoteStatus.Offline);
        service.QueuedCount.ShouldBe(2);

        var retried = await service.RetryAsync(Token).ConfigureAwait(true);
        retried.Status.ShouldBe(HttpArtifactRemote.RemoteStatus.Accepted);
        service.QueuedCount.ShouldBe(0);
        handler.Accepted.ShouldBe(["1", "2"]);

        (await service.PushAsync(id, Token).ConfigureAwait(true)).Status.ShouldBe(HttpArtifactRemote.RemoteStatus.SameHash);
        handler.Writes.ShouldBe(2);
    }

    private static (ArtifactSyncService Service, FileArtifactStore Store) Create(HttpClient client, bool enabled)
    {
        var root = Path.Combine(Path.GetTempPath(), "ag-sync-" + Guid.NewGuid().ToString("N"));
        var store = new FileArtifactStore(root, "sync-account");
        var remote = new HttpArtifactRemote(client, _ => ValueTask.FromResult<string?>("token"));
        var service = new ArtifactSyncService(store, remote) { Enabled = enabled };
        return (service, store);
    }

    private static async Task<string> CreateNoteAsync(FileArtifactStore store, byte[] content)
    {
        var service = new ArtifactService(store, TimeProvider.System, Options.Create(new ArtifactOptions()));
        var created = await service.CreateAsync(ArtifactKind.Markdown, "Notes", content, null, null, null, null, ArtifactAuthor.Agent, Token).ConfigureAwait(true);
        return created.Value!.ArtifactId;
    }

    private static bool Contains(byte[] haystack, byte[] needle)
    {
        for (var index = 0; index <= haystack.Length - needle.Length; index++)
        {
            if (haystack.AsSpan(index, needle.Length).SequenceEqual(needle))
            {
                return true;
            }
        }

        return false;
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, string> _hashes = new(StringComparer.Ordinal);

        internal List<byte[]> Bodies { get; } = [];

        internal List<string> MediaTypes { get; } = [];

        internal List<string> Accepted { get; } = [];

        internal int Writes { get; private set; }

        internal int FailuresRemaining { get; set; }

        internal void Seed(string artifactId, int number, string hash) => _hashes[artifactId + ":" + number] = hash;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(true);
            Bodies.Add(body);
            MediaTypes.Add(request.Content?.Headers.ContentType?.MediaType ?? string.Empty);
            if (FailuresRemaining > 0)
            {
                FailuresRemaining--;
                throw new HttpRequestException("offline");
            }

            var number = request.RequestUri!.AbsolutePath.Split('/').Last();
            var id = request.Headers.GetValues(ArtifactWireContract.ArtifactIdHeader).Single();
            var hash = request.Headers.GetValues(ArtifactWireContract.ContentSha256Header).Single();
            var key = id + ":" + number;
            if (_hashes.TryGetValue(key, out var existing))
            {
                if (string.Equals(existing, hash, StringComparison.Ordinal))
                {
                    var same = new HttpResponseMessage(HttpStatusCode.OK);
                    same.Headers.TryAddWithoutValidation(ArtifactWireContract.SameHashHeader, "1");
                    return same;
                }

                return new HttpResponseMessage((HttpStatusCode)ArtifactWireContract.StatusDivergence);
            }

            _hashes[key] = hash;
            Writes++;
            Accepted.Add(number);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
