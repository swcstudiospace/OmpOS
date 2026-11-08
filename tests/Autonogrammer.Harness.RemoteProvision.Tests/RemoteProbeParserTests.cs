using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.RemoteProvision.Tests;

/// <summary>The probe's answer is read strictly: a complete answer gives every fact, and anything else is a failure, never a guess.</summary>
public sealed class RemoteProbeParserTests
{
    private static readonly DateTimeOffset Observed = new(2026, 11, 8, 9, 0, 0, TimeSpan.Zero);

    /// <summary>A complete answer gives every fact with its shape.</summary>
    [Fact]
    public void ValidAnswerParsesEveryFact()
    {
        var answer = Fixtures.Probe(ompVersion: "18.18.1", bunVersion: "1.4.4", bundleJson: Fixtures.RemoteBundle(("gsd-core-omp", Fixtures.CommitA)));

        var outcome = RemoteProbeParser.Parse(answer, Observed);

        outcome.Failure.ShouldBeNull();
        var state = outcome.State.ShouldNotBeNull();
        state.ObservedAt.ShouldBe(Observed);
        state.KernelName.ShouldBe("Linux");
        state.Machine.ShouldBe("x86_64");
        state.Home.ShouldBe(Fixtures.Home);
        state.OmpPath.ShouldBe(Fixtures.Home + "/.local/bin/omp");
        state.OmpVersion.ShouldBe("18.18.1");
        state.BunVersion.ShouldBe("1.4.4");
        state.PluginBundlePresent.ShouldBeTrue();
        state.Plugins.Single().Dir.ShouldBe("gsd-core-omp");
        state.Plugins.Single().Commit.ShouldBe(Fixtures.CommitA);
        state.Tools.ShouldBe(new RemoteTools(Bash: true, Curl: true, Tar: true, Unzip: true, Git: true, Sha256: true, Sha512: true));
    }

    /// <summary>Absent omp, absent Bun and no bundle record are facts too, and they read as null or empty.</summary>
    [Fact]
    public void AbsentExecutablesAndBundleReadAsNullAndEmpty()
    {
        var outcome = RemoteProbeParser.Parse(Fixtures.Probe(ompVersion: null, bunVersion: null), Observed);

        var state = outcome.State.ShouldNotBeNull();
        state.OmpPath.ShouldBeNull();
        state.OmpVersion.ShouldBeNull();
        state.BunVersion.ShouldBeNull();
        state.PluginBundlePresent.ShouldBeFalse();
        state.Plugins.ShouldBeEmpty();
    }

    /// <summary>Text with no probe facts is a parse failure with no state.</summary>
    [Fact]
    public void GarbageIsAParseFailureWithNoState()
    {
        var outcome = RemoteProbeParser.Parse("bash: command not found\nsomething else entirely", Observed);

        outcome.State.ShouldBeNull();
        outcome.Failure.ShouldNotBeNullOrWhiteSpace();
    }

    /// <summary>A fact that is missing fails the answer, and the failure names the fact.</summary>
    [Fact]
    public void MissingFactFailsTheAnswerAndNamesIt()
    {
        var answer = Fixtures.Probe().Replace("AUTONOGRAMMER_PROBE bun_version=1.4.4\n", string.Empty, StringComparison.Ordinal);

        var outcome = RemoteProbeParser.Parse(answer, Observed);

        outcome.State.ShouldBeNull();
        outcome.Failure.ShouldNotBeNull().ShouldContain("bun_version");
    }

    /// <summary>A fact the probe does not define fails the answer rather than being ignored.</summary>
    [Fact]
    public void UnknownFactFailsTheAnswer()
    {
        var answer = Fixtures.Probe() + "AUTONOGRAMMER_PROBE gpu=yes\n";

        var outcome = RemoteProbeParser.Parse(answer, Observed);

        outcome.State.ShouldBeNull();
        outcome.Failure.ShouldNotBeNull().ShouldContain("gpu");
    }

    /// <summary>A fact given twice fails the answer, because the second value could be the one the shell did not mean.</summary>
    [Fact]
    public void DuplicateFactFailsTheAnswer()
    {
        var answer = Fixtures.Probe() + "AUTONOGRAMMER_PROBE home=/elsewhere\n";

        var outcome = RemoteProbeParser.Parse(answer, Observed);

        outcome.State.ShouldBeNull();
        outcome.Failure.ShouldNotBeNull().ShouldContain("home");
    }

    /// <summary>An omp version line that is an error message is not a version: the answer fails.</summary>
    [Fact]
    public void OmpVersionThatIsNotAVersionFailsTheAnswer()
    {
        var answer = Fixtures.Probe(ompVersion: "Error: segmentation fault");

        var outcome = RemoteProbeParser.Parse(answer, Observed);

        outcome.State.ShouldBeNull();
        outcome.Failure.ShouldNotBeNull().ShouldContain("omp --version");
    }

    /// <summary>An omp that resolves but prints no version is a contradiction, so the answer fails rather than reading omp as absent.</summary>
    [Fact]
    public void OmpOnThePathWithoutVersionFailsTheAnswer()
    {
        var answer = Fixtures.Probe(ompVersion: "18.18.1").Replace("omp_version=18.18.1", "omp_version=ABSENT", StringComparison.Ordinal);

        var outcome = RemoteProbeParser.Parse(answer, Observed);

        outcome.State.ShouldBeNull();
        outcome.Failure.ShouldNotBeNull().ShouldContain("omp_version");
    }

    /// <summary>A bundle record that is not valid JSON, or names a commit that is not a full commit, fails the answer.</summary>
    [Theory]
    [InlineData("[{")]
    [InlineData("{\"dir\":\"x\"}")]
    public void BrokenBundleRecordFailsTheAnswer(string bundle)
    {
        var outcome = RemoteProbeParser.Parse(Fixtures.Probe(bundleJson: bundle), Observed);

        outcome.State.ShouldBeNull();
        outcome.Failure.ShouldNotBeNullOrWhiteSpace();
    }

    /// <summary>A bundle entry whose directory is a path, not one name, fails the answer.</summary>
    [Fact]
    public void BundleEntryWithAPathDirectoryFailsTheAnswer()
    {
        var bundle = Fixtures.RemoteBundle(("../escape", Fixtures.CommitA));

        var outcome = RemoteProbeParser.Parse(Fixtures.Probe(bundleJson: bundle), Observed);

        outcome.State.ShouldBeNull();
        outcome.Failure.ShouldNotBeNull().ShouldContain("directory");
    }

    /// <summary>Lines without the probe prefix, such as a banner, do not spoil a complete answer.</summary>
    [Fact]
    public void LinesWithoutThePrefixAreIgnored()
    {
        var answer = "Welcome to the host\n" + Fixtures.Probe();

        var outcome = RemoteProbeParser.Parse(answer, Observed);

        outcome.Failure.ShouldBeNull();
        outcome.State.ShouldNotBeNull();
    }
}
