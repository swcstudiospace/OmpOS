using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace OmpOS.Harness.Mcp.Tests;

/// <summary>Runs the fixture in Streamable HTTP mode on a free loopback port for the lifetime of one test.</summary>
internal sealed class FixtureHttpProcess : IDisposable
{
    private readonly Process _process;

    private FixtureHttpProcess(Process process, int port, string callLogPath)
    {
        _process = process;
        Port = port;
        CallLogPath = callLogPath;
    }

    /// <summary>The loopback port the server listens on.</summary>
    public int Port { get; }

    /// <summary>The file the server appends each received tool call to.</summary>
    public string CallLogPath { get; }

    /// <summary>The MCP endpoint URL.</summary>
    public Uri Endpoint => new("http://127.0.0.1:" + Port.ToString(CultureInfo.InvariantCulture) + "/mcp");

    /// <summary>Starts the fixture and waits until it accepts connections.</summary>
    /// <param name="callLogPath">The call log file the server writes to.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The running fixture.</returns>
    public static async Task<FixtureHttpProcess> StartAsync(string callLogPath, CancellationToken cancellationToken)
    {
        var port = FreeLoopbackPort();
        var startInfo = new ProcessStartInfo(McpFixtureProcess.Executable)
        {
            WorkingDirectory = AppContext.BaseDirectory,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("--http");
        startInfo.ArgumentList.Add(port.ToString(CultureInfo.InvariantCulture));
        startInfo.Environment[McpFixtureProcess.CallLogVariable] = callLogPath;
        startInfo.Environment["Logging__LogLevel__Default"] = "Warning";

        var process = Process.Start(startInfo) ?? throw new InvalidOperationException("The MCP fixture did not start.");
        var fixture = new FixtureHttpProcess(process, port, callLogPath);
        try
        {
            await fixture.WaitUntilListeningAsync(cancellationToken).ConfigureAwait(true);
            return fixture;
        }
        catch
        {
            fixture.Dispose();
            throw;
        }
    }

    /// <summary>The tool names the server has received, in order; empty when nothing reached it.</summary>
    public IReadOnlyList<string> ReceivedTools =>
        File.Exists(CallLogPath) ? File.ReadAllLines(CallLogPath) : Array.Empty<string>();

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
            _process.WaitForExit();
        }

        _process.Dispose();
    }

    private static int FreeLoopbackPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private async Task WaitUntilListeningAsync(CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (true)
        {
            if (_process.HasExited)
            {
                throw new InvalidOperationException("The MCP fixture exited with code " + _process.ExitCode.ToString(CultureInfo.InvariantCulture) + " before listening.");
            }

            using var probe = new TcpClient();
            try
            {
                await probe.ConnectAsync(IPAddress.Loopback, Port, cancellationToken).ConfigureAwait(true);
                return;
            }
            catch (SocketException) when (DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(true);
            }
        }
    }
}
