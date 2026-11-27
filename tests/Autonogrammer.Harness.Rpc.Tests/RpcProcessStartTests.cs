using System.ComponentModel;
using System.Runtime.Versioning;
using System.Text;
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

    /// <summary>
    /// A supplied environment dictionary is the child's complete environment: the parent-only variable is
    /// gone (an empty entry also deliberately clears it) and only the supplied entry reaches the child.
    /// The script answers with shell builtins only, so it works with no PATH in its environment.
    /// </summary>
    [Fact]
    [SupportedOSPlatform("linux")]
    public async Task StartWithEnvironment_RestrictsChildToSuppliedDictionary()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("The fake omp fixture is a POSIX shell script.");
        }

        var ct = TestContext.Current.CancellationToken;
        string directory = Path.Combine(Path.GetTempPath(), "omp-rpc-env-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string output = Path.Combine(directory, "env.txt");
        string ompPath = WriteScript(
            directory,
            $"printf '%s|%s' \"$AUTONOGRAMMER_RPC_ONLY\" \"$AUTONOGRAMMER_RPC_PARENT_ONLY\" > '{output}'\n");
        Environment.SetEnvironmentVariable("AUTONOGRAMMER_RPC_PARENT_ONLY", "parent-secret");
        try
        {
            var environment = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["AUTONOGRAMMER_RPC_ONLY"] = "supplied",
                ["AUTONOGRAMMER_RPC_PARENT_ONLY"] = string.Empty,
            };
            await using OmpRpcProcess process = OmpRpcProcess.Start(ompPath, directory, environment);
            await process.WaitForExitAsync(ct).ConfigureAwait(true);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AUTONOGRAMMER_RPC_PARENT_ONLY", null);
        }

        (await File.ReadAllTextAsync(output, ct).ConfigureAwait(true)).ShouldBe("supplied|");
        Directory.Delete(directory, recursive: true);
    }

    /// <summary>
    /// An engine that exits before <c>ready</c> (the ledger's no-default-model startup) surfaces its stderr
    /// tail in the start failure instead of a bare disconnect/timeout.
    /// </summary>
    [Fact]
    [SupportedOSPlatform("linux")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Usage",
        "CA2000",
        Justification = "The OmpRpcSession takes ownership of the process transport and disposes it.")]
    public async Task PreReadyExit_SurfacesStderrTailInStartFailure()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("The fake omp fixture is a POSIX shell script.");
        }

        var ct = TestContext.Current.CancellationToken;
        string directory = Path.Combine(Path.GetTempPath(), "omp-rpc-exit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string ompPath = WriteScript(
            directory,
            "echo 'No default model selected. Use /model or --model.' >&2\n/bin/sleep 1\n");
        var session = new OmpRpcSession(
            OmpRpcProcess.Start(ompPath, directory), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(10));
        try
        {
            HarnessException failure = await Should.ThrowAsync<HarnessException>(
                () => session.CreateAsync(ct)).ConfigureAwait(true);

            failure.Message.ShouldContain("No default model selected.");
        }
        finally
        {
            await session.DisposeAsync().ConfigureAwait(true);
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Writes an executable fake omp shell script with <paramref name="body"/> and returns its path.</summary>
    [SupportedOSPlatform("linux")]
    private static string WriteScript(string directory, string body)
    {
        string ompPath = Path.Combine(directory, "omp");
        File.WriteAllText(ompPath, "#!/bin/sh\n" + body, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.SetUnixFileMode(ompPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return ompPath;
    }
}
