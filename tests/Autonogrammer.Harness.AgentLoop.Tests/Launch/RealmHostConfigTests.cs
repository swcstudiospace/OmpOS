using Autonogrammer.Harness.AgentLoop.Launch;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.AgentLoop.Tests.Launch;

/// <summary>
/// The host Realm config is found the way Realm finds it, and is copied into an isolated home at the path the isolated profile resolves.
/// </summary>
public sealed class RealmHostConfigTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "autonogrammer-realm-config-" + Guid.NewGuid().ToString("N"));

    public RealmHostConfigTests() => Directory.CreateDirectory(_root);

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void AnAbsoluteXdgConfigHomeIsUsedFirst()
    {
        var xdg = Path.Combine(_root, "xdg");
        WriteConfig(Path.Combine(xdg, "realm", "config.json"));
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["XDG_CONFIG_HOME"] = xdg,
            ["HOME"] = Path.Combine(_root, "home"),
        };

        RealmHostConfig.ResolveHostPath(Lookup(environment)).ShouldBe(Path.Combine(xdg, "realm", "config.json"));
    }

    [Fact]
    public void WithoutAnAbsoluteXdgConfigHomeTheHomeConfigDirectoryIsUsed()
    {
        var home = Path.Combine(_root, "home");
        WriteConfig(Path.Combine(home, ".config", "realm", "config.json"));
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["XDG_CONFIG_HOME"] = "relative/config",
            ["HOME"] = home,
        };

        RealmHostConfig.ResolveHostPath(Lookup(environment)).ShouldBe(Path.Combine(home, ".config", "realm", "config.json"));
    }

    [Fact]
    public void NoConfigFileMeansNoPath()
    {
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["HOME"] = Path.Combine(_root, "empty"),
        };

        RealmHostConfig.ResolveHostPath(Lookup(environment)).ShouldBeNull();
    }

    [Fact]
    public void ShareIntoCopiesTheConfigWhereTheIsolatedProfileResolvesIt()
    {
        var source = Path.Combine(_root, "host", "config.json");
        WriteConfig(source, "{\"image\":\"example\"}");
        var isolated = Path.Combine(_root, "isolated");

        RealmHostConfig.ShareInto(isolated, source).ShouldBeTrue();

        var copied = Path.Combine(isolated, "home", ".config", "realm", "config.json");
        File.ReadAllText(copied).ShouldBe("{\"image\":\"example\"}");
        if (!OperatingSystem.IsWindows())
        {
            File.GetUnixFileMode(copied).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    [Fact]
    public void ShareIntoCreatesNothingWhenTheHostHasNoConfig()
    {
        var isolated = Path.Combine(_root, "isolated");

        RealmHostConfig.ShareInto(isolated, Path.Combine(_root, "missing.json")).ShouldBeFalse();
        Directory.Exists(isolated).ShouldBeFalse();
    }

    private static Func<string, string?> Lookup(Dictionary<string, string?> environment) =>
        name => environment.TryGetValue(name, out var value) ? value : null;

    private static void WriteConfig(string path, string content = "{}")
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
