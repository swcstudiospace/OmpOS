using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace OmpOS.ArchitectureTests;

/// <summary>
/// Gate parity (v012 unit 35-A): bootstrap.sh and the CI workflow run the same secret scan, and the gitleaks
/// version and SHA-256 pin live in one file, so the local gate cannot drift from CI.
/// </summary>
public sealed partial class GateParityTests
{
    private const string ScanScript = "scripts/ci/secret-scan.sh";
    private const string PinFile = "scripts/ci/gitleaks.env";

    [Fact]
    public void BootstrapAndCiRunTheSameSecretScanAndVersionPin()
    {
        var bootstrap = CodeLines("bootstrap.sh");
        var workflow = CodeLines(".github/workflows/ci.yml");

        // Both gates run the one shared script, so they get the same pinned binary and the same flags.
        bootstrap.Count(RunsScanScript).ShouldBe(1, "bootstrap.sh must run " + ScanScript + " exactly once");
        workflow.Count(RunsScanScript).ShouldBe(1, "ci.yml must run " + ScanScript + " exactly once");

        // Neither caller carries its own copy of the pin, the download or the flags.
        foreach (var (name, lines) in new[] { ("bootstrap.sh", bootstrap), (".github/workflows/ci.yml", workflow) })
        {
            lines.Where(line => line.Contains("GITLEAKS_", StringComparison.Ordinal)
                    || (line.Contains("releases/download/v", StringComparison.Ordinal)
                        && line.Contains("gitleaks", StringComparison.OrdinalIgnoreCase))
                    || line.Contains("--log-opts", StringComparison.Ordinal))
                .ShouldBeEmpty(name + " must not duplicate the gitleaks pin, download or flags; they live in " + PinFile + " and " + ScanScript);
        }

        // The shared script reads the shared pin and carries the digest check and the scan flags.
        var script = CodeLines(ScanScript);
        script.ShouldContain(line => SourcesPinFile(line), ScanScript + " must source " + PinFile);
        script.ShouldContain(line => line.Contains("sha256sum -c", StringComparison.Ordinal) && line.Contains("${GITLEAKS_SHA256}", StringComparison.Ordinal),
            ScanScript + " must verify the tarball against GITLEAKS_SHA256");
        script.ShouldContain(line => line.Contains("${GITLEAKS_VERSION}", StringComparison.Ordinal) && line.Contains("releases/download/", StringComparison.Ordinal),
            ScanScript + " must download the pinned GITLEAKS_VERSION");
        script.ShouldContain(
            line => line.Contains("gitleaks\" git --redact --exit-code 1 --log-level warn --log-opts=\"--full-history HEAD\" .", StringComparison.Ordinal),
            ScanScript + " must run `gitleaks git --redact --exit-code 1 --log-level warn --log-opts=\"--full-history HEAD\" .`, the flags CI has always used");
    }

    [Fact]
    public void GitleaksPinFileCarriesAVersionAndADigestAndNoOtherAssignment()
    {
        var assignments = CodeLines(PinFile);
        assignments.Count.ShouldBe(2, PinFile + " must hold exactly GITLEAKS_VERSION and GITLEAKS_SHA256");

        assignments.ShouldContain(line => VersionAssignment().IsMatch(line), "GITLEAKS_VERSION must be a plain semantic version");
        assignments.ShouldContain(line => DigestAssignment().IsMatch(line), "GITLEAKS_SHA256 must be 64 lowercase hex characters");
    }

    [Fact]
    public void SecretScanFailsClosedAndOnlyAnExplicitSkipDisablesIt()
    {
        var script = CodeLines(ScanScript);

        // The skip is exactly OMPOS_SKIP_SECRET_SCAN=1, and the failure path points at it.
        script.ShouldContain(line => line.Contains("\"${OMPOS_SKIP_SECRET_SCAN:-}\" = \"1\"", StringComparison.Ordinal));
        script.ShouldContain(line => line.Contains("exit 1", StringComparison.Ordinal), "an unverifiable or unfetchable gitleaks must exit non-zero");
        script.ShouldContain(line => line.Contains("cannot download", StringComparison.Ordinal), "the offline failure must say why");

        // The skip is documented in the bootstrap header, and CI never sets it.
        var bootstrapHeader = File.ReadAllLines(RepoPath("bootstrap.sh")).TakeWhile(static line => line.StartsWith('#') || line.Length == 0);
        bootstrapHeader.ShouldContain(line => line.Contains("OMPOS_SKIP_SECRET_SCAN=1", StringComparison.Ordinal), "bootstrap.sh header must document OMPOS_SKIP_SECRET_SCAN=1");
        CodeLines(".github/workflows/ci.yml").ShouldNotContain(
            line => line.Contains("OMPOS_SKIP_SECRET_SCAN", StringComparison.Ordinal),
            "CI must never skip the secret scan");
    }

    [GeneratedRegex(@"^GITLEAKS_VERSION=\d+\.\d+\.\d+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex VersionAssignment();

    [GeneratedRegex(@"^GITLEAKS_SHA256=[0-9a-f]{64}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex DigestAssignment();

    /// <summary>True when a code line runs the shared secret-scan script (not merely mentions it in a comment).</summary>
    private static bool RunsScanScript(string line) => line.Contains("bash " + ScanScript, StringComparison.Ordinal);

    /// <summary>True when a code line sources the shared pin file.</summary>
    private static bool SourcesPinFile(string line) => line.StartsWith("source " + PinFile, StringComparison.Ordinal);

    private static string RepoPath(string relative) =>
        Path.Combine(ProjectGraphChecker.FindRepoRoot(), relative.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>The trimmed non-blank, non-comment lines of a repository file.</summary>
    private static List<string> CodeLines(string relative)
    {
        var path = RepoPath(relative);
        File.Exists(path).ShouldBeTrue("expected " + relative);
        return File.ReadAllLines(path)
            .Select(static line => line.Trim())
            .Where(static line => line.Length > 0 && !line.StartsWith('#'))
            .ToList();
    }
}
