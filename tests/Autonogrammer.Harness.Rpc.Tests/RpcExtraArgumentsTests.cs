using System.Runtime.Versioning;
using System.Text;
using Autonogrammer.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Rpc.Tests;

/// <summary>
/// Process-argv tests for the RPC extra-arguments channel (Phase 82 Task 3): <c>--mode rpc --no-ui --cwd</c> followed
/// by the supplied arguments verbatim, one argv element each. No test spawns a real omp: the engine is a POSIX shell
/// script that records its own argv or reports an out-of-pin version for <c>--version</c>.
/// </summary>
public sealed class RpcExtraArgumentsTests
{
    private const string LogName = "argv.txt";

    /// <summary>Representative plan arguments (plugin <c>-e</c> pairs, <c>--config</c>, <c>--add-dir</c>, <c>--skills</c>, <c>--approval-mode</c>) reach the child byte-for-byte, one element each and in order.</summary>
    [Fact]
    [SupportedOSPlatform("linux")]
    public async Task StartWithRepresentativeArguments_EmitsExactArgv()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("The fake omp fixture is a POSIX shell script.");
        }

        var ct = TestContext.Current.CancellationToken;
        string directory = Path.Combine(Path.GetTempPath(), "omp-rpc-argv-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string output = Path.Combine(directory, LogName);
            string ompPath = WriteScript(directory, $"for a in \"$@\"; do printf '%s\\n' \"$a\"; done > '{output}'\n");
            var extra = new[]
            {
                "--no-extensions", "-e", "/plugins/ext", "--config", "/cfg/overlay.yml",
                "--add-dir", "/other root", "--skills", "fixture-*", "--approval-mode", "write",
            };

            await using OmpRpcProcess process = OmpRpcProcess.Start(ompPath, directory, environment: null, extraArguments: extra);
            await process.WaitForExitAsync(ct).ConfigureAwait(true);

            string[] expected =
            [
                "--mode", "rpc", "--no-ui", "--cwd", directory,
                "--no-extensions", "-e", "/plugins/ext", "--config", "/cfg/overlay.yml",
                "--add-dir", "/other root", "--skills", "fixture-*", "--approval-mode", "write",
            ];
            (await File.ReadAllTextAsync(output, ct).ConfigureAwait(true)).TrimEnd('\n').ShouldBe(string.Join('\n', expected));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>With no extra arguments the argv is exactly the transport words plus <c>--cwd</c>.</summary>
    [Fact]
    [SupportedOSPlatform("linux")]
    public async Task StartWithNoExtraArguments_EmitsTransportWordsOnly()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("The fake omp fixture is a POSIX shell script.");
        }

        var ct = TestContext.Current.CancellationToken;
        string directory = Path.Combine(Path.GetTempPath(), "omp-rpc-argv-empty-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string output = Path.Combine(directory, LogName);
            string ompPath = WriteScript(directory, $"for a in \"$@\"; do printf '%s\\n' \"$a\"; done > '{output}'\n");

            await using OmpRpcProcess process = OmpRpcProcess.Start(ompPath, directory);
            await process.WaitForExitAsync(ct).ConfigureAwait(true);

            string[] expected = ["--mode", "rpc", "--no-ui", "--cwd", directory];
            (await File.ReadAllTextAsync(output, ct).ConfigureAwait(true)).TrimEnd('\n').ShouldBe(string.Join('\n', expected));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>A null entry is refused before any spawn, like the ACP starter refuses it.</summary>
    [Fact]
    public void StartWithNullEntry_RefusesBeforeSpawn()
    {
        string directory = Path.Combine(Path.GetTempPath(), "omp-rpc-argv-null-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            Should.Throw<ArgumentNullException>(
                () => OmpRpcProcess.Start("omp-no-such-binary-xyz", directory, environment: null, extraArguments: ["--ok", null!]));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>The client refuses a null entry at construction, before any version probe or spawn.</summary>
    [Fact]
    public void ClientWithNullEntry_RefusesAtConstruction()
    {
        Should.Throw<ArgumentNullException>(
            () => new OmpRpcClient(new OmpRpcClientOptions
            {
                Cwd = Path.GetTempPath(),
                ExtraArguments = ["--ok", null!],
            }));
    }

    /// <summary>An out-of-pin omp is still refused before the engine spawn even when extra arguments are present.</summary>
    [Fact]
    [SupportedOSPlatform("linux")]
    public async Task CreateSessionAsync_OutOfPinWithExtraArguments_ThrowsBeforeEngineSpawn()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("The fake omp fixture is a POSIX shell script.");
        }

        var ct = TestContext.Current.CancellationToken;
        string directory = Path.Combine(Path.GetTempPath(), "omp-rpc-argv-pin-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string log = Path.Combine(directory, "invocations.log");
            string ompPath = Path.Combine(directory, "omp");
            await File.WriteAllTextAsync(
                ompPath,
                "#!/bin/sh\n" + $"echo \"$*\" >> '{log}'\n" + "echo 'omp/19.0.0'\n",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                ct).ConfigureAwait(true);
            File.SetUnixFileMode(ompPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            var client = new OmpRpcClient(new OmpRpcClientOptions
            {
                OmpPath = ompPath,
                Cwd = directory,
                ExtraArguments = ["--no-extensions", "-e", "/plugins/ext"],
            });

            ProcessStartHarnessException failure = await Should.ThrowAsync<ProcessStartHarnessException>(
                () => client.CreateSessionAsync(ct)).ConfigureAwait(true);

            failure.Message.ShouldContain("omp 19.0.0 is outside the pinned 19.1.x range.", Case.Sensitive);
            (await File.ReadAllTextAsync(log, ct).ConfigureAwait(true)).ShouldBe("--version\n");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [SupportedOSPlatform("linux")]
    private static string WriteScript(string directory, string body)
    {
        string ompPath = Path.Combine(directory, "omp");
        File.WriteAllText(ompPath, "#!/bin/sh\n" + body, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.SetUnixFileMode(ompPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return ompPath;
    }
}
