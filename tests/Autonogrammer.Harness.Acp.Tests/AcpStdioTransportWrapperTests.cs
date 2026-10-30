using System.Runtime.Versioning;
using System.Text;
using Autonogrammer.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Acp.Tests;

/// <summary>
/// The optional launch wrapper sees the start info after the environment policy. A null wrapper starts the same child as the constructors
/// that do not take one. The fixture is a POSIX shell script.
/// </summary>
public sealed class AcpStdioTransportWrapperTests
{
    private const string RecorderBody = "printf '%s\\n' \"$0\" \"$@\" > \"${0%/*}/argv.txt\"\nenv > \"${0%/*}/environment.txt\"\n";

    /// <summary>A default options object has no wrapper, so the hosted path is unchanged.</summary>
    [Fact]
    public void DefaultOptionsHaveNoProcessWrapper()
    {
        new AcpClientOptions().ProcessWrapper.ShouldBeNull();
    }

    /// <summary>The wrapper sees the file name, acp plus the extra arguments, the working directory and the policy-applied environment.</summary>
    [Fact]
    [SupportedOSPlatform("linux")]
    public async Task WrapperSeesTheLaunchAfterTheEnvironmentPolicy()
    {
        SkipUnlessLinux();
        var ct = TestContext.Current.CancellationToken;
        using var parent = new ParentEnvironment();
        parent.Set("AUTONOGRAMMER_WRAPPER_SEEN", "seen");
        var directory = CreateDirectory();
        try
        {
            var script = WriteScript(directory, "record-launch");
            var wrapper = new RecordingWrapper();
            var transport = new AcpStdioTransport(
                script,
                directory,
                ["--flag"],
                new Dictionary<string, string>(StringComparer.Ordinal) { ["EXPLICIT_WRAPPER"] = "explicit" },
                EngineEnvironmentPolicy.Inherit,
                wrapper,
                null);
            await using var transportScope = transport.ConfigureAwait(true);
            await transport.ConnectAsync(ct).ConfigureAwait(true);
            await WaitForExitAsync(transport, ct).ConfigureAwait(true);

            wrapper.Seen.ShouldNotBeNull();
            wrapper.Seen.FileName.ShouldBe(script);
            wrapper.Seen.Arguments.ShouldBe(["acp", "--flag"]);
            wrapper.Seen.WorkingDirectory.ShouldBe(directory);
            wrapper.Seen.Environment["EXPLICIT_WRAPPER"].ShouldBe("explicit");
            wrapper.Seen.Environment["AUTONOGRAMMER_WRAPPER_SEEN"].ShouldBe("seen");
            var argv = await File.ReadAllLinesAsync(Path.Combine(directory, "argv.txt"), ct).ConfigureAwait(true);
            argv[0].ShouldBe(script);
            argv.Skip(1).ShouldBe(["acp", "--flag"]);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>A null environment value removes the variable in Inherit and in Allowlist, even when the allow-list names it.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [SupportedOSPlatform("linux")]
    public async Task NullEnvironmentValueRemovesTheVariable(bool allowlist)
    {
        SkipUnlessLinux();
        var ct = TestContext.Current.CancellationToken;
        using var parent = new ParentEnvironment();
        parent.Set("DISPLAY", ":9");
        var directory = CreateDirectory();
        try
        {
            var script = WriteScript(directory, "record-scrub");
            var policy = allowlist
                ? new EngineEnvironmentPolicy(EngineEnvironmentMode.Allowlist, ["DISPLAY"])
                : EngineEnvironmentPolicy.Inherit;
            var transport = new AcpStdioTransport(
                script,
                directory,
                [],
                new Dictionary<string, string>(StringComparer.Ordinal),
                policy,
                new ScrubbingWrapper(),
                null);
            await using var transportScope = transport.ConfigureAwait(true);
            await transport.ConnectAsync(ct).ConfigureAwait(true);
            await WaitForExitAsync(transport, ct).ConfigureAwait(true);

            var seen = await ReadEnvironmentAsync(directory, ct).ConfigureAwait(true);
            seen.ShouldNotContainKey("DISPLAY");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>No wrapper, including an explicit null, starts the same arguments and environment as the constructor that has no wrapper parameter.</summary>
    [Fact]
    [SupportedOSPlatform("linux")]
    public async Task NullWrapperIsByteIdentical()
    {
        SkipUnlessLinux();
        var ct = TestContext.Current.CancellationToken;
        using var parent = new ParentEnvironment();
        parent.Set("AUTONOGRAMMER_WRAPPER_IDENTICAL", "same");
        var left = CreateDirectory();
        var right = CreateDirectory();
        try
        {
            var explicitVariables = new Dictionary<string, string>(StringComparer.Ordinal) { ["EXPLICIT_WRAPPER"] = "explicit" };
            var leftTransport = new AcpStdioTransport(WriteScript(left, "record-left"), left, ["--flag"], explicitVariables, EngineEnvironmentPolicy.Inherit, null);
            var rightTransport = new AcpStdioTransport(WriteScript(right, "record-right"), right, ["--flag"], explicitVariables, EngineEnvironmentPolicy.Inherit, processWrapper: null, logger: null);
            await using var leftScope = leftTransport.ConfigureAwait(true);
            await using var rightScope = rightTransport.ConfigureAwait(true);
            await RunAsync(leftTransport, ct).ConfigureAwait(true);
            await RunAsync(rightTransport, ct).ConfigureAwait(true);
            var leftArgv = await File.ReadAllLinesAsync(Path.Combine(left, "argv.txt"), ct).ConfigureAwait(true);
            var rightArgv = await File.ReadAllLinesAsync(Path.Combine(right, "argv.txt"), ct).ConfigureAwait(true);
            leftArgv.Skip(1).ShouldBe(rightArgv.Skip(1));
            var leftEnv = await ReadEnvironmentAsync(left, ct).ConfigureAwait(true);
            var rightEnv = await ReadEnvironmentAsync(right, ct).ConfigureAwait(true);
            leftEnv["EXPLICIT_WRAPPER"].ShouldBe(rightEnv["EXPLICIT_WRAPPER"]);
            leftEnv["AUTONOGRAMMER_WRAPPER_IDENTICAL"].ShouldBe(rightEnv["AUTONOGRAMMER_WRAPPER_IDENTICAL"]);
            leftEnv.Count.ShouldBe(rightEnv.Count);
        }
        finally
        {
            Directory.Delete(left, recursive: true);
            Directory.Delete(right, recursive: true);
        }
    }

    private static async Task RunAsync(AcpStdioTransport transport, CancellationToken cancellationToken)
    {
        await using var transportScope = transport.ConfigureAwait(true);
        await transport.ConnectAsync(cancellationToken).ConfigureAwait(true);
        await WaitForExitAsync(transport, cancellationToken).ConfigureAwait(true);
    }

    private static async Task WaitForExitAsync(AcpStdioTransport transport, CancellationToken cancellationToken)
    {
        var frames = transport.ReceiveAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);
        await using var framesScope = frames.ConfigureAwait(true);
        await Should.ThrowAsync<DisconnectedHarnessException>(async () => await frames.MoveNextAsync().ConfigureAwait(true)).ConfigureAwait(true);
    }

    private static async Task<Dictionary<string, string>> ReadEnvironmentAsync(string directory, CancellationToken cancellationToken)
    {
        var seen = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in await File.ReadAllLinesAsync(Path.Combine(directory, "environment.txt"), cancellationToken).ConfigureAwait(true))
        {
            var separator = line.IndexOf('=', StringComparison.Ordinal);
            if (separator > 0)
            {
                seen[line[..separator]] = line[(separator + 1)..];
            }
        }

        return seen;
    }

    private static void SkipUnlessLinux()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("The wrapper fixture uses a POSIX shell script.");
        }
    }

    [SupportedOSPlatform("linux")]
    private static string WriteScript(string directory, string name)
    {
        var path = Path.Combine(directory, name);
        File.WriteAllText(path, "#!/bin/sh\n" + RecorderBody, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return path;
    }

    private static string CreateDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "acp-wrapper-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed class RecordingWrapper : IProcessLaunchWrapper
    {
        public ProcessLaunch? Seen { get; private set; }

        public ProcessLaunch Wrap(ProcessLaunch intended)
        {
            Seen = intended;
            return intended;
        }
    }

    private sealed class ScrubbingWrapper : IProcessLaunchWrapper
    {
        public ProcessLaunch Wrap(ProcessLaunch intended)
        {
            var environment = new Dictionary<string, string?>(intended.Environment, StringComparer.Ordinal)
            {
                ["DISPLAY"] = null,
            };
            return intended with { Environment = environment };
        }
    }

    private sealed class ParentEnvironment : IDisposable
    {
        private readonly Dictionary<string, string?> _originals = new(StringComparer.Ordinal);

        public void Set(string name, string value)
        {
            _ = _originals.TryAdd(name, Environment.GetEnvironmentVariable(name));
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose()
        {
            foreach (var (name, original) in _originals)
            {
                Environment.SetEnvironmentVariable(name, original);
            }
        }
    }
}
