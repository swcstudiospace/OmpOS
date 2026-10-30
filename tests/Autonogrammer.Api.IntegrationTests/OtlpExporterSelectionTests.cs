using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Exporter;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests;

/// <summary>
/// OPS-03 exporter selection, observed on the wire: <c>Otel:Exporter=otlp</c> sends telemetry to the configured OTLP endpoint, and the
/// default <c>none</c> sends nothing. A localhost collector records the path of every export request it receives.
/// </summary>
/// <remarks>
/// <c>Program</c> reads <c>Otel:Exporter</c> while it composes the host, and the test host applies its configuration overrides after that
/// (see <c>SessionStoreRegistration</c>). The selection is therefore made through the process environment (<c>Otel__Exporter</c>), the
/// source a deployment uses. The variable is process-wide: a host created while it is set builds an otlp exporter for its own default
/// endpoint, which never reaches this collector. xUnit runs the tests of one class sequentially, so the positive case never overlaps the
/// negative case.
/// </remarks>
public sealed class OtlpExporterSelectionTests
{
    private const string ExporterVariable = "Otel__Exporter";

    /// <summary>
    /// The batch processor exports on a five-second schedule. The absent-export window is longer than that schedule, so an export that
    /// would have happened has happened by the time the window ends.
    /// </summary>
    private static readonly TimeSpan ExportWindow = TimeSpan.FromSeconds(12);

    /// <summary>
    /// Verifies the otlp exporter sends export requests to the configured endpoint while the host runs. The first export is the trace
    /// batch, which fires on its five-second schedule; the metric reader fires at 60 seconds and does not run in this window.
    /// </summary>
    [Fact]
    public async Task OtlpExporter_SendsExportRequestsToTheConfiguredEndpoint()
    {
        var testToken = TestContext.Current.CancellationToken;
        using var collector = new FakeOtlpCollector();
        using var stop = new CancellationTokenSource();
        var serving = collector.ServeAsync(stop.Token);
        Environment.SetEnvironmentVariable(ExporterVariable, "otlp");
        try
        {
            using (var factory = CreateHost(collector.Endpoint))
            {
                using var client = factory.CreateClient();
                using var response = await client.GetAsync(new Uri("/health/live", UriKind.Relative), testToken).ConfigureAwait(true);
                response.StatusCode.ShouldBe(HttpStatusCode.OK);

                var exported = await collector.WaitForPathAsync(collector.Endpoint.AbsolutePath, TimeSpan.FromSeconds(30), testToken).ConfigureAwait(true);
                exported.ShouldBeTrue("the otlp exporter posts export requests to the configured endpoint");
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable(ExporterVariable, null);
            await stop.CancelAsync().ConfigureAwait(true);
            await serving.ConfigureAwait(true);
        }
    }

    /// <summary>Verifies the default exporter setting sends no telemetry to a configured endpoint, even after the export window.</summary>
    [Fact]
    public async Task DefaultExporter_SendsNoTelemetry()
    {
        var testToken = TestContext.Current.CancellationToken;
        using var collector = new FakeOtlpCollector();
        using var stop = new CancellationTokenSource();
        var serving = collector.ServeAsync(stop.Token);
        try
        {
            using (var factory = CreateHost(collector.Endpoint))
            {
                using var client = factory.CreateClient();
                using var response = await client.GetAsync(new Uri("/health/live", UriKind.Relative), testToken).ConfigureAwait(true);
                response.StatusCode.ShouldBe(HttpStatusCode.OK);

                await Task.Delay(ExportWindow, testToken).ConfigureAwait(true);
            }
        }
        finally
        {
            await stop.CancelAsync().ConfigureAwait(true);
            await serving.ConfigureAwait(true);
        }

        collector.ReceivedPaths().ShouldBeEmpty();
    }

    private static ApiFactory CreateHost(Uri endpoint) =>
        new(configureServices: services => services.PostConfigure<OtlpExporterOptions>(options =>
        {
            options.Endpoint = endpoint;
            options.Protocol = OtlpExportProtocol.HttpProtobuf;
        }));

    /// <summary>
    /// A localhost HTTP listener that records the request path of every POST, then answers 200 with an empty body. Requests are
    /// served one at a time, and each connection closes after its response.
    /// </summary>
    private sealed class FakeOtlpCollector : IDisposable
    {
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);
        private static readonly byte[] OkResponse = "HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"u8.ToArray();

        private readonly TcpListener _listener;
        private readonly ConcurrentQueue<string> _paths = new();

        /// <summary>Initializes a new instance of the <see cref="FakeOtlpCollector"/> class and binds an ephemeral loopback port.</summary>
        public FakeOtlpCollector()
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            Endpoint = new Uri($"http://127.0.0.1:{port.ToString(CultureInfo.InvariantCulture)}/");
        }

        /// <summary>Gets the base endpoint the exporter should target.</summary>
        public Uri Endpoint { get; }

        /// <summary>Returns the request paths received so far, in arrival order.</summary>
        /// <returns>A snapshot of the received paths.</returns>
        public string[] ReceivedPaths() => [.. _paths];

        /// <summary>Waits until a request for <paramref name="path"/> has been received.</summary>
        /// <param name="path">The request path to wait for.</param>
        /// <param name="timeout">How long to wait.</param>
        /// <param name="token">Cancels the wait.</param>
        /// <returns><see langword="true"/> when the path arrived within the timeout.</returns>
        public async Task<bool> WaitForPathAsync(string path, TimeSpan timeout, CancellationToken token)
        {
            var deadline = DateTimeOffset.UtcNow + timeout;
            while (!_paths.Any(received => string.Equals(received, path, StringComparison.Ordinal)))
            {
                if (DateTimeOffset.UtcNow >= deadline)
                {
                    return false;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(100), token).ConfigureAwait(false);
            }

            return true;
        }

        /// <summary>Accepts and answers requests until <paramref name="stop"/> is cancelled.</summary>
        /// <param name="stop">Ends the accept loop when cancelled.</param>
        /// <returns>A task that completes when the loop stops.</returns>
        public async Task ServeAsync(CancellationToken stop)
        {
            while (!stop.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync(stop).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stop.IsCancellationRequested)
                {
                    return;
                }

                using (client)
                {
                    try
                    {
                        await ServeClientAsync(client, stop).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (stop.IsCancellationRequested)
                    {
                        return;
                    }
                }
            }
        }

        /// <inheritdoc />
        public void Dispose() => _listener.Dispose();

        private async Task ServeClientAsync(TcpClient client, CancellationToken stop)
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stop);
            deadline.CancelAfter(RequestTimeout);
            var stream = client.GetStream();
            var chunk = new byte[8192];
            using var received = new MemoryStream();

            var headerEnd = -1;
            while (headerEnd < 0)
            {
                var read = await stream.ReadAsync(chunk, deadline.Token).ConfigureAwait(false);
                if (read == 0)
                {
                    return;
                }

                await received.WriteAsync(chunk.AsMemory(0, read), deadline.Token).ConfigureAwait(false);
                headerEnd = received.GetBuffer().AsSpan(0, (int)received.Length).IndexOf("\r\n\r\n"u8);
            }

            var head = Encoding.ASCII.GetString(received.GetBuffer(), 0, headerEnd);
            var lines = head.Split("\r\n");
            var target = lines[0].Split(' ')[1];
            var remaining = ContentLength(lines) - (received.Length - (headerEnd + 4));
            while (remaining > 0)
            {
                var read = await stream.ReadAsync(chunk, deadline.Token).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                remaining -= read;
            }

            _paths.Enqueue(target);
            await stream.WriteAsync(OkResponse, deadline.Token).ConfigureAwait(false);
        }

        private static long ContentLength(string[] headerLines)
        {
            foreach (var line in headerLines)
            {
                var separator = line.IndexOf(':', StringComparison.Ordinal);
                if (separator > 0 && string.Equals(line[..separator], "Content-Length", StringComparison.OrdinalIgnoreCase))
                {
                    return long.Parse(line[(separator + 1)..].Trim(), CultureInfo.InvariantCulture);
                }
            }

            return 0;
        }
    }
}
