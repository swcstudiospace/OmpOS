#pragma warning disable CA2007
#pragma warning disable MA0004
#pragma warning disable xUnit1051
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
