using System.Diagnostics;
using System.Net.WebSockets;
using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection;
using Autonogrammer.AgentWorker.Bridge;
using Shouldly;
using Xunit;

namespace Autonogrammer.AgentWorker.Tests;

/// <summary>The life of the omp child: where it runs, one child per session, and that it dies with the socket, the shutdown or the omp exit.</summary>
[SupportedOSPlatform("linux")]
public sealed class OmpProcessLifecycleTests
{
    private const string Session = "session-1";
    private const string InitializeRequest = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\"}";

    /// <summary>The child runs in the session's own directory under the workspace root, which the worker creates.</summary>
    [Fact]
    public async Task ChildRunsInTheSessionDirectory()
    {
        using var timeout = TestTimeouts.Create();
        using var tokens = new WorkerTokenFactory();
        var clock = new FixedTimeProvider();
        var cwdFile = Path.Combine(System.IO.Path.GetTempPath(), "cwd-" + Guid.NewGuid().ToString("N"));
        try
        {
            var host = await WorkerHost.StartAsync(tokens, clock, ["--cwd-file", cwdFile], cancellationToken: timeout.Token).ConfigureAwait(true);
            await using var hostScope = host.ConfigureAwait(true);
            using var socket = await WorkerSocket.ConnectAsync(host.AcpUri, tokens.Mint(Session, clock.GetUtcNow()), timeout.Token).ConfigureAwait(true);
            await WorkerSocket.SendTextAsync(socket, InitializeRequest, timeout.Token).ConfigureAwait(true);
            await WorkerSocket.ReceiveAsync(socket, timeout.Token).ConfigureAwait(true);

            var reported = (await File.ReadAllTextAsync(cwdFile, timeout.Token).ConfigureAwait(true)).Trim();

            reported.ShouldBe(Path.Combine(host.WorkspaceRoot, Session));
            Directory.Exists(reported).ShouldBeTrue();
        }
        finally
        {
            File.Delete(cwdFile);
        }
    }

    /// <summary>Each session gets its own omp child, and both run at once.</summary>
    [Fact]
    public async Task EachSessionGetsItsOwnChild()
    {
        using var timeout = TestTimeouts.Create();
        using var tokens = new WorkerTokenFactory();
        var clock = new FixedTimeProvider();
        var pidFile = Path.Combine(System.IO.Path.GetTempPath(), "pid-" + Guid.NewGuid().ToString("N"));
        try
        {
            var host = await WorkerHost.StartAsync(tokens, clock, ["--pidfile", pidFile], cancellationToken: timeout.Token).ConfigureAwait(true);
            await using var hostScope = host.ConfigureAwait(true);
            using var first = await WorkerSocket.ConnectAsync(host.AcpUri, tokens.Mint("session-a", clock.GetUtcNow()), timeout.Token).ConfigureAwait(true);
            await WorkerSocket.SendTextAsync(first, InitializeRequest, timeout.Token).ConfigureAwait(true);
            await WorkerSocket.ReceiveAsync(first, timeout.Token).ConfigureAwait(true);
            var firstPid = int.Parse((await File.ReadAllTextAsync(pidFile, timeout.Token).ConfigureAwait(true)).Trim(), System.Globalization.CultureInfo.InvariantCulture);
            File.Delete(pidFile);
            using var second = await WorkerSocket.ConnectAsync(host.AcpUri, tokens.Mint("session-b", clock.GetUtcNow()), timeout.Token).ConfigureAwait(true);
            await WorkerSocket.SendTextAsync(second, InitializeRequest, timeout.Token).ConfigureAwait(true);
            await WorkerSocket.ReceiveAsync(second, timeout.Token).ConfigureAwait(true);
            var secondPid = int.Parse((await File.ReadAllTextAsync(pidFile, timeout.Token).ConfigureAwait(true)).Trim(), System.Globalization.CultureInfo.InvariantCulture);

            secondPid.ShouldNotBe(firstPid);
            host.Services.GetRequiredService<OmpProcessHost>().AttachedCount.ShouldBe(2);
        }
        finally
        {
            File.Delete(pidFile);
        }
    }

    /// <summary>Closing the socket ends the child, and the session is freed.</summary>
    [Fact]
    public async Task ClosingTheSocketEndsTheChild()
    {
        using var timeout = TestTimeouts.Create();
        using var tokens = new WorkerTokenFactory();
        var clock = new FixedTimeProvider();
        var pidFile = Path.Combine(System.IO.Path.GetTempPath(), "pid-" + Guid.NewGuid().ToString("N"));
        try
        {
            var host = await WorkerHost.StartAsync(tokens, clock, ["--pidfile", pidFile], cancellationToken: timeout.Token).ConfigureAwait(true);
            await using var hostScope = host.ConfigureAwait(true);
            var processes = host.Services.GetRequiredService<OmpProcessHost>();
            using var socket = await WorkerSocket.ConnectAsync(host.AcpUri, tokens.Mint(Session, clock.GetUtcNow()), timeout.Token).ConfigureAwait(true);
            await WorkerSocket.SendTextAsync(socket, InitializeRequest, timeout.Token).ConfigureAwait(true);
            await WorkerSocket.ReceiveAsync(socket, timeout.Token).ConfigureAwait(true);
            var pid = int.Parse((await File.ReadAllTextAsync(pidFile, timeout.Token).ConfigureAwait(true)).Trim(), System.Globalization.CultureInfo.InvariantCulture);
            using var child = Process.GetProcessById(pid);

            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", timeout.Token).ConfigureAwait(true);
            await child.WaitForExitAsync(timeout.Token).ConfigureAwait(true);
            await processes.WaitForReleaseAsync(Session).WaitAsync(timeout.Token).ConfigureAwait(true);

            child.HasExited.ShouldBeTrue();
            processes.AttachedCount.ShouldBe(0);
        }
        finally
        {
            File.Delete(pidFile);
        }
    }

    /// <summary>A child that ignores stdin EOF is killed when the socket closes.</summary>
    [Fact]
    public async Task ChildThatIgnoresEndOfInputIsKilledWhenTheSocketCloses()
    {
        using var timeout = TestTimeouts.Create();
        using var tokens = new WorkerTokenFactory();
        var clock = new FixedTimeProvider();
        var pidFile = Path.Combine(System.IO.Path.GetTempPath(), "pid-" + Guid.NewGuid().ToString("N"));
        try
        {
            var host = await WorkerHost.StartAsync(tokens, clock, ["--pidfile", pidFile, "--mode", "stubborn"], cancellationToken: timeout.Token).ConfigureAwait(true);
            await using var hostScope = host.ConfigureAwait(true);
            var processes = host.Services.GetRequiredService<OmpProcessHost>();
            using var socket = await WorkerSocket.ConnectAsync(host.AcpUri, tokens.Mint(Session, clock.GetUtcNow()), timeout.Token).ConfigureAwait(true);
            var pid = await ReadPidAsync(pidFile, timeout.Token).ConfigureAwait(true);
            using var child = Process.GetProcessById(pid);

            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", timeout.Token).ConfigureAwait(true);
            await child.WaitForExitAsync(timeout.Token).ConfigureAwait(true);
            await processes.WaitForReleaseAsync(Session).WaitAsync(timeout.Token).ConfigureAwait(true);

            child.HasExited.ShouldBeTrue();
        }
        finally
        {
            File.Delete(pidFile);
        }
    }

    /// <summary>A graceful shutdown tells the client 1001 and kills the child.</summary>
    [Fact]
    public async Task ShutdownClosesWith1001AndKillsTheChild()
    {
        using var timeout = TestTimeouts.Create();
        using var tokens = new WorkerTokenFactory();
        var clock = new FixedTimeProvider();
        var pidFile = Path.Combine(System.IO.Path.GetTempPath(), "pid-" + Guid.NewGuid().ToString("N"));
        try
        {
            var host = await WorkerHost.StartAsync(tokens, clock, ["--pidfile", pidFile, "--mode", "stubborn"], cancellationToken: timeout.Token).ConfigureAwait(true);
            await using var hostScope = host.ConfigureAwait(true);
            using var socket = await WorkerSocket.ConnectAsync(host.AcpUri, tokens.Mint(Session, clock.GetUtcNow()), timeout.Token).ConfigureAwait(true);
            var pid = await ReadPidAsync(pidFile, timeout.Token).ConfigureAwait(true);
            using var child = Process.GetProcessById(pid);

            var stopping = host.StopAsync(timeout.Token);
            var close = await WorkerSocket.ReceiveAsync(socket, timeout.Token).ConfigureAwait(true);
            await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "ack", timeout.Token).ConfigureAwait(true);
            await stopping.ConfigureAwait(true);
            await child.WaitForExitAsync(timeout.Token).ConfigureAwait(true);

            close.Type.ShouldBe(WebSocketMessageType.Close);
            close.CloseStatus.ShouldBe(WebSocketCloseStatus.EndpointUnavailable);
        }
        finally
        {
            File.Delete(pidFile);
        }
    }

    /// <summary>A worker whose omp executable does not exist answers the upgrade with 503 and frees the session.</summary>
    [Fact]
    public async Task MissingOmpExecutableIsRefusedWith503AndTheSessionIsFreed()
    {
        using var timeout = TestTimeouts.Create();
        using var tokens = new WorkerTokenFactory();
        var clock = new FixedTimeProvider();
        var settings = new Dictionary<string, string?>(StringComparer.Ordinal) { ["Worker:Omp:Path"] = "/nonexistent/omp-does-not-exist" };
        var host = await WorkerHost.StartAsync(tokens, clock, settings: settings, cancellationToken: timeout.Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);

        var failure = await Should.ThrowAsync<WebSocketException>(
            () => WorkerSocket.ConnectAsync(host.AcpUri, tokens.Mint(Session, clock.GetUtcNow()), timeout.Token)).ConfigureAwait(true);

        failure.Message.ShouldContain("503", Case.Sensitive);
        host.Services.GetRequiredService<OmpProcessHost>().AttachedCount.ShouldBe(0);
    }

    private static async Task<int> ReadPidAsync(string pidFile, CancellationToken cancellationToken)
    {
        // The fake writes its pid as the first thing it does; the connection is already upgraded, so the file appears without delay.
        while (!File.Exists(pidFile) || new FileInfo(pidFile).Length == 0)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
        }

        return int.Parse((await File.ReadAllTextAsync(pidFile, cancellationToken).ConfigureAwait(false)).Trim(), System.Globalization.CultureInfo.InvariantCulture);
    }
}
