using OmpOS.Contracts.Environments;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Remote.Tests;

/// <summary>The connection test: every way a remote environment can be wrong becomes a typed diagnosis with fix text.</summary>
public sealed class RemotePreflightTests
{
    private static Task<RemotePreflightReport> RunAsync(FakeRemoteHost host, SshProfile? profile = null, CancellationToken cancellationToken = default) =>
        new RemotePreflight(host.ClientOptions).RunAsync(profile ?? host.Profile, cancellationToken);

    /// <summary>A healthy machine passes and the report tells what was found.</summary>
    [Fact]
    public async Task AHealthyMachinePasses()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeRemoteHost.Create();

        var report = await RunAsync(host, cancellationToken: ct).ConfigureAwait(true);

        report.Succeeded.ShouldBeTrue();
        report.Diagnoses.ShouldBeEmpty();
        report.HomeDirectory.ShouldBe(host.HomeDirectory);
        report.Platform.ShouldBe("Linux x86_64");
        report.OmpVersion.ShouldBe("18.12.6");
        report.OmpPath.ShouldBe(Path.Combine(host.HomeDirectory, ".bun", "bin", "omp"));
    }

    /// <summary>The banner of a login shell does not disturb the answer.</summary>
    [Fact]
    public async Task ABannerDoesNotDisturbTheAnswer()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeRemoteHost.Create(new FakeRemoteHostOptions { Banner = true });

        var report = await RunAsync(host, cancellationToken: ct).ConfigureAwait(true);

        report.Succeeded.ShouldBeTrue();
        report.OmpVersion.ShouldBe("18.12.6");
    }

    /// <summary>The probe is one non-interactive ssh call with a double dash, BatchMode and a quoted script.</summary>
    [Fact]
    public async Task TheProbeIsOneNonInteractiveCall()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeRemoteHost.Create();

        _ = await RunAsync(host, cancellationToken: ct).ConfigureAwait(true);

        var calls = host.SshCalls();
        calls.Count.ShouldBe(2);
        calls[0].ToArray().ShouldBe(["-V"]);
        calls[1].ShouldContain("BatchMode=yes");
        calls[1].ShouldContain("-T");
        calls[1][^1].ShouldStartWith("exec sh -c '", Case.Sensitive);
        calls[1][^2].ShouldBe("fake-host");
        calls[1][^3].ShouldBe("--");
    }

    /// <summary>A machine without omp reports it, with the PATH explanation.</summary>
    [Fact]
    public async Task OmpMissingIsReported()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeRemoteHost.Create(new FakeRemoteHostOptions { InstallOmp = false });

        var report = await RunAsync(host, cancellationToken: ct).ConfigureAwait(true);

        report.Succeeded.ShouldBeFalse();
        var diagnosis = report.Diagnoses.ShouldHaveSingleItem();
        diagnosis.Kind.ShouldBe(RemoteDiagnosisKind.OmpMissing);
        diagnosis.Severity.ShouldBe(DiagnosisSeverity.Error);
        diagnosis.FixText.ShouldContain("Remote omp path", Case.Sensitive);
        diagnosis.FixText.ShouldContain("~/.bun/bin", Case.Sensitive);
        report.OmpPath.ShouldBeNull();
    }

    /// <summary>An omp older than the minimum is reported with the minimum version.</summary>
    [Fact]
    public async Task AnOldOmpIsReported()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeRemoteHost.Create(new FakeRemoteHostOptions { OmpVersion = "18.3.1" });

        var report = await RunAsync(host, cancellationToken: ct).ConfigureAwait(true);

        report.Succeeded.ShouldBeFalse();
        var diagnosis = report.Diagnoses.ShouldHaveSingleItem();
        diagnosis.Kind.ShouldBe(RemoteDiagnosisKind.OmpTooOld);
        diagnosis.Summary.ShouldContain("18.12.0", Case.Sensitive);
        report.OmpVersion.ShouldBe("18.3.1");
    }

    /// <summary>The minimum version is configurable.</summary>
    [Fact]
    public async Task TheMinimumVersionIsConfigurable()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeRemoteHost.Create(new FakeRemoteHostOptions { OmpVersion = "18.3.1" });
        var preflight = new RemotePreflight(host.ClientOptions, new RemotePreflightOptions { MinimumOmpVersion = new Version(18, 0, 0) });

        var report = await preflight.RunAsync(host.Profile, ct).ConfigureAwait(true);

        report.Succeeded.ShouldBeTrue();
    }

    /// <summary>A workspace directory that does not exist is reported with its path.</summary>
    [Fact]
    public async Task AMissingDirectoryIsReported()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeRemoteHost.Create();
        var profile = host.Profile with { RemoteWorkspaceRoot = "/nonexistent/ompos test dir" };

        var report = await RunAsync(host, profile, ct).ConfigureAwait(true);

        report.Succeeded.ShouldBeFalse();
        var diagnosis = report.Diagnoses.ShouldHaveSingleItem();
        diagnosis.Kind.ShouldBe(RemoteDiagnosisKind.DirectoryMissing);
        diagnosis.Summary.ShouldContain("/nonexistent/ompos test dir", Case.Sensitive);
    }

    /// <summary>A directory the remote user cannot write is reported. A privileged user can write anywhere, so the test needs an ordinary one.</summary>
    [Fact]
    public async Task AReadOnlyDirectoryIsReported()
    {
        var ct = TestContext.Current.CancellationToken;
        if (Environment.IsPrivilegedProcess)
        {
            Assert.Skip("A privileged user can write to a read-only directory.");
        }

        using var host = FakeRemoteHost.Create();
        File.SetUnixFileMode(host.WorkspaceDirectory, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        try
        {
            var report = await RunAsync(host, cancellationToken: ct).ConfigureAwait(true);

            report.Diagnoses.ShouldHaveSingleItem().Kind.ShouldBe(RemoteDiagnosisKind.DirectoryNotWritable);
        }
        finally
        {
            File.SetUnixFileMode(host.WorkspaceDirectory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    /// <summary>A platform other than Linux x86_64 is a warning: the connection test still succeeds.</summary>
    [Fact]
    public async Task AnUnverifiedPlatformIsAWarning()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeRemoteHost.Create(new FakeRemoteHostOptions { UnameOutput = "Darwin arm64" });

        var report = await RunAsync(host, cancellationToken: ct).ConfigureAwait(true);

        report.Succeeded.ShouldBeTrue();
        var diagnosis = report.Diagnoses.ShouldHaveSingleItem();
        diagnosis.Kind.ShouldBe(RemoteDiagnosisKind.UnsupportedPlatform);
        diagnosis.Severity.ShouldBe(DiagnosisSeverity.Warning);
        diagnosis.Summary.ShouldContain("Darwin arm64", Case.Sensitive);
    }

    /// <summary>A login shell that does not understand POSIX syntax is named as the cause.</summary>
    [Fact]
    public async Task ANonPosixShellIsReported()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeRemoteHost.Create(new FakeRemoteHostOptions { Mode = "nonposix" });

        var report = await RunAsync(host, cancellationToken: ct).ConfigureAwait(true);

        report.Succeeded.ShouldBeFalse();
        var diagnosis = report.Diagnoses.ShouldHaveSingleItem();
        diagnosis.Kind.ShouldBe(RemoteDiagnosisKind.ShellNotPosix);
        diagnosis.Detail.ShouldNotBeNull().ShouldContain("fish:", Case.Sensitive);
    }

    /// <summary>The way ssh itself failed maps to a diagnosis, and every diagnosis has a summary and fix text.</summary>
    [Theory]
    [InlineData("refused", RemoteDiagnosisKind.HostUnreachable)]
    [InlineData("resolve", RemoteDiagnosisKind.HostUnreachable)]
    [InlineData("hostkey", RemoteDiagnosisKind.HostKeyUnknown)]
    [InlineData("hostkeychanged", RemoteDiagnosisKind.HostKeyChanged)]
    [InlineData("authfail", RemoteDiagnosisKind.AuthFailed)]
    public async Task AnSshFailureIsDiagnosed(string mode, RemoteDiagnosisKind expected)
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeRemoteHost.Create(new FakeRemoteHostOptions { Mode = mode });

        var report = await RunAsync(host, cancellationToken: ct).ConfigureAwait(true);

        report.Succeeded.ShouldBeFalse();
        var diagnosis = report.Diagnoses.ShouldHaveSingleItem();
        diagnosis.Kind.ShouldBe(expected);
        diagnosis.Summary.ShouldNotBeNullOrWhiteSpace();
        diagnosis.FixText.ShouldNotBeNullOrWhiteSpace();
        diagnosis.Detail.ShouldNotBeNullOrWhiteSpace();
    }

    /// <summary>A missing ssh client is the first thing reported, before any host is contacted.</summary>
    [Fact]
    public async Task AMissingSshClientIsReported()
    {
        var ct = TestContext.Current.CancellationToken;
        var preflight = new RemotePreflight(new SshClientOptions { SshPath = "/nonexistent/ompos-test-ssh" });

        var report = await preflight.RunAsync(new SshProfile { Alias = "any", RemoteWorkspaceRoot = "/w" }, ct).ConfigureAwait(true);

        report.Succeeded.ShouldBeFalse();
        report.Diagnoses.ShouldHaveSingleItem().Kind.ShouldBe(RemoteDiagnosisKind.SshMissing);
    }

    /// <summary>A connection that never answers is reported as unreachable once the time limit passes, without any real waiting.</summary>
    [Fact(Timeout = 60000)]
    public async Task ASilentHostIsReportedUnreachableAfterTheTimeLimit()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeRemoteHost.Create(new FakeRemoteHostOptions { Mode = "hang" });
        var clock = new ManualTimeProvider();
        var options = host.ClientOptions with { CommandTimeout = TimeSpan.FromSeconds(20) };
        var task = new RemotePreflight(options, clock: clock).RunAsync(host.Profile, ct);

        while (!task.IsCompleted)
        {
            ct.ThrowIfCancellationRequested();
            if (host.SshCalls().Count >= 2 && clock.PendingTimers > 0)
            {
                clock.Advance(TimeSpan.FromSeconds(20));
            }
            else
            {
                await Task.Yield();
            }
        }

        var report = await task.ConfigureAwait(true);

        report.Succeeded.ShouldBeFalse();
        var diagnosis = report.Diagnoses.ShouldHaveSingleItem();
        diagnosis.Kind.ShouldBe(RemoteDiagnosisKind.HostUnreachable);
        diagnosis.Detail.ShouldNotBeNull().ShouldContain("20 seconds", Case.Sensitive);
    }

    /// <summary>An invalid profile is a programming error, not a report.</summary>
    [Fact]
    public async Task AnInvalidProfileIsRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeRemoteHost.Create();

        await Should.ThrowAsync<ArgumentException>(() => RunAsync(host, new SshProfile { Alias = "-oBad", RemoteWorkspaceRoot = "/w" }, ct)).ConfigureAwait(true);
    }
}
