using Autonogrammer.Harness.OmpLauncher;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.RemoteProvision.Tests;

/// <summary>The generated scripts carry the pins, verify before they unpack, write their marker last, and quote every value.</summary>
public sealed class RemoteProvisionScriptTests
{
    private static readonly Uri GitHub = new("https://github.com/");

    /// <summary>The Bun script carries the pinned archive name, its pinned SHA-256 and the release address, and verifies before it unpacks.</summary>
    [Fact]
    public void BunScriptPinsTheArchiveAndVerifiesBeforeUnpacking()
    {
        var asset = OmpBunPin.Default.AssetFor(OmpBunPin.LinuxX64).ShouldNotBeNull();
        var script = RemoteProvisionScripts.Bun(new BunInstallAction("summary", OmpBunPin.Default.Version, OmpBunPin.LinuxX64, asset), GitHub);

        script.ShouldContain("'https://github.com/oven-sh/bun/releases/download/bun-v1.4.4/bun-linux-x64.zip'");
        script.ShouldContain("'bf8ccbfb2b0b80159e0f9a6d15c55317add8077534dbc6130a3edb52fccb395c'");
        script.IndexOf("sha256_of \"$tmp/bun.zip\"", StringComparison.Ordinal).ShouldBeLessThan(script.IndexOf("unzip -q", StringComparison.Ordinal));
    }

    /// <summary>The omp script checks the registry SHA-512 and the tarball's paths before anything is unpacked, and builds with the pinned Bun.</summary>
    [Fact]
    public void OmpScriptVerifiesTheSha512BeforeUnpacking()
    {
        var script = RemoteProvisionScripts.Omp(new OmpInstallAction("summary", Fixtures.Latest(), OmpBunPin.LinuxX64));

        script.ShouldContain("'" + Fixtures.Sha512 + "'");
        script.ShouldContain("'" + Fixtures.TarballUri.AbsoluteUri + "'");
        var verify = script.IndexOf("sha512_of \"$tmp/omp.tgz\"", StringComparison.Ordinal);
        verify.ShouldBeGreaterThan(0);
        verify.ShouldBeLessThan(script.IndexOf("tar -xzf", StringComparison.Ordinal));
        script.ShouldContain("grep -Eq");
        script.ShouldContain("\"$HOME/.bun/bin/bun\" build --compile dist/cli.js");
    }

    /// <summary>Each script writes its marker as its last statement, so a script that dies half way leaves no marker behind.</summary>
    [Fact]
    public void EveryScriptWritesItsMarkerLast()
    {
        var asset = OmpBunPin.Default.AssetFor(OmpBunPin.LinuxX64).ShouldNotBeNull();
        var bun = RemoteProvisionScripts.Bun(new BunInstallAction("s", OmpBunPin.Default.Version, OmpBunPin.LinuxX64, asset), GitHub);
        var omp = RemoteProvisionScripts.Omp(new OmpInstallAction("s", Fixtures.Latest(), OmpBunPin.LinuxX64));
        var plugin = RemoteProvisionScripts.Plugin(new PluginInstallAction("s", Fixtures.Ultrathink()), "[]");

        LastLine(bun).ShouldContain("bun.version");
        LastLine(omp).ShouldContain("omp.version");
        LastLine(plugin).ShouldContain("bundle.json");
    }

    /// <summary>The plugin script pins the commit, verifies it with git, and carries the bundle record it writes last.</summary>
    [Fact]
    public void PluginScriptPinsTheCommitAndCarriesTheBundleRecord()
    {
        const string bundle = "[{\"dir\":\"claude-ultrathink\",\"commit\":\"x\"}]";
        var script = RemoteProvisionScripts.Plugin(new PluginInstallAction("s", Fixtures.Ultrathink()), bundle);

        script.ShouldContain("'" + Fixtures.UltrathinkSource + "'");
        script.ShouldContain("checkout --quiet --detach '" + Fixtures.CommitB + "'");
        script.ShouldContain("rev-parse HEAD </dev/null)\" = '" + Fixtures.CommitB + "'");
        script.ShouldContain("'" + bundle + "'");
        script.ShouldContain("GIT_TERMINAL_PROMPT=0");
    }

    /// <summary>Every command that could read the piped script has its stdin closed, so none consumes the rest of the script.</summary>
    [Fact]
    public void ScriptsCloseStdinOfEveryCommandThatCouldReadIt()
    {
        var asset = OmpBunPin.Default.AssetFor(OmpBunPin.LinuxX64).ShouldNotBeNull();
        var scripts = new[]
        {
            RemoteProvisionScripts.Bun(new BunInstallAction("s", OmpBunPin.Default.Version, OmpBunPin.LinuxX64, asset), GitHub),
            RemoteProvisionScripts.Omp(new OmpInstallAction("s", Fixtures.Latest(), OmpBunPin.LinuxX64)),
            RemoteProvisionScripts.Plugin(new PluginInstallAction("s", Fixtures.Ultrathink()), "[]"),
        };

        foreach (var script in scripts)
        {
            foreach (var line in script.Split('\n').Where(static line => line.Contains("curl ", StringComparison.Ordinal) || line.Contains("git ", StringComparison.Ordinal)))
            {
                line.ShouldContain("</dev/null");
            }
        }
    }

    /// <summary>A value that carries shell syntax is inert: it is single-quoted, with its own quote escaped.</summary>
    [Fact]
    public void ValuesThatCarryShellSyntaxAreQuotedAndInert()
    {
        var hostile = new PluginBundleRecord("x'$(touch /tmp/pwned)", "https://example.org/r.git", Fixtures.CommitB, LicenseFile: true, NeedsInstall: false);

        var script = RemoteProvisionScripts.Plugin(new PluginInstallAction("s", hostile), "[]");

        script.ShouldContain("'x'\\''$(touch /tmp/pwned)'");
    }

    /// <summary>The probe is one line, writes nothing, and sets up the same PATH as the other remote commands.</summary>
    [Fact]
    public void ProbeIsOneLineReadOnlyAndUsesTheSessionPath()
    {
        var script = RemoteProbe.Script;

        script.ShouldNotContain("\n");
        script.ShouldStartWith(Autonogrammer.Harness.Remote.SshCommandBuilder.PathSetup);
        script.ShouldNotContain("mkdir");
        script.ShouldNotContain(" rm ");
        script.ShouldNotContain(" mv ");
        script.ShouldNotContain("install ");
    }

    /// <summary>The registry's SHA-512 is uppercase hex, and sha512sum prints lowercase, so the script compares the lowercase form.</summary>
    [Fact]
    public void OmpScriptComparesTheRegistryDigestInLowercase()
    {
        var uppercase = new OmpRelease(Fixtures.LatestVersion, Fixtures.TarballUri, new string('D', 128), ">=1.4.4");

        var script = RemoteProvisionScripts.Omp(new OmpInstallAction("summary", uppercase, OmpBunPin.LinuxX64));

        script.ShouldContain("'" + new string('d', 128) + "'");
        script.ShouldNotContain(new string('D', 128), Case.Sensitive);
    }

    private static string LastLine(string script) => script.TrimEnd('\n').Split('\n')[^1];
}
