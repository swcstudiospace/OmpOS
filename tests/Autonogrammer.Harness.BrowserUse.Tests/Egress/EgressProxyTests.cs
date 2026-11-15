#pragma warning disable CA2007
#pragma warning disable MA0004
#pragma warning disable xUnit1051
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Autonogrammer.Harness.BrowserUse.Egress;
using Autonogrammer.Harness.BrowserUse.Options;
using Autonogrammer.Harness.BrowserUse.Policy;
using Autonogrammer.Harness.BrowserUse.Tests.Support;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.BrowserUse.Tests.Egress;

/// <summary>Proxy forwarding, credentials, grants, and host policy.</summary>
public sealed class EgressProxyTests
{
    [Fact]
    public async Task PlainHttpAndConnect_ReachTheGrantedPort_AndMissTheUngrantedPort()
    {
        await using var site = LocalTestSite.Start();
        var grants = new OriginGrants();
        grants.TryAdd(new LocalOriginGrant("127.0.0.1", site.GrantedPort)).ShouldBeTrue();
        await using var proxy = NewProxy(grants);
        await proxy.StartAsync().ConfigureAwait(true);

        var plain = await SendAsync(proxy, "GET", "http://127.0.0.1:" + site.GrantedPort + "/plain", proxy.UserName, proxy.Password).ConfigureAwait(true);
        plain.ShouldContain("200");
        var connect = await ConnectAndGetAsync(proxy, "127.0.0.1", site.GrantedPort).ConfigureAwait(true);
        connect.ShouldContain("200");
        var denied = await SendAsync(proxy, "GET", "http://127.0.0.1:" + site.UngrantedPort + "/nope", proxy.UserName, proxy.Password).ConfigureAwait(true);
        denied.ShouldContain("403");

        site.GrantedLog.Count.ShouldBeGreaterThanOrEqualTo(2);
        site.UngrantedLog.Count.ShouldBe(0);
    }

    [Fact]
    public async Task WrongCredentials_FailClosed_AndDoNotHitTheTarget()
    {
        await using var site = LocalTestSite.Start();
        var grants = new OriginGrants();
        grants.TryAdd(new LocalOriginGrant("127.0.0.1", site.GrantedPort)).ShouldBeTrue();
        await using var proxy = NewProxy(grants);
        await proxy.StartAsync().ConfigureAwait(true);
        var response = await SendAsync(proxy, "GET", "http://127.0.0.1:" + site.GrantedPort + "/", "wrong", "creds").ConfigureAwait(true);
        response.ShouldContain("407");
        site.GrantedLog.Count.ShouldBe(0);
        site.UngrantedLog.Count.ShouldBe(0);
    }

    [Fact]
    public async Task GuardDown_FailsClosedWithEgressGuardUnavailable()
    {
        var proxy = NewProxy(new OriginGrants());
        var exception = await Should.ThrowAsync<EgressGuardUnavailableException>(() => proxy.DialAsync("8.8.8.8", 80).AsTask()).ConfigureAwait(true);
        exception.Message.ShouldContain("EgressGuardUnavailable");
        await proxy.DisposeAsync().ConfigureAwait(true);
        await Should.ThrowAsync<EgressGuardUnavailableException>(() => proxy.DialAsync("8.8.8.8", 80).AsTask()).ConfigureAwait(true);
    }

    [Fact]
    public async Task Strict_RefusesEveryUnlistedHost_WhilePromptAllowsListedHosts()
    {
        var listed = IPAddress.Parse("1.1.1.1");
        var resolver = new FakeHostResolver();
        resolver.Enqueue(listed);
        var connector = new RecordingConnector();
        var strict = new BrowserUseOptions { PolicyMode = PolicyMode.Strict };
        strict.AllowedHosts.Add("listed.example");
        await using var strictProxy = new EgressGuardProxy(strict, new OriginGrants(), resolver, connector);
        await strictProxy.StartAsync().ConfigureAwait(true);
        var refused = await strictProxy.DialAsync("other.example", 443).ConfigureAwait(true);
        refused.ShouldBeNull();
        resolver.Calls.ShouldBe(0);
        connector.Endpoints.Count.ShouldBe(0);

        var prompt = new BrowserUseOptions { PolicyMode = PolicyMode.PromptOnNewOrigin };
        prompt.AllowedHosts.Add("listed.example");
        var promptResolver = new FakeHostResolver();
        promptResolver.Enqueue(listed);
        var promptConnector = new RecordingConnector();
        await using var promptProxy = new EgressGuardProxy(prompt, new OriginGrants(), promptResolver, promptConnector);
        await promptProxy.StartAsync().ConfigureAwait(true);
        var allowed = await promptProxy.DialAsync("listed.example", 443).ConfigureAwait(true);
        allowed.ShouldNotBeNull();
        await allowed.DisposeAsync().ConfigureAwait(true);
        promptResolver.Calls.ShouldBe(1);
        promptConnector.Endpoints.Single().Address.ShouldBe(listed);
    }

    [Fact]
    public void OriginGrants_AcceptOnlyLocalHostsWithAnExplicitPort_AndNeverAlways()
    {
        var grants = new OriginGrants();
        grants.TryAdd(new LocalOriginGrant("localhost", 5000)).ShouldBeTrue();
        grants.TryAdd(new LocalOriginGrant("127.0.0.1", 5001)).ShouldBeTrue();
        grants.TryAdd(new LocalOriginGrant("[::1]", 5002)).ShouldBeTrue();
        grants.TryAdd(new LocalOriginGrant("127.0.0.2", 5003)).ShouldBeFalse();
        grants.TryAdd(new LocalOriginGrant("example.com", 80)).ShouldBeFalse();
        grants.TryAdd(new LocalOriginGrant("127.0.0.1", 0)).ShouldBeFalse();
        grants.TryAdd(new LocalOriginGrant("127.0.0.1", 9), always: true).ShouldBeFalse();
        grants.Contains("localhost", 5000).ShouldBeTrue();
        grants.Contains("127.0.0.1", 9).ShouldBeFalse();
        grants.Contains("::1", 5002).ShouldBeTrue();
    }

    [Theory]
    [InlineData("https://example.com/", true)]
    [InlineData("http://example.com/", true)]
    [InlineData("about:blank", true)]
    [InlineData("file:///etc/passwd", false)]
    [InlineData("chrome://settings", false)]
    [InlineData("chrome-extension://abc/page.html", false)]
    [InlineData("devtools://devtools/bundled/inspector.html", false)]
    [InlineData("view-source:https://example.com/", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("data:text/html,hi", false)]
    [InlineData("blob:https://example.com/id", false)]
    public void BrowserUrlPolicy_AllowsOnlyHttpHttpsAndAboutBlank(string input, bool allowed)
    {
        Uri.TryCreate(input, UriKind.Absolute, out var parsed).ShouldBeTrue();
        BrowserUrlPolicy.IsTopLevelSchemeAllowed(parsed!).ShouldBe(allowed);
    }

    [Fact]
    public void Open_IsAcceptedInConfig_AndIgnoredForDispatch()
    {
        var options = new BrowserUseOptions { PolicyMode = PolicyMode.Open };
        BrowserUrlPolicy.EffectiveMode(options, dispatchTurn: false).ShouldBe(PolicyMode.Open);
        BrowserUrlPolicy.EffectiveMode(options, dispatchTurn: true).ShouldBe(PolicyMode.PromptOnNewOrigin);
    }

    [Fact]
    public async Task PlainPost_ReachesTheOriginWithMethodAndBodyIntact()
    {
        await using var origin = new RawOrigin();
        var grants = new OriginGrants();
        grants.TryAdd(new LocalOriginGrant("127.0.0.1", origin.Port)).ShouldBeTrue();
        await using var proxy = NewProxy(grants);
        await proxy.StartAsync().ConfigureAwait(true);

        const string body = "name=autonogrammer&mode=post";
        var request = "POST " + OriginUrl(origin, "/submit") + " HTTP/1.1\r\n"
            + "Host: 127.0.0.1\r\n"
            + "Proxy-Authorization: " + BasicAuth(proxy) + "\r\n"
            + "Content-Type: application/x-www-form-urlencoded\r\n"
            + "Content-Length: " + body.Length.ToString(CultureInfo.InvariantCulture) + "\r\n"
            + "Connection: close\r\n\r\n" + body;
        var response = await SendRawAsync(proxy, request).ConfigureAwait(true);

        response.ShouldContain("200 OK");
        var received = origin.Requests.Single();
        received.ShouldStartWith("POST /submit HTTP/1.1\r\n");
        received.ShouldContain("\r\nContent-Type: application/x-www-form-urlencoded\r\n");
        received.ShouldEndWith("\r\n\r\n" + body);
    }

    [Fact]
    public async Task ProxyAuthorizationAndHopByHopHeaders_AreNotForwardedToTheOrigin()
    {
        await using var origin = new RawOrigin();
        var grants = new OriginGrants();
        grants.TryAdd(new LocalOriginGrant("127.0.0.1", origin.Port)).ShouldBeTrue();
        await using var proxy = NewProxy(grants);
        await proxy.StartAsync().ConfigureAwait(true);

        var request = "GET " + OriginUrl(origin, "/headers") + " HTTP/1.1\r\n"
            + "Host: 127.0.0.1\r\n"
            + "Proxy-Authorization: " + BasicAuth(proxy) + "\r\n"
            + "Proxy-Connection: keep-alive\r\n"
            + "Keep-Alive: timeout=5\r\n"
            + "TE: trailers\r\n"
            + "Upgrade: h2c\r\n"
            + "Connection: keep-alive, X-Drop\r\n"
            + "X-Drop: secret\r\n"
            + "X-Probe: kept\r\n\r\n";
        var response = await SendRawAsync(proxy, request).ConfigureAwait(true);

        response.ShouldContain("200 OK");
        var received = origin.Requests.Single();
        ShouldNotContainHeader(received, "Proxy-Authorization");
        ShouldNotContainHeader(received, "Proxy-Connection");
        ShouldNotContainHeader(received, "Keep-Alive");
        ShouldNotContainHeader(received, "TE");
        ShouldNotContainHeader(received, "Upgrade");
        ShouldNotContainHeader(received, "X-Drop");
        received.Contains("keep-alive", StringComparison.OrdinalIgnoreCase).ShouldBeFalse();
        received.Contains(proxy.Password, StringComparison.Ordinal).ShouldBeFalse();
        received.ShouldContain("\r\nX-Probe: kept\r\n");
    }

    [Fact]
    public async Task ChunkedPost_IsForwardedWithItsBodyDecodedAndAContentLength()
    {
        await using var origin = new RawOrigin();
        var grants = new OriginGrants();
        grants.TryAdd(new LocalOriginGrant("127.0.0.1", origin.Port)).ShouldBeTrue();
        await using var proxy = NewProxy(grants);
        await proxy.StartAsync().ConfigureAwait(true);

        var request = "POST " + OriginUrl(origin, "/chunked") + " HTTP/1.1\r\n"
            + "Host: 127.0.0.1\r\n"
            + "Proxy-Authorization: " + BasicAuth(proxy) + "\r\n"
            + "Transfer-Encoding: chunked\r\n"
            + "Connection: close\r\n\r\n"
            + "5\r\nhello\r\n6\r\n world\r\n0\r\n\r\n";
        var response = await SendRawAsync(proxy, request).ConfigureAwait(true);

        response.ShouldContain("200 OK");
        var received = origin.Requests.Single();
        received.ShouldContain("\r\nContent-Length: 11\r\n");
        ShouldNotContainHeader(received, "Transfer-Encoding");
        received.ShouldEndWith("\r\n\r\nhello world");
    }

    [Fact]
    public async Task Expect100Continue_IsAnsweredByTheProxy_AndTheBodyReachesTheOrigin()
    {
        await using var origin = new RawOrigin();
        var grants = new OriginGrants();
        grants.TryAdd(new LocalOriginGrant("127.0.0.1", origin.Port)).ShouldBeTrue();
        await using var proxy = NewProxy(grants);
        await proxy.StartAsync().ConfigureAwait(true);

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, proxy.Port).ConfigureAwait(true);
        await using var stream = client.GetStream();
        var head = "POST " + OriginUrl(origin, "/upload") + " HTTP/1.1\r\n"
            + "Host: 127.0.0.1\r\n"
            + "Proxy-Authorization: " + BasicAuth(proxy) + "\r\n"
            + "Expect: 100-continue\r\n"
            + "Content-Length: 4\r\n"
            + "Connection: close\r\n\r\n";
        await stream.WriteAsync(Encoding.Latin1.GetBytes(head)).ConfigureAwait(true);
        var interim = await ReadUntilHeadersAsync(stream).ConfigureAwait(true);
        interim.ShouldContain("100 Continue");
        await stream.WriteAsync(Encoding.Latin1.GetBytes("ping")).ConfigureAwait(true);
        var response = await ReadAsciiAsync(stream).ConfigureAwait(true);

        response.ShouldContain("200 OK");
        var received = origin.Requests.Single();
        ShouldNotContainHeader(received, "Expect");
        received.ShouldEndWith("\r\n\r\nping");
    }

    [Fact]
    public async Task OversizedBody_IsRefusedWith413_AndNeverReachesTheOrigin()
    {
        await using var origin = new RawOrigin();
        var grants = new OriginGrants();
        grants.TryAdd(new LocalOriginGrant("127.0.0.1", origin.Port)).ShouldBeTrue();
        await using var proxy = NewProxy(grants);
        await proxy.StartAsync().ConfigureAwait(true);

        var request = "POST " + OriginUrl(origin, "/big") + " HTTP/1.1\r\n"
            + "Host: 127.0.0.1\r\n"
            + "Proxy-Authorization: " + BasicAuth(proxy) + "\r\n"
            + "Content-Length: 10485761\r\n"
            + "Connection: close\r\n\r\n";
        var response = await SendRawAsync(proxy, request).ConfigureAwait(true);

        response.ShouldContain("413");
        origin.Requests.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public async Task PlainHttpsAbsoluteTarget_IsRefusedWith403_AndNeverReachesTheOrigin()
    {
        await using var origin = new RawOrigin();
        var grants = new OriginGrants();
        grants.TryAdd(new LocalOriginGrant("127.0.0.1", origin.Port)).ShouldBeTrue();
        await using var proxy = NewProxy(grants);
        await proxy.StartAsync().ConfigureAwait(true);

        var target = "https://127.0.0.1:" + origin.Port.ToString(CultureInfo.InvariantCulture) + "/";
        var response = await SendAsync(proxy, "GET", target, proxy.UserName, proxy.Password).ConfigureAwait(true);

        response.ShouldContain("403");
        origin.Requests.IsEmpty.ShouldBeTrue();
    }


    private static EgressGuardProxy NewProxy(OriginGrants grants) =>
        new(new BrowserUseOptions(), grants, new FakeHostResolver());

    private static async Task<string> SendAsync(EgressGuardProxy proxy, string method, string url, string user, string password)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, proxy.Port).ConfigureAwait(true);
        await using var stream = client.GetStream();
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes(user + ":" + password));
        var request = method + " " + url + " HTTP/1.1\r\nHost: 127.0.0.1\r\nProxy-Authorization: Basic " + token + "\r\nConnection: close\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(request)).ConfigureAwait(true);
        return await ReadAsciiAsync(stream).ConfigureAwait(true);
    }

    private static async Task<string> ConnectAndGetAsync(EgressGuardProxy proxy, string host, int port)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, proxy.Port).ConfigureAwait(true);
        await using var stream = client.GetStream();
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes(proxy.UserName + ":" + proxy.Password));
        var connect = "CONNECT " + host + ":" + port.ToString(System.Globalization.CultureInfo.InvariantCulture) + " HTTP/1.1\r\nHost: " + host + ":" + port.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\r\nProxy-Authorization: Basic " + token + "\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(connect)).ConfigureAwait(true);
        var established = await ReadUntilHeadersAsync(stream).ConfigureAwait(true);
        established.ShouldContain("200");
        var get = "GET /connect HTTP/1.1\r\nHost: " + host + ":" + port.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\r\nConnection: close\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(get)).ConfigureAwait(true);
        return await ReadAsciiAsync(stream).ConfigureAwait(true);
    }

    private static async Task<string> ReadUntilHeadersAsync(NetworkStream stream)
    {
        using var buffer = new MemoryStream();
        var window = new byte[256];
        while (buffer.Length < 4096)
        {
            var read = await stream.ReadAsync(window).ConfigureAwait(true);
            if (read == 0)
            {
                break;
            }

            await buffer.WriteAsync(window.AsMemory(0, read)).ConfigureAwait(true);
            if (buffer.GetBuffer().AsSpan(0, (int)buffer.Length).IndexOf("\r\n\r\n"u8) >= 0)
            {
                break;
            }
        }

        return Encoding.ASCII.GetString(buffer.ToArray());
    }

    private static async Task<string> ReadAsciiAsync(NetworkStream stream)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer).ConfigureAwait(true);
        return Encoding.ASCII.GetString(buffer.ToArray());
    }

    private static string OriginUrl(RawOrigin origin, string path) =>
        "http://127.0.0.1:" + origin.Port.ToString(CultureInfo.InvariantCulture) + path;

    private static string BasicAuth(EgressGuardProxy proxy) =>
        "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(proxy.UserName + ":" + proxy.Password));

    private static void ShouldNotContainHeader(string raw, string name) =>
        raw.Contains("\r\n" + name + ":", StringComparison.OrdinalIgnoreCase).ShouldBeFalse(name);

    private static async Task<string> SendRawAsync(EgressGuardProxy proxy, string request)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, proxy.Port).ConfigureAwait(true);
        await using var stream = client.GetStream();
        await stream.WriteAsync(Encoding.Latin1.GetBytes(request)).ConfigureAwait(true);
        return await ReadAsciiAsync(stream).ConfigureAwait(true);
    }

    private static int ContentLengthOf(string head)
    {
        foreach (var line in head.Split("\r\n"))
        {
            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
            {
                return int.Parse(line["Content-Length:".Length..].Trim(), CultureInfo.InvariantCulture);
            }
        }

        return 0;
    }

    private static async Task<string> ReadRequestAsync(NetworkStream stream)
    {
        using var buffer = new MemoryStream();
        var window = new byte[1024];
        while (true)
        {
            var text = Encoding.Latin1.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
            var headEnd = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            if (headEnd >= 0 && text.Length - (headEnd + 4) >= ContentLengthOf(text[..headEnd]))
            {
                return text;
            }

            var read = await stream.ReadAsync(window).ConfigureAwait(true);
            if (read == 0)
            {
                return text;
            }

            await buffer.WriteAsync(window.AsMemory(0, read)).ConfigureAwait(true);
        }
    }

    /// <summary>A raw origin that records one request verbatim and answers 200 with body "ok".</summary>
    private sealed class RawOrigin : IAsyncDisposable
    {
        private static readonly byte[] OriginResponse = "HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\n\r\nok"u8.ToArray();
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stop = new();
        private readonly Task _serve;

        public RawOrigin()
        {
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _serve = ServeOnceAsync();
        }

        public int Port { get; }

        public ConcurrentQueue<string> Requests { get; } = new();

        public async ValueTask DisposeAsync()
        {
            await _stop.CancelAsync();
            _listener.Stop();
            (_listener as IDisposable)?.Dispose();
            _ = _serve.ContinueWith(
                static completed => _ = completed.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);

            _stop.Dispose();
        }

        private async Task ServeOnceAsync()
        {
            using var client = await _listener.AcceptTcpClientAsync(_stop.Token).ConfigureAwait(true);
            var stream = client.GetStream();
            Requests.Enqueue(await ReadRequestAsync(stream).ConfigureAwait(true));
            await stream.WriteAsync(OriginResponse).ConfigureAwait(true);
        }
    }

    private sealed class RecordingConnector : ITcpConnector
    {
        public List<IPEndPoint> Endpoints { get; } = [];

        public ValueTask<Stream> ConnectAsync(IPEndPoint endpoint, CancellationToken cancellationToken)
        {
            Endpoints.Add(endpoint);
            return ValueTask.FromResult<Stream>(new MemoryStream());
        }
    }
}
