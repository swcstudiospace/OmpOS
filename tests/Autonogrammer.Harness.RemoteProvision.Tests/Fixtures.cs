using Autonogrammer.Contracts.Environments;
using Autonogrammer.Harness.OmpLauncher;

namespace Autonogrammer.Harness.RemoteProvision.Tests;

/// <summary>Probe answers, desired-state records and small fakes shared by the provisioning tests.</summary>
internal static class Fixtures
{
    public const string Home = "/home/tester";
    public const string UltrathinkSource = "https://github.com/swcstudiospace/ultrathink.git";
    public const string LatestVersion = "18.18.1";

    public static readonly string CommitA = new('a', 40);
    public static readonly string CommitB = new('b', 40);
    public static readonly string CommitC = new('c', 40);
    public static readonly string Sha512 = new('d', 128);

    public static readonly Uri TarballUri = new("https://registry.npmjs.org/@oh-my-pi/pi-coding-agent/-/pi-coding-agent-18.18.1.tgz");

    /// <summary>Builds one probe answer, the way the remote probe prints it. Missing tools are listed by name; a null omp or Bun version means absent.</summary>
    public static string Probe(
        string kernel = "Linux",
        string machine = "x86_64",
        string? ompVersion = null,
        string? bunVersion = "1.4.4",
        string? bundleJson = null,
        string[]? missingTools = null)
    {
        var missing = missingTools ?? Array.Empty<string>();
        var lines = new List<string>
        {
            Fact("system", kernel),
            Fact("machine", machine),
            Fact("home", Home),
        };
        foreach (var tool in new[] { "bash", "curl", "tar", "unzip", "git" })
        {
            lines.Add(Fact("tool_" + tool, missing.Contains(tool) ? "no" : "yes"));
        }

        lines.Add(Fact("tool_sha256", missing.Contains("sha256") ? "no" : "yes"));
        lines.Add(Fact("tool_sha512", missing.Contains("sha512") ? "no" : "yes"));
        lines.Add(Fact("omp_path", ompVersion is null ? "ABSENT" : Home + "/.local/bin/omp"));
        lines.Add(Fact("omp_version", ompVersion ?? "ABSENT"));
        lines.Add(Fact("bun_version", bunVersion ?? "ABSENT"));
        lines.Add(Fact("bundle", bundleJson ?? "ABSENT"));
        return string.Join('\n', lines) + "\n";
    }

    /// <summary>Builds a remote bundle record as the probe reads it: one entry per directory and commit.</summary>
    public static string RemoteBundle(params (string Dir, string Commit)[] entries) =>
        "[" + string.Join(',', entries.Select(static entry =>
            "{\"dir\":\"" + entry.Dir + "\",\"source\":\"in-repo\",\"commit\":\"" + entry.Commit + "\",\"licenseFile\":true,\"needsInstall\":false}")) + "]";

    /// <summary>The desired release: the latest omp, with a registry SHA-512 and the engines range given.</summary>
    public static OmpRelease Latest(string? enginesBun = ">=1.4.4") => new(LatestVersion, TarballUri, Sha512, enginesBun);

    /// <summary>A plugin that ships inside the checkout.</summary>
    public static PluginBundleRecord InRepo(string commit = "") =>
        new("gsd-core-omp", RemoteProvisionRules.InRepoSource, commit.Length == 0 ? CommitA : commit, LicenseFile: true, NeedsInstall: false);

    /// <summary>A plugin that a remote machine can clone.</summary>
    public static PluginBundleRecord Ultrathink(string commit = "") =>
        new("claude-ultrathink", UltrathinkSource, commit.Length == 0 ? CommitB : commit, LicenseFile: true, NeedsInstall: false);

    /// <summary>The desktop's bundle record used by most tests: one in-repo plugin and one clonable plugin.</summary>
    public static IReadOnlyList<PluginBundleRecord> Bundle() => [InRepo(), Ultrathink()];

    /// <summary>A profile whose omp is provisioned here (no custom path), or the given custom path.</summary>
    public static SshProfile Profile(string? ompPath = null) => new()
    {
        Alias = "remote-a",
        RemoteWorkspaceRoot = "/work",
        RemoteOmpPath = ompPath,
    };

    /// <summary>A request for one environment.</summary>
    public static RemoteProvisionRequest Request(RemoteState? lastKnown = null, SshProfile? profile = null, IReadOnlyList<PluginBundleRecord>? bundle = null) =>
        new("env-1", profile ?? Profile(), bundle ?? Bundle(), lastKnown);

    /// <summary>The probe answer of a machine before any install: old Bun, no omp, and only the in-repo plugin recorded.</summary>
    public static string BeforeInstall() => Probe(ompVersion: null, bunVersion: "1.3.0", bundleJson: RemoteBundle(("gsd-core-omp", CommitA)));

    /// <summary>The probe answer of the same machine after the three installs: current Bun, current omp, both plugins recorded.</summary>
    public static string AfterInstall() => Probe(
        ompVersion: LatestVersion,
        bunVersion: OmpBunPin.Default.Version,
        bundleJson: RemoteBundle(("gsd-core-omp", CommitA), ("claude-ultrathink", CommitB)));

    private static string Fact(string key, string value) => "AUTONOGRAMMER_PROBE " + key + "=" + value;
}
