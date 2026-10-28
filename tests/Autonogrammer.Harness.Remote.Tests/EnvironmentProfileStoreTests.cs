using System.Globalization;
using Autonogrammer.Contracts.Environments;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Remote.Tests;

/// <summary>The profile store: an atomic, owner-only file of validated references, a damaged file that never breaks reading, and bounded, serialized writes.</summary>
public sealed class EnvironmentProfileStoreTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static EnvironmentProfile Profile(string id, string alias = "dev-box", string root = "/home/dev/work") => new()
    {
        Id = id,
        DisplayName = "Box " + id,
        Kind = EnvironmentKind.Ssh,
        Ssh = new SshProfile { Alias = alias, RemoteWorkspaceRoot = root, User = "dev", Port = 2222, ForwardAgent = true },
    };

    /// <summary>A saved profile reads back equal in every field, and the file holds no secret-shaped member.</summary>
    [Fact]
    public async Task AProfileRoundTrips()
    {
        using var directory = TempDirectory.Create();
        var path = directory.Combine("environments.json");
        using var store = new EnvironmentProfileStore(path);

        await store.SaveAsync(Profile("a"), Token).ConfigureAwait(true);
        var listing = await store.ListAsync(Token).ConfigureAwait(true);

        listing.Warning.ShouldBeNull();
        var stored = listing.Profiles.ShouldHaveSingleItem();
        stored.LastUsedUtc.ShouldBeNull();
        stored.Profile.Id.ShouldBe("a");
        stored.Profile.Kind.ShouldBe(EnvironmentKind.Ssh);
        stored.Profile.Ssh!.Alias.ShouldBe("dev-box");
        stored.Profile.Ssh.Port.ShouldBe(2222);
        stored.Profile.Ssh.User.ShouldBe("dev");
        stored.Profile.Ssh.ForwardAgent.ShouldBeTrue();
        stored.Profile.Ssh.KeepAliveSeconds.ShouldBe(SshProfileRules.DefaultKeepAliveSeconds);
        var text = await File.ReadAllTextAsync(path, Token).ConfigureAwait(true);
        foreach (var forbidden in new[] { "password", "passphrase", "privateKey", "token", "secret" })
        {
            text.ShouldNotContain(forbidden, Case.Insensitive);
        }
    }

    /// <summary>The file and a directory the store creates are readable by the owner only, and no temporary file is left.</summary>
    [Fact]
    public async Task TheFileIsOwnerOnlyAndWrittenAtomically()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Unix file modes only.");
            return;
        }

        using var directory = TempDirectory.Create();
        var path = directory.Combine("state", "environments.json");
        using var store = new EnvironmentProfileStore(path);

        await store.SaveAsync(Profile("a"), Token).ConfigureAwait(true);
        await store.SaveAsync(Profile("b"), Token).ConfigureAwait(true);

        File.GetUnixFileMode(path).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        (File.GetUnixFileMode(directory.Combine("state")) & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.OtherRead | UnixFileMode.OtherWrite)).ShouldBe(UnixFileMode.None);
        Directory.GetFiles(directory.Combine("state")).Length.ShouldBe(1);
    }

    /// <summary>Saving an id again replaces the profile in place; deleting removes it and its last-used time; a missing id deletes nothing.</summary>
    [Fact]
    public async Task SaveReplacesInPlaceAndDeleteRemoves()
    {
        using var directory = TempDirectory.Create();
        using var store = new EnvironmentProfileStore(directory.Combine("environments.json"));
        await store.SaveAsync(Profile("a"), Token).ConfigureAwait(true);
        await store.SaveAsync(Profile("b"), Token).ConfigureAwait(true);

        await store.SaveAsync(Profile("a", root: "/srv/new"), Token).ConfigureAwait(true);
        (await store.RecordUsedAsync("a", Token).ConfigureAwait(true)).ShouldBeTrue();
        var afterReplace = await store.ListAsync(Token).ConfigureAwait(true);
        (await store.DeleteAsync("a", Token).ConfigureAwait(true)).ShouldBeTrue();
        (await store.DeleteAsync("a", Token).ConfigureAwait(true)).ShouldBeFalse();
        await store.SaveAsync(Profile("a"), Token).ConfigureAwait(true);
        var afterDelete = await store.ListAsync(Token).ConfigureAwait(true);

        afterReplace.Profiles.Select(stored => stored.Profile.Id).ToArray().ShouldBe(["a", "b"]);
        afterReplace.Profiles[0].Profile.Ssh!.RemoteWorkspaceRoot.ShouldBe("/srv/new");
        afterReplace.Profiles[0].LastUsedUtc.ShouldNotBeNull();
        afterDelete.Profiles.Select(stored => stored.Profile.Id).ToArray().ShouldBe(["b", "a"]);
        afterDelete.Profiles[1].LastUsedUtc.ShouldBeNull();
    }

    /// <summary>The last-used time comes from the injected clock, and an unknown id records nothing.</summary>
    [Fact]
    public async Task LastUsedComesFromTheClock()
    {
        using var directory = TempDirectory.Create();
        var clock = new ManualTimeProvider();
        using var store = new EnvironmentProfileStore(directory.Combine("environments.json"), clock);
        await store.SaveAsync(Profile("a"), Token).ConfigureAwait(true);

        clock.Advance(TimeSpan.FromMinutes(5));
        (await store.RecordUsedAsync("a", Token).ConfigureAwait(true)).ShouldBeTrue();
        (await store.RecordUsedAsync("zzz", Token).ConfigureAwait(true)).ShouldBeFalse();

        var listing = await store.ListAsync(Token).ConfigureAwait(true);
        listing.Profiles.ShouldHaveSingleItem().LastUsedUtc.ShouldBe(clock.GetUtcNow());
    }

    /// <summary>An invalid profile is refused before anything is written, and the 65th profile is refused.</summary>
    [Fact]
    public async Task InvalidAndTooManyProfilesAreRefused()
    {
        using var directory = TempDirectory.Create();
        var path = directory.Combine("environments.json");
        using var store = new EnvironmentProfileStore(path);

        await Should.ThrowAsync<ArgumentException>(() => store.SaveAsync(Profile("a", alias: "-oProxyCommand=evil"), Token)).ConfigureAwait(true);
        File.Exists(path).ShouldBeFalse();

        for (var index = 0; index < EnvironmentProfileStore.MaxProfiles; index++)
        {
            await store.SaveAsync(Profile("p" + index.ToString(CultureInfo.InvariantCulture)), Token).ConfigureAwait(true);
        }

        await Should.ThrowAsync<ArgumentException>(() => store.SaveAsync(Profile("one-too-many"), Token)).ConfigureAwait(true);
        await store.SaveAsync(Profile("p0", root: "/srv/other"), Token).ConfigureAwait(true);
        (await store.ListAsync(Token).ConfigureAwait(true)).Profiles.Count.ShouldBe(EnvironmentProfileStore.MaxProfiles);
    }

    /// <summary>A corrupt file, an empty file and a file of another version read as no profiles with a warning, are moved aside, and the next save works.</summary>
    [Theory]
    [InlineData("{ this is not json")]
    [InlineData("")]
    [InlineData("{\"version\":99,\"profiles\":[]}")]
    [InlineData("{\"version\":1,\"profiles\":[],\"surprise\":true}")]
    public async Task ADamagedFileIsSetAside(string content)
    {
        using var directory = TempDirectory.Create();
        var path = directory.Combine("environments.json");
        await File.WriteAllTextAsync(path, content, Token).ConfigureAwait(true);
        using var store = new EnvironmentProfileStore(path);

        var listing = await store.ListAsync(Token).ConfigureAwait(true);
        await store.SaveAsync(Profile("a"), Token).ConfigureAwait(true);

        listing.Profiles.ShouldBeEmpty();
        listing.Warning.ShouldNotBeNull().ShouldContain("could not be read", Case.Sensitive);
        Directory.GetFiles(directory.Path, "environments.json.damaged-*").Length.ShouldBe(1);
        (await store.ListAsync(Token).ConfigureAwait(true)).Profiles.ShouldHaveSingleItem();
    }

    /// <summary>A hand-edited profile that fails validation (an alias that is an ssh option) is ignored with a warning and never returned.</summary>
    [Fact]
    public async Task AnInvalidStoredProfileIsIgnored()
    {
        using var directory = TempDirectory.Create();
        var path = directory.Combine("environments.json");
        await File.WriteAllTextAsync(
            path,
            """
            {"version":1,"profiles":[
              {"id":"bad","displayName":"Bad","kind":"Ssh","ssh":{"alias":"-oProxyCommand=evil","remoteWorkspaceRoot":"/w"}},
              {"id":"good","displayName":"Good","kind":"Ssh","ssh":{"alias":"good-box","remoteWorkspaceRoot":"/w"}}
            ]}
            """,
            Token).ConfigureAwait(true);
        using var store = new EnvironmentProfileStore(path);

        var listing = await store.ListAsync(Token).ConfigureAwait(true);

        listing.Profiles.ShouldHaveSingleItem().Profile.Id.ShouldBe("good");
        listing.Warning.ShouldNotBeNull().ShouldContain("1 stored environment", Case.Sensitive);
    }

    /// <summary>Concurrent saves are serialized: every profile is present afterwards.</summary>
    [Fact]
    public async Task ConcurrentSavesAreNotLost()
    {
        using var directory = TempDirectory.Create();
        using var store = new EnvironmentProfileStore(directory.Combine("environments.json"));

        await Task.WhenAll(Enumerable.Range(0, 12).Select(index => store.SaveAsync(Profile("c" + index.ToString(CultureInfo.InvariantCulture)), Token))).ConfigureAwait(true);

        (await store.ListAsync(Token).ConfigureAwait(true)).Profiles.Count.ShouldBe(12);
    }
}
