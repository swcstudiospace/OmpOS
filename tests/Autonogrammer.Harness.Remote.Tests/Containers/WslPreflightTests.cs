using Autonogrammer.Contracts.Environments;
using Autonogrammer.Harness.Remote.Containers;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Remote.Tests;

/// <summary>The connection test of a WSL distribution against a fake wsl: every way a WSL environment can be wrong becomes a typed diagnosis with fix text.</summary>
public sealed class WslPreflightTests
{
    private static Task<RemotePreflightReport> RunAsync(FakeWslHost host, WslProfile? profile = null, CancellationToken cancellationToken = default) =>
        new WslPreflight(host.ClientOptions).RunAsync(profile ?? host.Profile, cancellationToken);

    private static RemoteDiagnosis Only(RemotePreflightReport report)
    {
        report.Succeeded.ShouldBeFalse();
        return report.Diagnoses.ShouldHaveSingleItem();
    }

    /// <summary>A healthy distribution passes; the distribution list that wsl writes as UTF-16 text is read correctly.</summary>
    [Fact]
    public async Task AHealthyDistributionPasses()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeWslHost.Create();

        var report = await RunAsync(host, cancellationToken: ct).ConfigureAwait(true);

        report.Succeeded.ShouldBeTrue();
        report.Diagnoses.ShouldBeEmpty();
        report.HomeDirectory.ShouldBe(host.HomeDirectory);
        report.OmpVersion.ShouldBe("18.12.6");
        var calls = host.WslCalls();
        calls.Count.ShouldBe(2);
        calls[0].ToArray().ShouldBe(["--list", "--quiet"]);
        calls[1].Take(5).ToArray().ShouldBe(["-d", FakeWslHost.DistroName, "--exec", "sh", "-c"]);
    }

    /// <summary>A missing wsl.exe is one diagnosis that says WSL must be installed.</summary>
    [Fact]
    public async Task AMissingWslIsDiagnosed()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeWslHost.Create();
        var preflight = new WslPreflight(new WslClientOptions { WslPath = "/nonexistent/autonogrammer-test-wsl.exe" });

        var report = await preflight.RunAsync(host.Profile, ct).ConfigureAwait(true);

        var diagnosis = Only(report);
        diagnosis.Kind.ShouldBe(RemoteDiagnosisKind.WslMissing);
        diagnosis.FixText.ShouldContain("wsl --install", Case.Sensitive);
    }

    /// <summary>A distribution that is not installed is diagnosed with its name, found in the list without running anything in it.</summary>
    [Fact]
    public async Task AMissingDistributionIsDiagnosed()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeWslHost.Create();

        var report = await RunAsync(host, host.Profile with { Distro = "Debian" }, ct).ConfigureAwait(true);

        var diagnosis = Only(report);
        diagnosis.Kind.ShouldBe(RemoteDiagnosisKind.WslDistroMissing);
        diagnosis.Summary.ShouldContain("Debian", Case.Sensitive);
        host.WslCalls().ShouldHaveSingleItem();
    }

    /// <summary>The distribution name is matched against the list without regard to case, as wsl.exe does: a name in another case is not reported as missing by the list check.</summary>
    [Fact]
    public async Task TheDistributionNameIgnoresCase()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeWslHost.Create();

        _ = await RunAsync(host, host.Profile with { Distro = FakeWslHost.DistroName.ToUpperInvariant() }, ct).ConfigureAwait(true);

        // The list check passed, so the probe was attempted: a second call exists. (The fake keeps distributions in case-sensitive directories, so the probe itself fails here.)
        host.WslCalls().Count.ShouldBe(2);
    }

    /// <summary>A user that does not exist in the distribution is diagnosed.</summary>
    [Fact]
    public async Task AMissingUserIsDiagnosed()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeWslHost.Create();

        var report = await RunAsync(host, host.Profile with { User = "nobody" }, ct).ConfigureAwait(true);

        var diagnosis = Only(report);
        diagnosis.Kind.ShouldBe(RemoteDiagnosisKind.WslUserMissing);
        diagnosis.Summary.ShouldContain("nobody", Case.Sensitive);
    }

    /// <summary>A workspace path that does not exist in the distribution is diagnosed with the distribution wording.</summary>
    [Fact]
    public async Task AMissingWorkspaceIsDiagnosed()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeWslHost.Create();

        var report = await RunAsync(host, host.Profile with { WorkspacePath = Path.Combine(host.Root, "nowhere") }, ct).ConfigureAwait(true);

        var diagnosis = Only(report);
        diagnosis.Kind.ShouldBe(RemoteDiagnosisKind.DirectoryMissing);
        diagnosis.Summary.ShouldContain("in the WSL distribution", Case.Sensitive);
    }

    /// <summary>An omp that is not installed in the distribution is diagnosed.</summary>
    [Fact]
    public async Task AMissingOmpIsDiagnosed()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeWslHost.Create(new FakeWslHostOptions { InstallOmp = false });

        var report = await RunAsync(host, cancellationToken: ct).ConfigureAwait(true);

        Only(report).Summary.ShouldBe("omp was not found in the WSL distribution.");
    }

    /// <summary>The host adapter reports WSL as unavailable on a system without WSL, without running anything.</summary>
    [Fact]
    public async Task TheHostAdapterSaysWslNeedsWindows()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeWslHost.Create();
        var adapter = new ContainerEnvironmentsHost(wslOptions: host.ClientOptions);

        var report = await adapter.TestWslAsync(host.Profile, ct).ConfigureAwait(true);

        if (OperatingSystem.IsWindows())
        {
            adapter.SupportsWsl.ShouldBeTrue();
            return;
        }

        adapter.SupportsWsl.ShouldBeFalse();
        Only(report).Kind.ShouldBe(RemoteDiagnosisKind.WslMissing);
        host.WslCalls().ShouldBeEmpty();
    }
}
