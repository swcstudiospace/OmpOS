using System.Runtime.Versioning;
using System.Text;
using OmpOS.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Rpc.Tests;

/// <summary>
/// Verifies the omp version pin refuses an out-of-pin release through the public start paths, before the engine
/// process is spawned.
/// </summary>
/// <remarks>
/// The fake omp is a POSIX shell script. It reports an out-of-pin version for <c>--version</c> and logs every
/// invocation. An engine spawn would add a line to that log, so the log shows whether the start reached spawning.
/// </remarks>
public sealed class OmpPinMismatchTests : IDisposable
{
    private readonly string _directory;
    private readonly string _ompPath;
    private readonly string _invocationLog;

    /// <summary>Creates a fresh fixture directory for the fake omp and its invocation log.</summary>
    public OmpPinMismatchTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "omp-pin-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _ompPath = Path.Combine(_directory, "omp");
        _invocationLog = Path.Combine(_directory, "invocations.log");
    }

    /// <summary>Removes the fixture directory.</summary>
    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    /// <summary>Each out-of-pin release, on either side of the 18.12 line, is refused before any engine spawn.</summary>
    /// <param name="version">The out-of-pin release the fake omp reports.</param>
    [Theory]
    [InlineData("19.0.0")]
    [InlineData("18.13.0")]
    [InlineData("18.11.9")]
    [InlineData("17.12.4")]
    [SupportedOSPlatform("linux")]
    public async Task CreateSessionAsync_OutOfPinVersion_ThrowsBeforeEngineSpawn(string version)
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("The fake omp fixture is a POSIX shell script.");
        }

        // Arrange
        WriteFakeOmp(version);
        var client = new OmpRpcClient(new OmpRpcClientOptions { OmpPath = _ompPath, Cwd = _directory });
        var ct = TestContext.Current.CancellationToken;

        // Act & Assert
        ProcessStartHarnessException failure = await Should.ThrowAsync<ProcessStartHarnessException>(
            () => client.CreateSessionAsync(ct)).ConfigureAwait(true);

        failure.Message.ShouldContain($"omp {version} is outside the pinned 18.12.x range.");
        string invocations = await File.ReadAllTextAsync(_invocationLog, ct).ConfigureAwait(true);
        invocations.ShouldBe("--version\n");
    }

    /// <summary>The resume path applies the same pin check and refuses an out-of-pin release before any engine spawn.</summary>
    [Fact]
    [SupportedOSPlatform("linux")]
    public async Task ResumeSessionAsync_OutOfPinVersion_ThrowsBeforeEngineSpawn()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("The fake omp fixture is a POSIX shell script.");
        }

        // Arrange
        WriteFakeOmp("19.0.0");
        var client = new OmpRpcClient(new OmpRpcClientOptions { OmpPath = _ompPath, Cwd = _directory });
        var ct = TestContext.Current.CancellationToken;

        // Act & Assert
        ProcessStartHarnessException failure = await Should.ThrowAsync<ProcessStartHarnessException>(
            () => client.ResumeSessionAsync(Path.Combine(_directory, "session.jsonl"), ct)).ConfigureAwait(true);

        failure.Message.ShouldContain("omp 19.0.0 is outside the pinned 18.12.x range.");
        string invocations = await File.ReadAllTextAsync(_invocationLog, ct).ConfigureAwait(true);
        invocations.ShouldBe("--version\n");
    }

    /// <summary>Writes an executable fake omp that reports <paramref name="version"/> and logs each invocation.</summary>
    /// <param name="version">The release the fake reports for <c>--version</c>.</param>
    [SupportedOSPlatform("linux")]
    private void WriteFakeOmp(string version)
    {
        string script = "#!/bin/sh\n"
            + $"echo \"$*\" >> '{_invocationLog}'\n"
            + $"echo 'omp/{version}'\n";
        File.WriteAllText(_ompPath, script, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.SetUnixFileMode(_ompPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }
}
