using System.Runtime.Versioning;
using System.Text;
using OmpOS.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Acp.Tests;

/// <summary>
/// Argument and environment plumbing of <see cref="AcpStdioTransport"/> and <see cref="OmpAcpClient"/>, checked against a real
/// child process: a POSIX script that records its own argv and selected environment variables to a file, then exits.
/// </summary>
public sealed class AcpLaunchPlumbingTests
{
    private const string OverriddenHome = "/nonexistent-ompos-acp-test-home";
    private const string AddedValue = "added-value";

    // Only shell builtins are used, so overriding PATH in the child cannot break the recorder.
    private const string RecorderBody =
        "out=\"${0%/*}/record.txt\"\n" +
        "{ printf 'ARG:%s\\n' \"$@\"; printf 'HOME=%s\\nPATH=%s\\nADDED=%s\\n' \"$HOME\" \"$PATH\" \"$OMPOS_ACP_TEST_ADDED\"; } > \"$out\"\n";

    /// <summary>Extra arguments follow <c>acp</c> in order, one argv element each and unsplit; the environment overrides and adds variables while the rest is inherited.</summary>
    [Fact]
    [SupportedOSPlatform("linux")]
    public async Task ExtraArgumentsAndEnvironmentReachTheChildExactly()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("The launch fixture uses a POSIX shell script.");
        }

        var ct = TestContext.Current.CancellationToken;
        var directory = CreateDirectory();
        try
        {
            var script = WriteScript(directory, "record-launch", RecorderBody);
            string[] arguments = ["--no-extensions", "-e", "/opt/ext one", "-e", "/opt/ext-two", "--flag=$HOME"];
            var environment = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["HOME"] = OverriddenHome,
                ["OMPOS_ACP_TEST_ADDED"] = AddedValue,
            };
            var transport = new AcpStdioTransport(script, directory, arguments, environment);
            await using var transportScope = transport.ConfigureAwait(true);
            await transport.ConnectAsync(ct).ConfigureAwait(true);

            await WaitForExitAsync(transport, ct).ConfigureAwait(true);

            var lines = await File.ReadAllLinesAsync(Path.Combine(directory, "record.txt"), ct).ConfigureAwait(true);
            string[] expected =
            [
                "ARG:acp",
                "ARG:--no-extensions",
                "ARG:-e",
                "ARG:/opt/ext one",
                "ARG:-e",
                "ARG:/opt/ext-two",
                "ARG:--flag=$HOME",
                "HOME=" + OverriddenHome,
                "PATH=" + Environment.GetEnvironmentVariable("PATH"),
                "ADDED=" + AddedValue,
            ];
            string.Join('\n', lines).ShouldBe(string.Join('\n', expected));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>The original constructor still launches <c>omp acp</c> with exactly the single <c>acp</c> argument and the unmodified environment.</summary>
    [Fact]
    [SupportedOSPlatform("linux")]
    public async Task DefaultConstructorAddsNothingToArgvOrEnvironment()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("The launch fixture uses a POSIX shell script.");
        }

        var ct = TestContext.Current.CancellationToken;
        var directory = CreateDirectory();
        try
        {
            var script = WriteScript(directory, "record-launch", RecorderBody);
            var transport = new AcpStdioTransport(script, directory);
            await using var transportScope = transport.ConfigureAwait(true);
            await transport.ConnectAsync(ct).ConfigureAwait(true);

            await WaitForExitAsync(transport, ct).ConfigureAwait(true);

            var lines = await File.ReadAllLinesAsync(Path.Combine(directory, "record.txt"), ct).ConfigureAwait(true);
            lines[0].ShouldBe("ARG:acp");
            lines.Count(static line => line.StartsWith("ARG:", StringComparison.Ordinal)).ShouldBe(1);
            lines[^1].ShouldBe("ADDED=" + Environment.GetEnvironmentVariable("OMPOS_ACP_TEST_ADDED"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>OmpAcpClient hands the options' ExtraArguments and EnvironmentVariables to the process it spawns.</summary>
    [Fact]
    [SupportedOSPlatform("linux")]
    public async Task ClientPassesOptionsThroughToTheSpawnedProcess()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("The launch fixture uses a POSIX shell script.");
        }

        var ct = TestContext.Current.CancellationToken;
        var directory = CreateDirectory();
        try
        {
            var script = WriteScript(directory, "record-launch", RecorderBody);
            var client = new OmpAcpClient();
            var options = new AcpClientOptions
            {
                OmpPath = script,
                WorkingDirectory = directory,
                HandshakeTimeout = TimeSpan.FromSeconds(20),
                ExtraArguments = ["--no-extensions", "-e", "/opt/ext-a"],
                EnvironmentVariables = new Dictionary<string, string>(StringComparer.Ordinal) { ["OMPOS_ACP_TEST_ADDED"] = AddedValue },
            };
            await using var session = client.CreateSession(options);

            // The recorder exits without speaking ACP, so the handshake fails; the launch is what is under test.
            var failure = await Record.ExceptionAsync(() => session.CreateAsync(ct)).ConfigureAwait(true);

            failure.ShouldBeAssignableTo<HarnessException>();
            var lines = await File.ReadAllLinesAsync(Path.Combine(directory, "record.txt"), ct).ConfigureAwait(true);
            string.Join('\n', lines.Take(4)).ShouldBe("ARG:acp\nARG:--no-extensions\nARG:-e\nARG:/opt/ext-a");
            lines[^1].ShouldBe("ADDED=" + AddedValue);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>A null argument, a blank variable name and a name containing '=' are rejected before any process starts, without echoing the value.</summary>
    [Fact]
    public void InvalidArgumentsOrEnvironmentAreRejectedWithoutEchoingValues()
    {
        const string secret = "launch-secret-value";
        string[] nullArgument = [null!];
        var noEnvironment = new Dictionary<string, string>(StringComparer.Ordinal);
        var withEquals = new Dictionary<string, string>(StringComparer.Ordinal) { ["A=B"] = secret };
        var blankName = new Dictionary<string, string>(StringComparer.Ordinal) { [" "] = secret };
        string[] noArguments = [];

        Should.Throw<ArgumentNullException>(() => new AcpStdioTransport("omp", null, nullArgument, noEnvironment));
        var equalsFailure = Should.Throw<ArgumentException>(() => new AcpStdioTransport("omp", null, noArguments, withEquals));
        var blankFailure = Should.Throw<ArgumentException>(() => new AcpStdioTransport("omp", null, noArguments, blankName));

        equalsFailure.Message.ShouldNotContain(secret, Case.Sensitive);
        blankFailure.Message.ShouldNotContain(secret, Case.Sensitive);
    }

    private static async Task WaitForExitAsync(AcpStdioTransport transport, CancellationToken cancellationToken)
    {
        var frames = transport.ReceiveAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);
        await using var framesScope = frames.ConfigureAwait(true);
        await Should.ThrowAsync<DisconnectedHarnessException>(async () => await frames.MoveNextAsync().ConfigureAwait(true))
            .ConfigureAwait(true);
    }

    private static string CreateDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "acp-launch-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    [SupportedOSPlatform("linux")]
    private static string WriteScript(string directory, string name, string body)
    {
        var path = Path.Combine(directory, name);
        File.WriteAllText(path, "#!/bin/sh\n" + body, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return path;
    }
}
