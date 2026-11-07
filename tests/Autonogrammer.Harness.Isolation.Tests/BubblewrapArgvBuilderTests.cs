using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Isolation.Tests;

/// <summary>The bubblewrap argument vector matches the golden file, including the Code and Cowork defaults.</summary>
public sealed class BubblewrapArgvBuilderTests
{
    /// <summary>One read-write grant plus scratch produces the golden vector, and a read-only grant is not a bind.</summary>
    [Fact]
    public void GoldenArgvMatchesTheFixture()
    {
        var policy = GoldenPolicy();

        var argv = BubblewrapArgvBuilder.Build(policy);

        argv.ShouldBe(FixtureLines("golden-bwrap-argv.txt"));
        argv.Contains("--ro-bind", StringComparer.Ordinal).ShouldBeTrue();
        argv.Contains("--unshare-pid", StringComparer.Ordinal).ShouldBeTrue();
        argv.Contains("--die-with-parent", StringComparer.Ordinal).ShouldBeTrue();
        argv.Contains("--new-session", StringComparer.Ordinal).ShouldBeTrue();
        argv.Contains("/home/ada/notes", StringComparer.Ordinal).ShouldBeFalse();
    }

    /// <summary>Code defaults to off and Cowork defaults to on.</summary>
    [Fact]
    public void CodeDefaultsOffAndCoworkDefaultsOn()
    {
        IsolationPolicy.ForCode().Enabled.ShouldBeFalse();
        GoldenPolicy().Enabled.ShouldBeTrue();
    }

    /// <summary>The scrub sets the socket environment names to null and leaves the rest.</summary>
    [Fact]
    public void ScrubUnsetsSocketEnvironmentNames()
    {
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["PATH"] = "/bin",
            ["DISPLAY"] = ":0",
            ["SSH_AUTH_SOCK"] = "/tmp/ssh",
        };

        var scrubbed = BubblewrapArgvBuilder.Scrub(environment);

        scrubbed["PATH"].ShouldBe("/bin");
        scrubbed["DISPLAY"].ShouldBeNull();
        scrubbed["SSH_AUTH_SOCK"].ShouldBeNull();
        scrubbed["DBUS_SESSION_BUS_ADDRESS"].ShouldBeNull();
        scrubbed["WAYLAND_DISPLAY"].ShouldBeNull();
        environment["DISPLAY"].ShouldBe(":0");
    }

    internal static IsolationPolicy GoldenPolicy() =>
        IsolationPolicy.ForCowork(
            "/home/ada",
            "/tmp/scratch-session",
            [
                new IsolationGrant("/home/ada/project", true),
                new IsolationGrant("/home/ada/notes", false),
            ],
            "/opt/pi-agent");

    internal static string[] FixtureLines(string name) =>
        File.ReadAllLines(FixturePath(name));

    internal static string FixtureText(string name) =>
        File.ReadAllText(FixturePath(name));

    private static string FixturePath(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "tests", "Autonogrammer.Harness.Isolation.Tests", "Fixtures", name);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            var local = Path.Combine(directory.FullName, "Fixtures", name);
            if (File.Exists(local))
            {
                return local;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(name);
    }
}
