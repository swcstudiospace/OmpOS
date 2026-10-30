using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using Autonogrammer.Cowork.Artifacts.Stores;
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

    private static async Task<string> PushAsync(HttpArtifactRemote remote, string id, int number, byte[] bytes, string hash, string kind)
    {
        var method = typeof(HttpArtifactRemote).GetMethod("PushVersionAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        var task = (Task)method!.Invoke(remote, [id, number, bytes.AsMemory(), hash, kind, TestContext.Current.CancellationToken])!;
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
