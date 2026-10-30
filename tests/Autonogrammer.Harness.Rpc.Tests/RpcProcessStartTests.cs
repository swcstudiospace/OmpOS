using System.ComponentModel;
using Autonogrammer.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Rpc.Tests;

/// <summary>Unstartable engines surface a typed process-start failure from both start paths.</summary>
public sealed class RpcProcessStartTests
{
    /// <summary>Starting the engine from a missing binary maps the OS failure to a typed exception.</summary>
    [Fact]
    public void StartWithMissingBinaryThrowsProcessStart()
    {
        ProcessStartHarnessException failure = Should.Throw<ProcessStartHarnessException>(
            () => OmpRpcProcess.Start("omp-no-such-binary-xyz", Path.GetTempPath()));

        failure.Message.ShouldContain("omp-no-such-binary-xyz");
        failure.InnerException.ShouldBeOfType<Win32Exception>();
    }

    /// <summary>Probing a missing binary maps the OS failure to a typed exception.</summary>
    [Fact]
    public async Task ProbeWithMissingBinaryThrowsProcessStart()
    {
        var ct = TestContext.Current.CancellationToken;

        ProcessStartHarnessException failure = await Should.ThrowAsync<ProcessStartHarnessException>(
            () => OmpVersionProbe.ProbeAsync("omp-no-such-binary-xyz", ct)).ConfigureAwait(true);

        failure.Message.ShouldContain("omp-no-such-binary-xyz");
        failure.InnerException.ShouldBeOfType<Win32Exception>();
    }
}
