using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Workspace.Sets;
using Shouldly;
using Xunit;

namespace Autonogrammer.Workspace.Tests;

/// <summary>
/// The consent store and the workspace-set store over a migrated per-user data directory: they route their default file through the shared
/// resolver, serve what the legacy directory held after the move, keep the new directory when both exist, and never write under the legacy one.
/// </summary>
public sealed class UserDataDirectoryMigrationTests : IDisposable
{
    private const string Repository = "/work/repo-a";
    private const string HashLegacy = "1111111111111111111111111111111111111111111111111111111111111111";
    private const string HashNew = "2222222222222222222222222222222222222222222222222222222222222222";

    private readonly string _parent = Path.Combine(Path.GetTempPath(), "autonogrammer-userdata-ws-" + Guid.NewGuid().ToString("N"));

    private string Legacy => Path.Combine(_parent, LegacyBrandNames.UserDataDirectory);

    private string Current => UserDataDirectory.PathFor(_parent);

    public void Dispose()
    {
        if (Directory.Exists(_parent))
        {
            Directory.Delete(_parent, recursive: true);
        }
    }

    /// <summary>The default consent file is the resolver directory under the per-user application data folder.</summary>
    [Fact]
    public void TheConsentStoreDefaultPathIsTheResolverDirectory()
    {
        FileSetupConsentStore.DefaultPath().ShouldBe(Path.Combine(UserDataDirectory.PathFor(ApplicationData()), "setup-consent.json"));
    }

    /// <summary>The default workspace-set file is the resolver directory under the per-user application data folder.</summary>
    [Fact]
    public void TheWorkspaceSetStoreDefaultPathIsTheResolverDirectory()
    {
        WorkspaceSetStore.DefaultPath().ShouldBe(Path.Combine(UserDataDirectory.PathFor(ApplicationData()), "workspace-sets.json"));
    }

    /// <summary>An approval written under the legacy directory is served by the consent store after the move, and the legacy path is gone.</summary>
    [Fact]
    public async Task AnApprovalWrittenBeforeTheMoveIsServedAfterIt()
    {
        var ct = TestContext.Current.CancellationToken;
        using (var legacy = new FileSetupConsentStore(Path.Combine(Legacy, "setup-consent.json")))
        {
            await legacy.RecordGrantAsync(new SetupConsentKey(Repository, HashLegacy), ct).ConfigureAwait(true);
        }

        var migration = UserDataDirectory.EnsureCurrent(_parent);
        using var store = new FileSetupConsentStore(Path.Combine(migration.Path, "setup-consent.json"));

        migration.Migrated.ShouldBeTrue();
        Directory.Exists(Legacy).ShouldBeFalse();
        (await store.IsGrantedAsync(new SetupConsentKey(Repository, HashLegacy), ct).ConfigureAwait(true)).ShouldBeTrue();
    }

    /// <summary>With both directories present the consent store serves the new file only, writes beside it, and leaves the legacy tree byte-identical.</summary>
    [Fact]
    public async Task BothPresentTheConsentStoreServesTheNewFileAndLeavesTheLegacyDirectoryAlone()
    {
        var ct = TestContext.Current.CancellationToken;
        using (var legacy = new FileSetupConsentStore(Path.Combine(Legacy, "setup-consent.json")))
        {
            await legacy.RecordGrantAsync(new SetupConsentKey(Repository, HashLegacy), ct).ConfigureAwait(true);
        }

        using (var current = new FileSetupConsentStore(Path.Combine(Current, "setup-consent.json")))
        {
            await current.RecordGrantAsync(new SetupConsentKey(Repository, HashNew), ct).ConfigureAwait(true);
        }

        var legacyBefore = Snapshot(Legacy);
        var migration = UserDataDirectory.EnsureCurrent(_parent);
        using var store = new FileSetupConsentStore(Path.Combine(migration.Path, "setup-consent.json"));
        await store.RecordGrantAsync(new SetupConsentKey("/work/repo-b", HashNew), ct).ConfigureAwait(true);

        migration.Migrated.ShouldBeFalse();
        migration.Warning.ShouldNotBeNull();
        (await store.IsGrantedAsync(new SetupConsentKey(Repository, HashNew), ct).ConfigureAwait(true)).ShouldBeTrue();
        (await store.IsGrantedAsync(new SetupConsentKey(Repository, HashLegacy), ct).ConfigureAwait(true)).ShouldBeFalse();
        (await store.IsGrantedAsync(new SetupConsentKey("/work/repo-b", HashNew), ct).ConfigureAwait(true)).ShouldBeTrue();
        Snapshot(Legacy).ShouldBe(legacyBefore);
    }

    /// <summary>A workspace set recorded under the legacy directory is listed by the set store after the move.</summary>
    [Fact]
    public async Task ASetRecordedBeforeTheMoveIsListedAfterIt()
    {
        var ct = TestContext.Current.CancellationToken;
        using (var legacy = new WorkspaceSetStore(Path.Combine(Legacy, "workspace-sets.json")))
        {
            await legacy.RecordAsync(WorkspaceSet.FromPaths("Team", ["/work/web", "/work/api"]), ct).ConfigureAwait(true);
        }

        var migration = UserDataDirectory.EnsureCurrent(_parent);
        using var store = new WorkspaceSetStore(Path.Combine(migration.Path, "workspace-sets.json"));
        var listed = await store.ListAsync(ct).ConfigureAwait(true);

        migration.Migrated.ShouldBeTrue();
        Directory.Exists(Legacy).ShouldBeFalse();
        listed.ShouldHaveSingleItem().Set.Id.ShouldBe("team");
    }

    /// <summary>With both directories present the set store lists the new file only, records beside it, and leaves the legacy tree byte-identical.</summary>
    [Fact]
    public async Task BothPresentTheSetStoreServesTheNewFileAndLeavesTheLegacyDirectoryAlone()
    {
        var ct = TestContext.Current.CancellationToken;
        using (var legacy = new WorkspaceSetStore(Path.Combine(Legacy, "workspace-sets.json")))
        {
            await legacy.RecordAsync(WorkspaceSet.FromPaths("Legacy", ["/work/legacy"]), ct).ConfigureAwait(true);
        }

        using (var current = new WorkspaceSetStore(Path.Combine(Current, "workspace-sets.json")))
        {
            await current.RecordAsync(WorkspaceSet.FromPaths("Fresh", ["/work/fresh"]), ct).ConfigureAwait(true);
        }

        var legacyBefore = Snapshot(Legacy);
        var migration = UserDataDirectory.EnsureCurrent(_parent);
        using var store = new WorkspaceSetStore(Path.Combine(migration.Path, "workspace-sets.json"));
        await store.RecordAsync(WorkspaceSet.FromPaths("Later", ["/work/later"]), ct).ConfigureAwait(true);
        var listed = await store.ListAsync(ct).ConfigureAwait(true);

        migration.Warning.ShouldNotBeNull();
        listed.Select(static recent => recent.Set.Id).ShouldBe(["later", "fresh"]);
        Snapshot(Legacy).ShouldBe(legacyBefore);
    }

    private static string ApplicationData() =>
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.DoNotVerify);

    private static Dictionary<string, string> Snapshot(string directory)
    {
        var snapshot = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFileSystemEntries(directory, "*", SearchOption.AllDirectories))
        {
            snapshot[Path.GetRelativePath(directory, path)] = Directory.Exists(path) ? "<directory>" : Convert.ToBase64String(File.ReadAllBytes(path));
        }

        return snapshot;
    }
}
