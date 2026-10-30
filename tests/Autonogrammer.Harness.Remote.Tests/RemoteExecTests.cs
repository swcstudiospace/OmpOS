using Autonogrammer.Contracts.Environments;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Remote.Tests;

/// <summary>The ssh exec layer: stdin, the output cap, exit codes and stderr, binary output, the time limit, cancellation and the refusal of a bad script or profile.</summary>
public sealed class RemoteExecTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Bytes written to stdin reach the script, and stdout comes back as bytes, cut at the cap with the cut reported.</summary>
    [Fact]
    public async Task StdinReachesTheScriptAndOutputIsCapped()
    {
        using var host = FakeRemoteHost.Create();
        using var exec = new RemoteExec(host.Profile, host.ClientOptions);
        var input = new byte[100];
        Array.Fill(input, (byte)'x');

        var capped = await exec.RunAsync("cat", input, 10, Token).ConfigureAwait(true);
        var whole = await exec.RunAsync("cat", input, 1000, Token).ConfigureAwait(true);

        capped.Output.Length.ShouldBe(10);
        capped.OutputTruncated.ShouldBeTrue();
        capped.Succeeded.ShouldBeTrue();
        whole.Output.Length.ShouldBe(100);
        whole.OutputTruncated.ShouldBeFalse();
    }

    /// <summary>The exit code of the script and its stderr are reported; a failing script is a result, not an exception.</summary>
    [Fact]
    public async Task ExitCodeAndStandardErrorAreReported()
    {
        using var host = FakeRemoteHost.Create();
        using var exec = new RemoteExec(host.Profile, host.ClientOptions);

        var result = await exec.RunAsync("printf oops >&2; exit 7", ReadOnlyMemory<byte>.Empty, 100, Token).ConfigureAwait(true);

        result.ExitCode.ShouldBe(7);
        result.Error.ShouldBe("oops");
        result.Succeeded.ShouldBeFalse();
        result.IsConnectionFailure.ShouldBeFalse();
    }

    /// <summary>Stdout is returned as raw bytes: a NUL and an invalid UTF-8 byte survive.</summary>
    [Fact]
    public async Task BinaryOutputSurvives()
    {
        using var host = FakeRemoteHost.Create();
        using var exec = new RemoteExec(host.Profile, host.ClientOptions);

        var result = await exec.RunAsync("printf '\\377\\000\\001'", ReadOnlyMemory<byte>.Empty, 100, Token).ConfigureAwait(true);

        result.Output.ToArray().ShouldBe([0xFF, 0x00, 0x01]);
    }

    /// <summary>The home directory is read from the remote machine.</summary>
    [Fact]
    public async Task TheHomeDirectoryIsTheRemoteOne()
    {
        using var host = FakeRemoteHost.Create();
        using var exec = new RemoteExec(host.Profile, host.ClientOptions);

        (await exec.ResolveHomeDirectoryAsync(Token).ConfigureAwait(true)).ShouldBe(host.HomeDirectory);
    }

    /// <summary>A refused connection is a connection failure with the ssh diagnosis, and a missing ssh client is a start failure.</summary>
    [Fact]
    public async Task ConnectionAndStartFailuresAreResults()
    {
        using var refused = FakeRemoteHost.Create(new FakeRemoteHostOptions { Mode = "refused" });
        using var refusedExec = new RemoteExec(refused.Profile, refused.ClientOptions);
        using var missingExec = new RemoteExec(refused.Profile, new SshClientOptions { SshPath = Path.Combine(refused.Root, "no-such-ssh") });

        var refusedResult = await refusedExec.RunAsync("true", ReadOnlyMemory<byte>.Empty, 10, Token).ConfigureAwait(true);
        var missingResult = await missingExec.RunAsync("true", ReadOnlyMemory<byte>.Empty, 10, Token).ConfigureAwait(true);

        refusedResult.IsConnectionFailure.ShouldBeTrue();
        refusedResult.DescribeConnectionFailure().ShouldContain("cannot be reached", Case.Sensitive);
        missingResult.StartFailure.ShouldNotBeNull();
        missingResult.IsConnectionFailure.ShouldBeTrue();
        missingResult.DescribeConnectionFailure().ShouldContain("could not be started", Case.Sensitive);
    }

    /// <summary>A script that runs past the time limit is killed and reported as timed out; the clock is the injected one, so no real time passes.</summary>
    [Fact]
    public async Task ARunPastTheTimeLimitIsKilled()
    {
        using var host = FakeRemoteHost.Create(new FakeRemoteHostOptions { Mode = "hang" });
        var clock = new ManualTimeProvider();
        using var exec = new RemoteExec(host.Profile, host.ClientOptions with { CommandTimeout = TimeSpan.FromSeconds(5) }, clock);

        var run = exec.RunAsync("true", ReadOnlyMemory<byte>.Empty, 10, Token);
        await Task.Run(() => SpinWait.SpinUntil(() => clock.PendingTimers > 0, TimeSpan.FromSeconds(30)), Token).ConfigureAwait(true);
        clock.Advance(TimeSpan.FromSeconds(10));
        var result = await run.ConfigureAwait(true);

        result.TimedOut.ShouldBeTrue();
        result.ExitCode.ShouldBe(-1);
        result.IsConnectionFailure.ShouldBeTrue();
        result.DescribeConnectionFailure().ShouldContain("did not answer", Case.Sensitive);
    }

    /// <summary>Cancelling stops the run and surfaces as a cancellation, not as a result.</summary>
    [Fact]
    public async Task CancellationStopsTheRun()
    {
        using var host = FakeRemoteHost.Create(new FakeRemoteHostOptions { Mode = "hang" });
        using var exec = new RemoteExec(host.Profile, host.ClientOptions);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);

        var run = exec.RunAsync("true", ReadOnlyMemory<byte>.Empty, 10, cancellation.Token);
        await cancellation.CancelAsync().ConfigureAwait(true);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run.WaitAsync(TestContext.Current.CancellationToken)).ConfigureAwait(true);
    }

    /// <summary>A script that spans several lines, and a profile that fails validation, are refused before ssh starts.</summary>
    [Fact]
    public async Task ABadScriptOrProfileIsRefused()
    {
        using var host = FakeRemoteHost.Create();
        using var exec = new RemoteExec(host.Profile, host.ClientOptions);

        await Should.ThrowAsync<ArgumentException>(() => exec.RunAsync("true\nrm -rf x", ReadOnlyMemory<byte>.Empty, 10, Token)).ConfigureAwait(true);
        Should.Throw<ArgumentException>(() => new RemoteExec(new SshProfile { Alias = "-oProxyCommand=evil", RemoteWorkspaceRoot = "/w" }));

        host.SshCalls().ShouldBeEmpty();
    }
}
