using System.Runtime.Versioning;
using System.Text;
using Autonogrammer.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Acp.Tests;

/// <summary>Exercises <see cref="AcpStdioTransport"/> against a real child process: a shell script that stands in for <c>omp acp</c>.</summary>
public sealed class AcpStdioTransportTests
{
    /// <summary>Launching a missing executable fails with ProcessStartHarnessException.</summary>
    [Fact]
    public async Task MissingExecutableFailsToStart()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new AcpStdioTransport("/nonexistent/omp-acp-test-executable");
        await using var transportScope = transport.ConfigureAwait(true);

        await Should.ThrowAsync<ProcessStartHarnessException>(() => transport.ConnectAsync(ct)).ConfigureAwait(true);
    }

    /// <summary>Frames sent to the child come back as received frames, stderr is drained, and exit surfaces as disconnection.</summary>
    [Fact]
    [SupportedOSPlatform("linux")]
    public async Task FramesRoundTripThroughChildAndStderrIsDrained()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("The stdio fixture uses a POSIX shell script.");
        }

        var ct = TestContext.Current.CancellationToken;
        var directory = CreateDirectory();
        try
        {
            var script = WriteScript(directory, "echo-once", "read line\nprintf '%s\\n' \"$line\"\necho 'diagnostic line' 1>&2\nprintf '%s\\n' '{\"jsonrpc\":\"2.0\",\"method\":\"ping\"}'\n");
            var transport = new AcpStdioTransport(script, directory);
            await using var transportScope = transport.ConfigureAwait(true);
            await transport.ConnectAsync(ct).ConfigureAwait(true);

            var request = "{\"jsonrpc\":\"2.0\",\"id\":7,\"method\":\"echo\"}";
            await transport.SendAsync(AcpFixtures.Utf8(request), ct).ConfigureAwait(true);

            var frames = transport.ReceiveAsync(ct).GetAsyncEnumerator(ct);
            await using var framesScope = frames.ConfigureAwait(true);
            (await frames.MoveNextAsync().ConfigureAwait(true)).ShouldBeTrue();
            AcpFixtures.Text(frames.Current.ToArray()).ShouldBe(request);
            (await frames.MoveNextAsync().ConfigureAwait(true)).ShouldBeTrue();
            AcpFixtures.Text(frames.Current.ToArray()).ShouldBe("{\"jsonrpc\":\"2.0\",\"method\":\"ping\"}");

            var exited = await Should.ThrowAsync<DisconnectedHarnessException>(async () => await frames.MoveNextAsync().ConfigureAwait(true))
                .ConfigureAwait(true);
            exited.Message.ShouldContain("exited with code 0");

            (await transport.StandardError.ReadAsync(ct).ConfigureAwait(true)).ShouldBe("diagnostic line");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>A line that is not a JSON object is rejected as a malformed frame, with the content in the message.</summary>
    [Fact]
    [SupportedOSPlatform("linux")]
    public async Task NonJsonLineFromChildIsRejectedAsMalformed()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("The stdio fixture uses a POSIX shell script.");
        }

        var ct = TestContext.Current.CancellationToken;
        var directory = CreateDirectory();
        try
        {
            var script = WriteScript(directory, "noisy", "read line\necho 'banner not json'\n");
            var transport = new AcpStdioTransport(script, directory);
            await using var transportScope = transport.ConfigureAwait(true);
            await transport.ConnectAsync(ct).ConfigureAwait(true);
            await transport.SendAsync(AcpFixtures.Utf8("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\"}"), ct).ConfigureAwait(true);

            var frames = transport.ReceiveAsync(ct).GetAsyncEnumerator(ct);
            await using var framesScope = frames.ConfigureAwait(true);
            var malformed = await Should.ThrowAsync<MalformedFrameHarnessException>(async () => await frames.MoveNextAsync().ConfigureAwait(true))
                .ConfigureAwait(true);
            malformed.Message.ShouldContain("banner not json");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>A child line longer than the 1 MiB transport limit is refused as a malformed frame instead of being buffered.</summary>
    [Fact]
    [SupportedOSPlatform("linux")]
    public async Task OversizedLineFromChildIsRejectedAsMalformed()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("The stdio fixture uses a POSIX shell script.");
        }

        var ct = TestContext.Current.CancellationToken;
        var directory = CreateDirectory();
        try
        {
            // 1,100,000 characters on one line, over the 1 MiB (1,048,576-character) limit.
            var script = WriteScript(directory, "oversized", "head -c 1100000 /dev/zero | tr '\\0' 'a'\necho\n");
            var transport = new AcpStdioTransport(script, directory);
            await using var transportScope = transport.ConfigureAwait(true);
            await transport.ConnectAsync(ct).ConfigureAwait(true);

            var frames = transport.ReceiveAsync(ct).GetAsyncEnumerator(ct);
            await using var framesScope = frames.ConfigureAwait(true);
            var malformed = await Should.ThrowAsync<MalformedFrameHarnessException>(async () => await frames.MoveNextAsync().ConfigureAwait(true))
                .ConfigureAwait(true);
            malformed.Message.ShouldContain("1 MiB");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "acp-stdio-" + Guid.NewGuid().ToString("N"));
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
