using Autonogrammer.Contracts.Environments;
using Autonogrammer.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Remote.Tests;

/// <summary>
/// The environment profile store over a migrated per-user data directory: it routes its default file through the shared resolver, serves what
/// the legacy directory held after the move, keeps the new directory when both exist, and never writes under the legacy one.
/// </summary>
public sealed class UserDataDirectoryMigrationTests : IDisposable
{
    private readonly string _parent = Path.Combine(Path.GetTempPath(), "autonogrammer-userdata-remote-" + Guid.NewGuid().ToString("N"));

    private string Legacy => Path.Combine(_parent, LegacyBrandNames.UserDataDirectory);

    private string Current => UserDataDirectory.PathFor(_parent);

    public void Dispose()
    {
        if (Directory.Exists(_parent))
        {
            Directory.Delete(_parent, recursive: true);
        }
    }

    private static EnvironmentProfile Profile(string id) => new()
    {
        Id = id,
        DisplayName = "Box " + id,
        Kind = EnvironmentKind.Ssh,
        Ssh = new SshProfile { Alias = "dev-box", RemoteWorkspaceRoot = "/home/dev/work", User = "dev" },
    };

    /// <summary>The default profile file is the resolver directory under the per-user application data folder.</summary>
    [Fact]
    public void TheDefaultPathIsTheResolverDirectory()
    {
        var applicationData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.DoNotVerify);

        EnvironmentProfileStore.DefaultPath().ShouldBe(Path.Combine(UserDataDirectory.PathFor(applicationData), "environments.json"));
    }

    /// <summary>A profile saved under the legacy directory is listed by the store after the move, and the legacy path is gone.</summary>
    [Fact]
    public async Task AProfileSavedBeforeTheMoveIsListedAfterIt()
    {
        var ct = TestContext.Current.CancellationToken;
        using (var legacy = new EnvironmentProfileStore(Path.Combine(Legacy, "environments.json")))
        {
            await legacy.SaveAsync(Profile("legacy-box"), ct).ConfigureAwait(true);
        }

        var migration = UserDataDirectory.EnsureCurrent(_parent);
        using var store = new EnvironmentProfileStore(Path.Combine(migration.Path, "environments.json"));
        var listing = await store.ListAsync(ct).ConfigureAwait(true);

        migration.Migrated.ShouldBeTrue();
        Directory.Exists(Legacy).ShouldBeFalse();
        listing.Profiles.ShouldHaveSingleItem().Profile.Id.ShouldBe("legacy-box");
    }

    /// <summary>With both directories present the store lists the new file only, saves beside it, and leaves the legacy tree byte-identical.</summary>
    [Fact]
    public async Task BothPresentTheStoreServesTheNewFileAndLeavesTheLegacyDirectoryAlone()
    {
        var ct = TestContext.Current.CancellationToken;
        using (var legacy = new EnvironmentProfileStore(Path.Combine(Legacy, "environments.json")))
        {
            await legacy.SaveAsync(Profile("legacy-box"), ct).ConfigureAwait(true);
        }

        using (var current = new EnvironmentProfileStore(Path.Combine(Current, "environments.json")))
        {
            await current.SaveAsync(Profile("new-box"), ct).ConfigureAwait(true);
        }

        var legacyBefore = Snapshot(Legacy);
        var migration = UserDataDirectory.EnsureCurrent(_parent);
        using var store = new EnvironmentProfileStore(Path.Combine(migration.Path, "environments.json"));
        await store.SaveAsync(Profile("later-box"), ct).ConfigureAwait(true);
        var listing = await store.ListAsync(ct).ConfigureAwait(true);

        migration.Migrated.ShouldBeFalse();
        migration.Warning.ShouldNotBeNull();
        listing.Profiles.Select(static stored => stored.Profile.Id).Order(StringComparer.Ordinal).ToArray().ShouldBe(["later-box", "new-box"]);
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
