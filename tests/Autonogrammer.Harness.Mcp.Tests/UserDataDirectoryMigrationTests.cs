using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.Mcp.Catalog;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Mcp.Tests;

/// <summary>
/// The MCP state store over a migrated per-user data directory: it routes its default file through the shared resolver, serves what the
/// legacy directory held after the move, keeps the new directory when both exist, and never writes under the legacy one.
/// </summary>
public sealed class UserDataDirectoryMigrationTests : IDisposable
{
    private readonly string _parent = Path.Combine(Path.GetTempPath(), "autonogrammer-userdata-mcp-" + Guid.NewGuid().ToString("N"));

    private string Legacy => Path.Combine(_parent, LegacyBrandNames.UserDataDirectory);

    private string Current => UserDataDirectory.PathFor(_parent);

    public void Dispose()
    {
        if (Directory.Exists(_parent))
        {
            Directory.Delete(_parent, recursive: true);
        }
    }

    /// <summary>The default state file is the resolver directory under the per-user application data folder.</summary>
    [Fact]
    public void TheDefaultPathIsTheResolverDirectory()
    {
        var applicationData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.DoNotVerify);

        FileMcpStateStore.DefaultPath().ShouldBe(Path.Combine(UserDataDirectory.PathFor(applicationData), "mcp-state.json"));
    }

    /// <summary>A choice saved under the legacy directory is loaded by the store after the move, and the legacy path is gone.</summary>
    [Fact]
    public async Task AChoiceSavedBeforeTheMoveIsLoadedAfterIt()
    {
        var ct = TestContext.Current.CancellationToken;
        await new FileMcpStateStore(Path.Combine(Legacy, "mcp-state.json")).SaveAsync(McpState.Empty.WithEnabled("files", true), ct).ConfigureAwait(true);

        var migration = UserDataDirectory.EnsureCurrent(_parent);
        var loaded = await new FileMcpStateStore(Path.Combine(migration.Path, "mcp-state.json")).LoadAsync(ct).ConfigureAwait(true);

        migration.Migrated.ShouldBeTrue();
        Directory.Exists(Legacy).ShouldBeFalse();
        loaded.EnabledOverride("files").ShouldBe(true);
    }

    /// <summary>With both directories present the store serves the new file only, saves beside it, and leaves the legacy tree byte-identical.</summary>
    [Fact]
    public async Task BothPresentTheStoreServesTheNewFileAndLeavesTheLegacyDirectoryAlone()
    {
        var ct = TestContext.Current.CancellationToken;
        await new FileMcpStateStore(Path.Combine(Legacy, "mcp-state.json")).SaveAsync(McpState.Empty.WithEnabled("legacy-server", true), ct).ConfigureAwait(true);
        await new FileMcpStateStore(Path.Combine(Current, "mcp-state.json")).SaveAsync(McpState.Empty.WithEnabled("new-server", true), ct).ConfigureAwait(true);
        var legacyBefore = Snapshot(Legacy);

        var migration = UserDataDirectory.EnsureCurrent(_parent);
        var store = new FileMcpStateStore(Path.Combine(migration.Path, "mcp-state.json"));
        var loaded = await store.LoadAsync(ct).ConfigureAwait(true);
        await store.SaveAsync(loaded.WithConsent("identity-1"), ct).ConfigureAwait(true);
        var reloaded = await store.LoadAsync(ct).ConfigureAwait(true);

        migration.Migrated.ShouldBeFalse();
        migration.Warning.ShouldNotBeNull();
        reloaded.EnabledOverride("new-server").ShouldBe(true);
        reloaded.EnabledOverride("legacy-server").ShouldBeNull();
        reloaded.IsConsented("identity-1").ShouldBeTrue();
        Snapshot(Legacy).ShouldBe(legacyBefore);
    }

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
