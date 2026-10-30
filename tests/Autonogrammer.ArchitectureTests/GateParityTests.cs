using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace Autonogrammer.ArchitectureTests;

/// <summary>
/// Gate parity (v012 unit 35-A): bootstrap.sh and the CI workflow run the same secret scan, and the gitleaks
/// version and SHA-256 pin live in one file, so the local gate cannot drift from CI.
/// </summary>
public sealed partial class GateParityTests
{
    private const string ScanScript = "scripts/ci/secret-scan.sh";
    private const string PinFile = "scripts/ci/gitleaks.env";
    private const string RepoChecksScript = "scripts/ci/repo-checks.sh";
    private const string LicenceScript = "scripts/ci/check-licences.sh";

    private static readonly string[] RepositoryChecks =
    [
        "scripts/ci/check-brand.sh",
        "scripts/ci/check-changelog.sh",
        "scripts/ci/check-docs.sh",
        "scripts/ci/check-providers-doc.sh",
        "scripts/check-settings-parity.sh",
    ];

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
    public void BootstrapAndCiRunTheSameRepositoryChecks()
    {
        var bootstrap = CodeLines("bootstrap.sh");
        var workflow = CodeLines(".github/workflows/ci.yml");

        foreach (var script in new[] { RepoChecksScript, LicenceScript })
        {
            bootstrap.Count(line => RunsScript(line, script)).ShouldBe(1, "bootstrap.sh must run " + script + " exactly once");
            workflow.Count(line => RunsScript(line, script)).ShouldBe(1, "ci.yml must run " + script + " exactly once");
        }

        // The licence check reads the NuGet cache, so both callers run it after a restore.
        foreach (var (name, lines) in new[] { ("bootstrap.sh", bootstrap), (".github/workflows/ci.yml", workflow) })
        {
            var restore = lines.FindIndex(static line => line.Contains("dotnet restore", StringComparison.Ordinal));
            var licences = lines.FindIndex(line => RunsScript(line, LicenceScript));
            restore.ShouldBeGreaterThanOrEqualTo(0, name + " must restore");
            licences.ShouldBeGreaterThan(restore, name + " must run " + LicenceScript + " after the restore");
        }

        // The shared script runs every static check, so neither caller can run a subset.
        var checks = CodeLines(RepoChecksScript);
        foreach (var check in RepositoryChecks)
        {
            checks.ShouldContain(line => RunsScript(line, check), RepoChecksScript + " must run " + check);
        }
    }

    [Fact]
    public void BootstrapAndCiStartAndStopTheSameTestServices()
    {
        const string callerScript = "scripts/test-services.sh";
        var bootstrap = CodeLines("bootstrap.sh");
        var workflow = CodeLines(".github/workflows/ci.yml");

        // Both callers start the services with the same arguments. Plain `up` is the contract: neither passes --with-greptime,
        // so the container tests that need only PostgreSQL and Dragonfly run identically in the local gate and in CI.
        var bootstrapUp = bootstrap.FindAll(line => TestServicesUp().IsMatch(line));
        var workflowUp = workflow.FindAll(line => TestServicesUp().IsMatch(line));
        bootstrapUp.Count.ShouldBe(1, "bootstrap.sh must run `bash " + callerScript + " up` exactly once");
        workflowUp.Count.ShouldBe(1, "ci.yml must run `bash " + callerScript + " up` exactly once");
        var bootstrapArguments = TestServicesUp().Match(bootstrapUp[0]).Groups["arguments"].Value.Trim();
        var workflowArguments = TestServicesUp().Match(workflowUp[0]).Groups["arguments"].Value.Trim();
        bootstrapArguments.ShouldBe(workflowArguments, "bootstrap.sh and ci.yml must start the test services with the same arguments");
        bootstrapArguments.ShouldBeEmpty("both gate paths run plain `up`");

        // bootstrap.sh stops them on every exit path: `down` sits inside the cleanup function that an EXIT trap runs,
        // and the trap is installed before `up`.
        var cleanupStart = bootstrap.FindIndex(static line => line.StartsWith("cleanup() {", StringComparison.Ordinal));
        var trap = bootstrap.FindIndex(static line => string.Equals(line, "trap cleanup EXIT", StringComparison.Ordinal));
        var bootstrapDown = bootstrap.FindAll(line => RunsScript(line, callerScript) && line.EndsWith(" down", StringComparison.Ordinal));
        cleanupStart.ShouldBeGreaterThanOrEqualTo(0, "bootstrap.sh must define cleanup()");
        trap.ShouldBeGreaterThan(cleanupStart, "bootstrap.sh must install `trap cleanup EXIT` after defining cleanup()");
        trap.ShouldBeLessThan(bootstrap.IndexOf(bootstrapUp[0]), "the EXIT trap must be installed before the test services start");
        bootstrapDown.Count.ShouldBe(1, "bootstrap.sh must run `bash " + callerScript + " down` exactly once");
        var cleanupEnd = bootstrap.FindIndex(cleanupStart, static line => string.Equals(line, "}", StringComparison.Ordinal));
        var downIndex = bootstrap.IndexOf(bootstrapDown[0]);
        downIndex.ShouldBeGreaterThan(cleanupStart, "down must be inside cleanup()");
        downIndex.ShouldBeLessThan(cleanupEnd, "down must be inside cleanup()");

        // ci.yml stops them in a later step that always runs.
        var workflowDown = workflow.FindAll(line => RunsScript(line, callerScript) && line.EndsWith(" down", StringComparison.Ordinal));
        workflowDown.Count.ShouldBe(1, "ci.yml must run `bash " + callerScript + " down` exactly once");
        var upLine = workflow.IndexOf(workflowUp[0]);
        var workflowDownLine = workflow.IndexOf(workflowDown[0]);
        workflowDownLine.ShouldBeGreaterThan(upLine, "the stop step must follow the start step");
        var stepStart = workflow.FindLastIndex(workflowDownLine, static line => line.StartsWith("- name:", StringComparison.Ordinal));
        stepStart.ShouldBeGreaterThan(upLine, "the stop step must be its own step after the start step");
        workflow.GetRange(stepStart, workflowDownLine - stepStart)
            .ShouldContain(static line => string.Equals(line, "if: always()", StringComparison.Ordinal), "the stop step must run `if: always()`");

        // Container names live only in the script, the compose file and the tests; the callers never name one.
        foreach (var path in new[] { "bootstrap.sh", ".github/workflows/ci.yml" })
        {
            File.ReadAllText(RepoPath(path)).ShouldNotContain("autonogrammer-test-", Case.Sensitive, path + " must not name a test container");
        }
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

        // The skip is exactly AUTONOGRAMMER_SKIP_SECRET_SCAN=1, and the failure path points at it.
        script.ShouldContain(line => line.Contains("\"${AUTONOGRAMMER_SKIP_SECRET_SCAN:-}\" = \"1\"", StringComparison.Ordinal));
        script.ShouldContain(line => line.Contains("exit 1", StringComparison.Ordinal), "an unverifiable or unfetchable gitleaks must exit non-zero");
        script.ShouldContain(line => line.Contains("cannot download", StringComparison.Ordinal), "the offline failure must say why");

        // The skip is documented in the bootstrap header, and CI never sets it.
        var bootstrapHeader = File.ReadAllLines(RepoPath("bootstrap.sh")).TakeWhile(static line => line.StartsWith('#') || line.Length == 0);
        bootstrapHeader.ShouldContain(line => line.Contains("AUTONOGRAMMER_SKIP_SECRET_SCAN=1", StringComparison.Ordinal), "bootstrap.sh header must document AUTONOGRAMMER_SKIP_SECRET_SCAN=1");
        CodeLines(".github/workflows/ci.yml").ShouldNotContain(
            line => line.Contains("AUTONOGRAMMER_SKIP_SECRET_SCAN", StringComparison.Ordinal),
            "CI must never skip the secret scan");
    }

    [GeneratedRegex(@"bash scripts/test-services\.sh up\b(?<arguments>[^)""]*)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex TestServicesUp();

    [GeneratedRegex(@"^GITLEAKS_VERSION=\d+\.\d+\.\d+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex VersionAssignment();

    [GeneratedRegex(@"^GITLEAKS_SHA256=[0-9a-f]{64}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex DigestAssignment();

    /// <summary>True when a code line runs the shared secret-scan script (not merely mentions it in a comment).</summary>
    private static bool RunsScanScript(string line) => line.Contains("bash " + ScanScript, StringComparison.Ordinal);

    /// <summary>True when a code line runs the given repository script through bash (not merely mentions it in a comment).</summary>
    private static bool RunsScript(string line, string script) => line.Contains("bash " + script, StringComparison.Ordinal);

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
