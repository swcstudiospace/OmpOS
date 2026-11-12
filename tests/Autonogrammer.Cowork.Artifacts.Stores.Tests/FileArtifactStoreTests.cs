using System.Security.Cryptography;
using System.Text;
using Autonogrammer.Cowork.Artifacts.Model;
using Autonogrammer.Cowork.Artifacts.Stores.Tests.Support;
using Shouldly;
using Xunit;

namespace Autonogrammer.Cowork.Artifacts.Stores.Tests;

/// <summary>Temp-directory tests for the authoritative file store.</summary>
public sealed class FileArtifactStoreTests : ArtifactStoreContract, IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ag-artifacts-" + Guid.NewGuid().ToString("N"));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <inheritdoc />
    protected override ValueTask<IArtifactStore> CreateStoreAsync() => ValueTask.FromResult<IArtifactStore>(new FileArtifactStore(_root, "account-contract"));

    /// <summary>Deletes the temp root this test created.</summary>
    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>Two updates write v0001 and v0002 and the head is the highest version file.</summary>
    [Fact]
    public async Task Sequential_updates_are_write_once_and_head_is_the_highest_file()
    {
        var store = new FileArtifactStore(_root, "account-a");
        var id = Guid.NewGuid().ToString("D");
        await store.MutateAsync(mutation =>
        {
            mutation.Insert(Header(id), Version(id, 1, "one"u8.ToArray(), null, null));
            mutation.Append(Version(id, 2, "two"u8.ToArray(), 1, Hash("one"u8)));
            return true;
        }, Token).ConfigureAwait(true);

        var folder = Path.Combine(store.TenantDirectory, id);
        File.Exists(Path.Combine(folder, "v0001.bin")).ShouldBeTrue();
        File.Exists(Path.Combine(folder, "v0001.json")).ShouldBeTrue();
        File.Exists(Path.Combine(folder, "v0002.bin")).ShouldBeTrue();
        File.Exists(Path.Combine(folder, "HEAD")).ShouldBeFalse();
        var versions = await store.GetVersionsAsync(id, Token).ConfigureAwait(true);
        versions.Count.ShouldBe(2);
        versions[^1].Number.ShouldBe(2);
        Directory.EnumerateFiles(folder, "*.tmp").ShouldBeEmpty();
    }

    /// <summary>Concurrent updates to one artifact do not lose a version or leave a partial file.</summary>
    [Fact]
    public async Task Concurrent_updates_serialize_under_the_artifact_lock()
    {
        var store = new FileArtifactStore(_root, "account-a");
        var id = Guid.NewGuid().ToString("D");
        await store.MutateAsync(mutation =>
        {
            mutation.Insert(Header(id), Version(id, 1, [1], null, null));
            return true;
        }, Token).ConfigureAwait(true);

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => store.MutateAsync(mutation =>
        {
            var versions = mutation.Versions(id);
            var next = versions.Count + 1;
            var parent = versions[^1];
            mutation.Append(Version(id, next, [(byte)next], parent.Number, parent.ContentSha256));
            return next;
        }, Token).AsTask())));

        var stored = await store.GetVersionsAsync(id, Token).ConfigureAwait(true);
        stored.Select(version => version.Number).ShouldBe(Enumerable.Range(1, 9));
        var folder = Path.Combine(store.TenantDirectory, id);
        Directory.EnumerateFiles(folder, "*.tmp").ShouldBeEmpty();
        foreach (var json in Directory.EnumerateFiles(folder, "v*.json"))
        {
            File.Exists(Path.ChangeExtension(json, ".bin")).ShouldBeTrue();
        }
    }

    /// <summary>Account tenants are distinct and one account cannot list the other.</summary>
    [Fact]
    public async Task Accounts_map_to_distinct_tenant_folders()
    {
        var left = new FileArtifactStore(_root, "account-left");
        var right = new FileArtifactStore(_root, "account-right");
        string.Equals(left.TenantDirectory, right.TenantDirectory, StringComparison.Ordinal).ShouldBeFalse();
        FileArtifactStore.TenantName("account-left").ShouldBe(ExpectedTenant("account-left"));
        var id = Guid.NewGuid().ToString("D");
        await left.MutateAsync(mutation =>
        {
            mutation.Insert(Header(id), Version(id, 1, [9], null, null));
            return true;
        }, Token).ConfigureAwait(true);

        (await right.GetArtifactAsync(id, Token).ConfigureAwait(true)).ShouldBeNull();
        (await right.GetVersionsAsync(id, Token).ConfigureAwait(true)).ShouldBeEmpty();
        Directory.Exists(Path.Combine(right.TenantDirectory, id)).ShouldBeFalse();
    }

    /// <summary>A tampered version file is refused on the next read.</summary>
    [Fact]
    public async Task Tampered_content_raises_integrity_exception()
    {
        var store = new FileArtifactStore(_root, "account-a");
        var id = Guid.NewGuid().ToString("D");
        await store.MutateAsync(mutation =>
        {
            mutation.Insert(Header(id), Version(id, 1, [1, 2, 3], null, null));
            return true;
        }, Token).ConfigureAwait(true);
        var bin = Path.Combine(store.TenantDirectory, id, "v0001.bin");
        var bytes = await File.ReadAllBytesAsync(bin, Token).ConfigureAwait(true);
        bytes[0] ^= 0xFF;
        await File.WriteAllBytesAsync(bin, bytes, Token).ConfigureAwait(true);

        await Should.ThrowAsync<ArtifactIntegrityException>(() => store.GetVersionsAsync(id, Token).AsTask()).ConfigureAwait(true);
    }

    /// <summary>A crash that leaves a manifest or a temp file is not a visible version after the next read.</summary>
    [Fact]
    public async Task Simulated_crash_leaves_no_partial_version()
    {
        var store = new FileArtifactStore(_root, "account-a");
        var id = Guid.NewGuid().ToString("D");
        var folder = Path.Combine(store.TenantDirectory, id);
        Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(Path.Combine(folder, "v0001.json"), "{}", Token).ConfigureAwait(true);
        await File.WriteAllTextAsync(Path.Combine(folder, "v0001.bin." + Guid.NewGuid().ToString("N") + ".tmp"), "partial", Token).ConfigureAwait(true);

        (await store.GetVersionsAsync(id, Token).ConfigureAwait(true)).ShouldBeEmpty();
        File.Exists(Path.Combine(folder, "v0001.json")).ShouldBeFalse();
        Directory.EnumerateFiles(folder, "*.tmp").ShouldBeEmpty();
    }

    private static ArtifactRecord Header(string id) =>
        new(id, Autonogrammer.Contracts.Artifacts.ArtifactKind.Markdown, "Notes", "text/markdown", null, null, null);

    private static ArtifactVersionRecord Version(string id, int number, byte[] content, int? parent, string? parentHash) =>
        new(id, number, content, Hash(content), parent, parentHash, ArtifactAuthor.Agent, DateTimeOffset.UnixEpoch, null);

    private static string Hash(ReadOnlySpan<byte> content) => ArtifactWireContract.Hash(content);

    private static string ExpectedTenant(string accountId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(accountId));
        return Convert.ToHexStringLower(hash.AsSpan(0, 16));
    }
}
