using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Autonogrammer.Harness.BrowserUse.Chromium;
using Autonogrammer.Harness.BrowserUse.Options;
using Autonogrammer.Harness.BrowserUse.Policy;
using Autonogrammer.Native.Cowork;
using PuppeteerSharp;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Browser;

/// <summary>
/// The production page source the desktop composes, run against live Chromium. Skipped unless <c>AUTONOGRAMMER_LIVE_BROWSER=1</c>
/// and <c>CHROME_BIN</c> is set. The loopback origin is granted for the session, so its page loads through the egress proxy.
/// The same origin under a host name that was never granted is refused, and the origin never receives that request.
/// </summary>
public sealed class BrowserProductionPageLiveTests
{
    private const string GrantedBody = "granted-body-7c4e1f";
    private const string DeniedBody = "denied-body-2b9a60";
    private const string SessionId = "live-production-session";
    private const string SandboxVariable = "AUTONOGRAMMER_BROWSER_ALLOW_NO_SANDBOX";

    /// <summary>Parses a CDP parameter object literal into a detached element. PuppeteerSharp cannot serialize anonymous types here.</summary>
    private static JsonElement Params(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    /// <summary>Encodes text as a JSON string literal.</summary>
    private static string Quoted(string value) => "\"" + JsonEncodedText.Encode(value).ToString() + "\"";

    /// <summary>A granted loopback origin loads through the proxy, and an ungranted loopback host is refused before any dial.</summary>
    [Fact]
    [Trait("Category", "Live")]
    public async Task GrantedOrigin_LoadsThroughTheProxy_AndUngrantedHost_IsRefused()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("AUTONOGRAMMER_LIVE_BROWSER"), "1", StringComparison.Ordinal))
        {
            Assert.Skip("Set AUTONOGRAMMER_LIVE_BROWSER=1 to run the production page live test.");
        }

        var chrome = Environment.GetEnvironmentVariable("CHROME_BIN");
        if (string.IsNullOrEmpty(chrome))
        {
            Assert.Skip("Set CHROME_BIN to the Chromium binary.");
        }

        using var origin = new LoopbackOrigin();
        var grants = new OriginGrants();
        grants.TryAdd(new LocalOriginGrant("127.0.0.1", origin.Port)).ShouldBeTrue();
        var (pages, host) = ProductionBrowserPages.Create(new BrowserUseOptions(), chrome, _ => grants, Launcher());
        await using var hostDisposal = host.ConfigureAwait(true);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(60));
        var token = deadline.Token;
        var serving = origin.ServeAsync(token);
        try
        {
            var page = await pages.OpenAsync(SessionId, token).ConfigureAwait(true);
            pages.Opens.ShouldBe(1);

            var granted = "http://127.0.0.1:" + origin.Port.ToString(CultureInfo.InvariantCulture) + "/granted";
            _ = await page.SendAsync("Page.navigate", Params("{\"url\":" + Quoted(granted) + "}"), waitForCallback: true, options: null).ConfigureAwait(true);
            (await SettledBodyAsync(page, granted, token).ConfigureAwait(true)).ShouldBe(GrantedBody);
            origin.Paths.ShouldContain("/granted", StringComparer.Ordinal);

            var denied = "http://localhost:" + origin.Port.ToString(CultureInfo.InvariantCulture) + "/denied";
            _ = await page.SendAsync("Page.navigate", Params("{\"url\":" + Quoted(denied) + "}"), waitForCallback: true, options: null).ConfigureAwait(true);
            (await SettledBodyAsync(page, denied, token).ConfigureAwait(true)).ShouldNotBe(DeniedBody, StringComparer.Ordinal);
            origin.Paths.ShouldNotContain("/denied", StringComparer.Ordinal);
        }
        finally
        {
            origin.Dispose();
            try
            {
                await serving.ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    /// <summary>Waits until the document for <paramref name="url"/> has loaded or failed, then returns its text.</summary>
    private static async Task<string?> SettledBodyAsync(ICDPSession page, string url, CancellationToken cancellationToken)
    {
        var expression = "document.readyState === 'complete' && (location.href === '" + url + "' || location.protocol === 'chrome-error:') ? document.body.textContent : null";
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var payload = await page.SendAsync("Runtime.evaluate", Params("{\"expression\":" + Quoted(expression) + ",\"returnByValue\":true}"), waitForCallback: true, options: null).ConfigureAwait(true);
            if (payload is { } element
                && element.TryGetProperty("result", out var result)
                && result.TryGetProperty("value", out var value)
                && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(true);
        }
    }

    /// <summary>
    /// The desktop launcher, unchanged. Root cannot run Chromium's sandbox, so the same explicit variable the options validator names adds
    /// <c>--no-sandbox</c> for this test only. The production launcher never adds it.
    /// </summary>
    private static IChromiumLauncher Launcher() =>
        string.Equals(Environment.GetEnvironmentVariable(SandboxVariable), "1", StringComparison.Ordinal)
            ? new ExplicitNoSandboxLauncher()
            : new ChromiumProcessLauncher();

    /// <summary>A loopback HTTP origin that records each request path and serves a fixed body for two paths.</summary>
    private sealed class LoopbackOrigin : IDisposable
    {
        private const int MaxHeadBytes = 8192;
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly ConcurrentQueue<string> _paths = new();

        public LoopbackOrigin()
        {
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        }

        public int Port { get; }

        public string[] Paths => _paths.ToArray();

        /// <summary>Closes the listener. A pending <see cref="ServeAsync"/> then returns.</summary>
        public void Dispose() => _listener.Dispose();

        /// <summary>Serves connections until the listener is disposed or the token is cancelled.</summary>
        /// <param name="cancellationToken">Cancels the serving loop.</param>
        /// <returns>A task that completes when serving stops.</returns>
        public async Task ServeAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(true);
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (SocketException)
                {
                    return;
                }

                using (client)
                {
                    try
                    {
                        await ServeOneAsync(client, cancellationToken).ConfigureAwait(true);
                    }
                    catch (IOException)
                    {
                        // A client that resets mid-response is not the test's concern; the next connection is served.
                    }
                }
            }
        }

        private async Task ServeOneAsync(TcpClient client, CancellationToken cancellationToken)
        {
            var stream = client.GetStream();
            var path = PathOf(await ReadHeadAsync(stream, cancellationToken).ConfigureAwait(true));
            if (path is null)
            {
                return;
            }

            _paths.Enqueue(path);
            var (status, body) = path switch
            {
                "/granted" => ("200 OK", "<html><body>" + GrantedBody + "</body></html>"),
                "/denied" => ("200 OK", "<html><body>" + DeniedBody + "</body></html>"),
                _ => ("404 Not Found", string.Empty),
            };
            var response = "HTTP/1.1 " + status + "\r\nContent-Type: text/html\r\nContent-Length: "
                + body.Length.ToString(CultureInfo.InvariantCulture) + "\r\nConnection: close\r\n\r\n" + body;
            await stream.WriteAsync(Encoding.ASCII.GetBytes(response), cancellationToken).ConfigureAwait(true);
        }

        private static async Task<string> ReadHeadAsync(NetworkStream stream, CancellationToken cancellationToken)
        {
            var head = new StringBuilder();
            var buffer = new byte[1024];
            while (head.Length < MaxHeadBytes && !head.ToString().Contains("\r\n\r\n", StringComparison.Ordinal))
            {
                var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(true);
                if (read == 0)
                {
                    break;
                }

                head.Append(Encoding.ASCII.GetString(buffer, 0, read));
            }

            return head.ToString();
        }

        private static string? PathOf(string head)
        {
            var requestLine = head.Split("\r\n", 2, StringSplitOptions.None)[0];
            var parts = requestLine.Split(' ');
            if (parts.Length < 2)
            {
                return null;
            }

            var target = parts[1];
            return Uri.TryCreate(target, UriKind.Absolute, out var absolute) ? absolute.AbsolutePath : target;
        }
    }

    /// <summary>Starts Chromium with <c>--no-sandbox</c> appended. Used only when the named variable is 1.</summary>
    private sealed class ExplicitNoSandboxLauncher : IChromiumLauncher
    {
        public Task<IChromiumSession> LaunchAsync(ChromiumLaunchRequest request, CancellationToken cancellationToken) =>
            new ChromiumProcessLauncher().LaunchAsync(request with { Arguments = [.. request.Arguments, "--no-sandbox"] }, cancellationToken);
    }
}
