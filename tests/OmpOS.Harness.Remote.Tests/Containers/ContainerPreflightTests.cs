using OmpOS.Contracts.Environments;
using OmpOS.Harness.Remote.Containers;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Remote.Tests;

/// <summary>The connection test of a container: every way a container environment can be wrong becomes a typed diagnosis with fix text.</summary>
public sealed class ContainerPreflightTests
{
    private static Task<RemotePreflightReport> RunAsync(FakeContainerHost host, ContainerProfile? profile = null, CancellationToken cancellationToken = default) =>
        new ContainerPreflight(host.ClientOptions).RunAsync(profile ?? host.Profile, cancellationToken);

    private static RemoteDiagnosis Only(RemotePreflightReport report)
    {
        report.Succeeded.ShouldBeFalse();
        return report.Diagnoses.ShouldHaveSingleItem();
    }

    /// <summary>A healthy running container passes and the report tells what was found.</summary>
    [Fact]
    public async Task AHealthyContainerPasses()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeContainerHost.Create();

        var report = await RunAsync(host, cancellationToken: ct).ConfigureAwait(true);

        report.Succeeded.ShouldBeTrue();
        report.Diagnoses.ShouldBeEmpty();
        report.HomeDirectory.ShouldBe(host.HomeDirectory);
        report.Platform.ShouldBe("Linux x86_64");
        report.OmpVersion.ShouldBe("18.12.6");
        report.OmpPath.ShouldBe(Path.Combine(host.HomeDirectory, ".bun", "bin", "omp"));
    }

    /// <summary>The test is three podman calls in order: version, inspect of the container, one exec of the probe. It never starts anything.</summary>
    [Fact]
    public async Task TheTestIsVersionInspectAndOneProbe()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeContainerHost.Create();

        _ = await RunAsync(host, cancellationToken: ct).ConfigureAwait(true);

        var calls = host.PodmanCalls();
        calls.Count.ShouldBe(3);
        calls[0].ToArray().ShouldBe(["--version"]);
        calls[1].ToArray().ShouldBe(["container", "inspect", "--format", "{{.State.Status}}", "--", "devbox"]);
        calls[2].Take(4).ToArray().ShouldBe(["exec", "-i", "--", "devbox"]);
        calls.ShouldNotContain(call => call[0] == "start");
    }

    /// <summary>A banner in the exec output does not disturb the answer.</summary>
    [Fact]
    public async Task ABannerDoesNotDisturbTheAnswer()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeContainerHost.Create(new FakeContainerHostOptions { Banner = true });

        var report = await RunAsync(host, cancellationToken: ct).ConfigureAwait(true);

        report.Succeeded.ShouldBeTrue();
        report.OmpVersion.ShouldBe("18.12.6");
    }

    /// <summary>A missing podman is one diagnosis with the install fix.</summary>
    [Fact]
    public async Task AMissingPodmanIsDiagnosed()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeContainerHost.Create();
        var preflight = new ContainerPreflight(new PodmanClientOptions { PodmanPath = "/nonexistent/ompos-test-podman" });

        var report = await preflight.RunAsync(host.Profile, ct).ConfigureAwait(true);

        var diagnosis = Only(report);
        diagnosis.Kind.ShouldBe(RemoteDiagnosisKind.ContainerRuntimeMissing);
        diagnosis.FixText.ShouldContain("Install podman", Case.Sensitive);
    }

    /// <summary>A podman whose service does not answer is the runtime-unavailable diagnosis with the detail podman printed.</summary>
    [Fact]
    public async Task ADownServiceIsDiagnosed()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeContainerHost.Create(new FakeContainerHostOptions { Mode = "service-down" });

        var report = await RunAsync(host, cancellationToken: ct).ConfigureAwait(true);

        var diagnosis = Only(report);
        diagnosis.Kind.ShouldBe(RemoteDiagnosisKind.ContainerRuntimeUnavailable);
        diagnosis.Detail.ShouldNotBeNull().ShouldContain("Cannot connect to Podman", Case.Sensitive);
    }

    /// <summary>A container that does not exist is diagnosed with its name and without any exec.</summary>
    [Fact]
    public async Task AMissingContainerIsDiagnosed()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeContainerHost.Create();

        var report = await RunAsync(host, host.Profile with { Container = "ghost" }, ct).ConfigureAwait(true);

        var diagnosis = Only(report);
        diagnosis.Kind.ShouldBe(RemoteDiagnosisKind.ContainerMissing);
        diagnosis.Summary.ShouldContain("ghost", Case.Sensitive);
        host.PodmanCalls().ShouldNotContain(call => call[0] == "exec");
    }

    /// <summary>A stopped container is diagnosed with the <c>podman start</c> fix; the test leaves it stopped.</summary>
    [Theory]
    [InlineData("exited")]
    [InlineData("created")]
    [InlineData("paused")]
    public async Task AStoppedContainerIsDiagnosedAndLeftAlone(string state)
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeContainerHost.Create(new FakeContainerHostOptions { State = state });

        var report = await RunAsync(host, cancellationToken: ct).ConfigureAwait(true);

        var diagnosis = Only(report);
        diagnosis.Kind.ShouldBe(RemoteDiagnosisKind.ContainerNotRunning);
        diagnosis.FixText.ShouldContain("podman start devbox", Case.Sensitive);
        diagnosis.Detail.ShouldNotBeNull().ShouldContain(state, Case.Sensitive);
        host.StateOf("devbox").ShouldBe(state);
        host.PodmanCalls().ShouldNotContain(call => call[0] == "start" || call[0] == "exec");
    }

    /// <summary>A user that does not exist in the container is diagnosed with the user name.</summary>
    [Fact]
    public async Task AMissingUserIsDiagnosed()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeContainerHost.Create();

        var report = await RunAsync(host, host.Profile with { RemoteUser = "nobody" }, ct).ConfigureAwait(true);

        var diagnosis = Only(report);
        diagnosis.Kind.ShouldBe(RemoteDiagnosisKind.ContainerUserMissing);
        diagnosis.Summary.ShouldContain("nobody", Case.Sensitive);
    }

    /// <summary>A user that exists is passed to podman and the test passes.</summary>
    [Fact]
    public async Task AKnownUserPasses()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeContainerHost.Create();

        var report = await RunAsync(host, host.Profile with { RemoteUser = "dev" }, ct).ConfigureAwait(true);

        report.Succeeded.ShouldBeTrue();
        host.PodmanCalls()[^1].Take(6).ToArray().ShouldBe(["exec", "-i", "-u", "dev", "--", "devbox"]);
    }

    /// <summary>A workspace folder that does not exist in the container is diagnosed with its path.</summary>
    [Fact]
    public async Task AMissingWorkspaceFolderIsDiagnosed()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeContainerHost.Create();
        var missing = Path.Combine(host.Root, "nowhere");

        var report = await RunAsync(host, host.Profile with { WorkspaceFolder = missing }, ct).ConfigureAwait(true);

        var diagnosis = Only(report);
        diagnosis.Kind.ShouldBe(RemoteDiagnosisKind.DirectoryMissing);
        diagnosis.Summary.ShouldContain(missing, Case.Sensitive);
        diagnosis.Summary.ShouldContain("inside the container", Case.Sensitive);
    }

    /// <summary>An omp that is not installed in the container is diagnosed with the container wording.</summary>
    [Fact]
    public async Task AMissingOmpIsDiagnosed()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeContainerHost.Create(new FakeContainerHostOptions { InstallOmp = false });

        var report = await RunAsync(host, cancellationToken: ct).ConfigureAwait(true);

        var diagnosis = Only(report);
        diagnosis.Kind.ShouldBe(RemoteDiagnosisKind.OmpMissing);
        diagnosis.Summary.ShouldBe("omp was not found inside the container.", Case.Sensitive);
        diagnosis.FixText.ShouldContain("dev container definition", Case.Sensitive);
    }

    /// <summary>An omp older than the minimum is diagnosed, and the minimum can be changed.</summary>
    [Fact]
    public async Task AnOldOmpIsDiagnosed()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeContainerHost.Create(new FakeContainerHostOptions { OmpVersion = "17.9.0" });

        var strict = await RunAsync(host, cancellationToken: ct).ConfigureAwait(true);
        var lenient = await new ContainerPreflight(host.ClientOptions, new RemotePreflightOptions { MinimumOmpVersion = new Version(17, 0, 0) }).RunAsync(host.Profile, ct).ConfigureAwait(true);

        Only(strict).Kind.ShouldBe(RemoteDiagnosisKind.OmpTooOld);
        strict.OmpVersion.ShouldBe("17.9.0");
        lenient.Succeeded.ShouldBeTrue();
    }

    /// <summary>A platform other than the verified one is only a warning: the container can still run a session.</summary>
    [Fact]
    public async Task AnUnverifiedPlatformIsAWarning()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeContainerHost.Create(new FakeContainerHostOptions { UnameOutput = "Linux aarch64" });

        var report = await RunAsync(host, cancellationToken: ct).ConfigureAwait(true);

        report.Succeeded.ShouldBeTrue();
        var diagnosis = report.Diagnoses.ShouldHaveSingleItem();
        diagnosis.Kind.ShouldBe(RemoteDiagnosisKind.UnsupportedPlatform);
        diagnosis.Severity.ShouldBe(DiagnosisSeverity.Warning);
    }

    /// <summary>The omp path of the profile is the one checked, not the default.</summary>
    [Fact]
    public async Task TheProfileOmpPathIsChecked()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeContainerHost.Create();

        var report = await RunAsync(host, host.Profile with { OmpPath = "/nonexistent/omp" }, ct).ConfigureAwait(true);

        Only(report).Kind.ShouldBe(RemoteDiagnosisKind.OmpMissing);
    }

    /// <summary>An invalid profile is refused before podman runs.</summary>
    [Fact]
    public async Task AnInvalidProfileIsRefusedBeforeAnyCall()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeContainerHost.Create();

        await Should.ThrowAsync<ArgumentException>(() => RunAsync(host, host.Profile with { Container = "--rm" }, ct)).ConfigureAwait(true);

        host.PodmanCalls().ShouldBeEmpty();
    }

    /// <summary>Starting a stopped container runs <c>podman start</c> once and the container is then running.</summary>
    [Fact]
    public async Task StartingAStoppedContainerRunsPodmanStart()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeContainerHost.Create(new FakeContainerHostOptions { State = "exited" });

        var result = await new ContainerPreflight(host.ClientOptions).StartAsync(host.Profile, ct).ConfigureAwait(true);

        result.Started.ShouldBeTrue();
        result.Diagnosis.ShouldBeNull();
        host.StateOf("devbox").ShouldBe("running");
        host.PodmanCalls().ShouldHaveSingleItem().ToArray().ShouldBe(["start", "--", "devbox"]);
    }

    /// <summary>A start that fails keeps podman's words in the diagnosis; a container that does not exist is diagnosed as missing.</summary>
    [Fact]
    public async Task AFailedStartIsDiagnosed()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeContainerHost.Create(new FakeContainerHostOptions { State = "exited", StartFails = true });

        using var healthy = FakeContainerHost.Create();

        var failed = await new ContainerPreflight(host.ClientOptions).StartAsync(host.Profile, ct).ConfigureAwait(true);
        var missing = await new ContainerPreflight(healthy.ClientOptions).StartAsync(healthy.Profile with { Container = "ghost" }, ct).ConfigureAwait(true);

        failed.Started.ShouldBeFalse();
        failed.Diagnosis.ShouldNotBeNull().Detail.ShouldNotBeNull().ShouldContain("crun", Case.Sensitive);
        missing.Started.ShouldBeFalse();
        missing.Diagnosis.ShouldNotBeNull().Kind.ShouldBe(RemoteDiagnosisKind.ContainerMissing);
    }
}
