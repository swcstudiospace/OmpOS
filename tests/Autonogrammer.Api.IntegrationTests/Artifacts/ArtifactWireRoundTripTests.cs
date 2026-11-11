using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using Autonogrammer.Api.Cowork.Artifacts;
using Autonogrammer.Contracts.Artifacts;
using Autonogrammer.Cowork.Artifacts;
using Autonogrammer.Cowork.Artifacts.Model;
using Autonogrammer.Cowork.Artifacts.Stores;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Artifacts;

/// <summary>Drives <see cref="HttpArtifactRemote"/> against the API host.</summary>
public sealed class ArtifactWireRoundTripTests
{
    private static readonly string[] MetadataHashes = ["abc"];

    /// <summary>Verifies same-hash 200, divergent 409, oversize 413 and the metadata-only form.</summary>
    [Fact]
    public async Task RemoteMapsHashDivergenceSizeAndMetadata()
    {
        using var factory = new ApiFactory(ArtifactTestConfig.Enabled(), JwtTestHost.InjectStaticMetadata);
        using var client = factory.CreateClient();
        var token = JwtTestHost.IssueToken("alice");
        var remote = new HttpArtifactRemote(client, _ => ValueTask.FromResult<string?>(token));
        var id = "wire-" + Guid.NewGuid().ToString("N");
        var bytes = "version-bytes"u8.ToArray();
        var hash = ArtifactWireContract.Hash(bytes);
        (await PushAsync(remote, id, 1, bytes, hash, "Markdown").ConfigureAwait(true)).ShouldBe("Accepted");
        (await PushAsync(remote, id, 1, bytes, hash, "Markdown").ConfigureAwait(true)).ShouldBe("SameHash");
        var other = "other-bytes"u8.ToArray();
        (await PushAsync(remote, id, 1, other, ArtifactWireContract.Hash(other), "Markdown").ConfigureAwait(true)).ShouldBe("Divergence");
        (await PushAsync(remote, "over-" + Guid.NewGuid().ToString("N"), 1, new byte[(1024 * 1024) + 1], "ab", "Markdown").ConfigureAwait(true)).ShouldBe("TooLarge");
        (await PushMetadataAsync(remote, id).ConfigureAwait(true)).ShouldBe("SameHash");
        using var probe = factory.CreateClient();
        probe.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var content = await probe.GetAsync(new Uri("/cowork/artifacts/" + id + "/versions/1/content", UriKind.Relative), TestContext.Current.CancellationToken).ConfigureAwait(true);
        content.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await content.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBe(bytes);
    }

    /// <summary>Sync-off metadata leaves an existing server grant unchanged.</summary>
    [Fact]
    public async Task SyncOffMetadataDoesNotChangeExistingGrant()
    {
        var root = Path.Combine(Path.GetTempPath(), "ag-wire-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var factory = new ApiFactory(ArtifactTestConfig.Enabled(), JwtTestHost.InjectStaticMetadata);
            using var client = factory.CreateClient();
            var token = JwtTestHost.IssueToken("alice");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var remote = new HttpArtifactRemote(client, _ => ValueTask.FromResult<string?>(token));
            var store = new FileArtifactStore(root, "alice");
            var service = new ArtifactService(store, TimeProvider.System, Microsoft.Extensions.Options.Options.Create(new ArtifactOptions()));
            var bytes = "private-content"u8.ToArray();
            var created = await service.CreateAsync(ArtifactKind.Markdown, "Notes", bytes, null, null, null, null, ArtifactAuthor.Agent, TestContext.Current.CancellationToken).ConfigureAwait(true);
            var id = created.Value!.ArtifactId;
            var hash = ArtifactWireContract.Hash(bytes);
            (await PushAsync(remote, id, 1, bytes, hash, "Markdown").ConfigureAwait(true)).ShouldBe("Accepted");
            var grant = new ArtifactGrantDocument(id, 1, hash, ["https://docs.example"], "version", "web", DateTimeOffset.UnixEpoch);
            using var granted = await client.PutAsJsonAsync("/cowork/artifacts/" + id + "/versions/1/grants", grant, ArtifactsJsonContext.Default.ArtifactGrantDocument, TestContext.Current.CancellationToken).ConfigureAwait(true);
            granted.StatusCode.ShouldBe(HttpStatusCode.OK);
            var grants = factory.Services.GetRequiredService<ArtifactGrants>();
            var before = grants.List("alice", id).ShouldHaveSingleItem();
            before.Origins.ShouldBe(["https://docs.example"]);

            var sync = new ArtifactSyncService(store, remote);
            var method = typeof(ArtifactSyncService).GetMethod("PushAsync", BindingFlags.Instance | BindingFlags.NonPublic);
            var task = (Task)method!.Invoke(sync, [id, TestContext.Current.CancellationToken])!;
            await task.ConfigureAwait(true);
            var result = task.GetType().GetProperty("Result")!.GetValue(task);
            result!.GetType().GetProperty("Status")!.GetValue(result)!.ToString().ShouldBe("SameHash");
            grants.List("alice", id).ShouldHaveSingleItem().ShouldBe(before);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static async Task<string> PushAsync(HttpArtifactRemote remote, string id, int number, byte[] bytes, string hash, string kind)
    {
        var method = typeof(HttpArtifactRemote).GetMethod("PushVersionAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        var task = (Task)method!.Invoke(remote, [id, number, (ReadOnlyMemory<byte>)bytes, hash, kind, TestContext.Current.CancellationToken])!;
        await task.ConfigureAwait(true);
        var result = task.GetType().GetProperty("Result")!.GetValue(task);
        return result!.GetType().GetProperty("Status")!.GetValue(result)!.ToString()!;
    }

    private static async Task<string> PushMetadataAsync(HttpArtifactRemote remote, string id)
    {
        var metadataType = typeof(ArtifactWireContract).GetNestedType("SyncMetadata", BindingFlags.NonPublic);
        var metadata = Activator.CreateInstance(metadataType!)!;
        metadataType!.GetProperty("Id")!.SetValue(metadata, id);
        metadataType.GetProperty("Title")!.SetValue(metadata, "hidden-title");
        metadataType.GetProperty("Kind")!.SetValue(metadata, "Markdown");
        metadataType.GetProperty("VersionCount")!.SetValue(metadata, 1);
        metadataType.GetProperty("CreatedAt")!.SetValue(metadata, DateTimeOffset.UnixEpoch);
        metadataType.GetProperty("UpdatedAt")!.SetValue(metadata, DateTimeOffset.UnixEpoch);
        metadataType.GetProperty("Hashes")!.SetValue(metadata, MetadataHashes);
        var method = typeof(HttpArtifactRemote).GetMethod("PushMetadataAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        var task = (Task)method!.Invoke(remote, [metadata, TestContext.Current.CancellationToken])!;
        await task.ConfigureAwait(true);
        var result = task.GetType().GetProperty("Result")!.GetValue(task);
        return result!.GetType().GetProperty("Status")!.GetValue(result)!.ToString()!;
    }
}
